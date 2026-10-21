# 用户层 FramePresenter 设计草案

## 1. 定位

在 CornellBox 用户层实现一个包含渲染线程和呈现线程的类，不修改 RHI，不负责帧生成算法。

窗口线程负责平台消息和参数读写，渲染线程负责更新、渲染及尺寸调整，呈现线程负责消费输出。资源在启动前创建，停止后销毁。

## 2. 对外接口

以下为接口草图：

```csharp
internal class FramePresenter : DisposableObject
{
    public FramePresenter(GraphicsContext context, SwapChainDesc desc);

    public uint Width { get; }
    public uint Height { get; }

    public event Action<double>? Update;
    public event Action<double>? Render;
    public event Action<uint, uint>? Resize;

    public void Start();
    public void Stop();
    public void RequestResize(uint width, uint height);

    public void Submit(
        Texture texture,
        TextureLayout currentLayout,
        params ReadOnlySpan<TimelineValue> waits);
}
```

- Dispose 使用项目现有 DisposableObject 实现。
- 构造参数提供初始有效尺寸、颜色格式及交换链选项；内部使用独立 PresentQueue，创建交换链和三张输出纹理。
- Update、Render、Resize 都在渲染线程同步触发。帧事件参数为实际经过秒数；首轮从渲染循环开始计时，Resize 后重新计时。
- 用户在 Render 内按显示顺序调用 Submit，一次 Render 可以提交 n 张生成帧及一张真实帧。
- Width、Height 表示已采用的 framebuffer 像素尺寸，初值来自构造参数，供渲染线程读取。
- RequestResize 只记录最新待处理尺寸，不更新 Width、Height，不影响当前任务，也不立即暂停呈现线程。
- 不公开格式、频率属性、错误事件或异步生命周期接口。

## 3. 渲染与呈现

### 渲染线程

渲染线程按实际执行耗时自然推进，不设目标帧率或额外限频等待：

```text
previousStart = 当前单调时钟

循环：
    检查停止及尺寸请求
    等待可用输出容量或零尺寸暂停恢复，再次检查控制请求
    start = 当前单调时钟
    deltaSeconds = (start - previousStart) / Stopwatch.Frequency
    previousStart = start
    Update(deltaSeconds)
    Render(deltaSeconds)
```

容量及暂停等待放在输入消费之前。deltaSeconds 按相邻实际启动时刻计算，包含工作耗时和容量等待；Resize 后重置计时基准，不将暂停及资源重建耗时带入恢复后的首轮更新。

### 呈现线程

呈现线程无输出时等待，有输出时依次向呈现队列提交复制到 drawable 的命令，再调用现有 Present；Present 返回后归还槽位，不额外等待复制或增加软件限速。只使用 RHI 接口，不检测图形 API 或原生队列，不在用户层补偿后端差异。交换链是否按刷新率推进取决于垂直同步等平台条件，不承诺固定呈现耗时或实际帧率。

## 4. Submit 与输出缓冲

Submit 是同步快照复制：

1. 取得空闲槽位，没有空槽就等待。
2. 在 GraphicsQueue 上将输入纹理复制到槽位，连接 waits，并恢复源纹理布局。
3. 等待复制完成。
4. 发布槽位，唤醒呈现线程，然后返回。

用户先提交生成源图像的命令，再调用 Submit。Submit 返回后，本类不再使用源纹理，用户可以复用；与本次复制无关的 GPU 使用仍由用户负责。

输入要求同 Context、与呈现器当前尺寸及格式一致、单采样 2D，具备 TransferSrc 用途。内部纹理具备 TransferSrc、TransferDst 用途并记录实际布局。不内置缩放、格式转换或 resolve。

槽位流转：

```text
Free -> Copying -> Queued -> Presenting -> Free
Queued -> Free（丢弃）
```

发布前复制已经完成；呈现结束后才归还正在读取的槽位。状态变更用短临界区保护，GPU 等待和回调不放在锁内。

实现只记录槽位是否占用，待呈现队列区分可丢弃输出，无须为每个阶段保存枚举状态。

每次 Submit 独立发布，不等 Render 结束，所以提交数量可以超过槽位数量。

### 丢帧规则

一次 Render 的输出属于一组：

- 同组按提交顺序呈现，不互相淘汰。
- 新组首张图像复制完成并发布后，丢弃旧组尚在 Queued 状态的输出。
- 不撤回已经进入 Presenting 的图像。
- 没有安全槽位时仍然等待，不无限积压。

组编号和队列顺序足够，不增加帧类别、尺寸版本或可配置丢帧策略。

## 5. 生命周期与尺寸调整

### 启停与销毁

Start 启动两个工作线程。Stop、Dispose 由外部线程调用，不在回调内等待自身退出；Dispose 在停止后释放内部资源。

Stop 不再启动新一轮回调。当前 Render 需要槽位时，呈现线程继续消费，直到 Render 完成；随后丢弃尚未开始呈现的输出，等当前呈现及相关 GPU 工作完成、两个线程退出后返回。不要求显示完队列。空闲等待可被停止通知唤醒，不取消正在执行的 Submit。

不封装 try/catch/finally，不保存或转发后台异常，不提供恢复和故障清理机制；异常直接暴露。驱动挂起时不保证停止耗时。

### 尺寸调整

RequestResize 只保存最新请求。实际 Resize 在渲染线程当前任务结束后串行处理：

尺寸仅保存当前 Width、Height 和一组可为空的待处理尺寸。渲染线程在锁内取走待处理尺寸，作为本次重建的固定快照；后续请求保留到下一轮。呈现暂停标记同时用于零尺寸挂起，不另存一份挂起状态。

1. 呈现线程完成当前操作并暂停。
2. 清空待呈现输出，等待相关 GPU 使用结束。
3. 渲染线程调整交换链及内部纹理，更新 Width、Height，再触发 Resize 事件调整应用资源。
4. 恢复运行；期间收到的新尺寸留到下一轮处理。

零尺寸请求在任务边界进入暂停，不创建零尺寸纹理、不触发 Resize 事件，Width、Height 保持最后一次有效尺寸；恢复有效尺寸后按上述流程更新。

## 6. 平台处理器

平台处理器只负责消息收集和参数读写：

- 原生键鼠回调追加消息；渲染线程 Update 取出一批，依次交给相机和 ImGui。
- 窗口 Update 读取 DPI 缩放和剪贴板，并按需应用待写的剪贴板和鼠标状态；不保存逻辑尺寸或 framebuffer 尺寸。
- 渲染线程读取已保存参数，设置参数时记录待写值，下一次窗口 Update 应用。
- 消息追加、批次取出和参数更新加锁；平台调用、相机处理和 ImGui 调用都在锁外执行。未应用的写入不能被旧读取值覆盖。
- 取走待写剪贴板值时同步更新缓存，保证系统调用期间仍能读到该值；期间追加的新写入留待下一次窗口 Update 应用。

只用消息集合、参数字段和待写标记，不增加命令调度器、消费游标或版本系统。

## 7. CornellBox 对接

```text
初始化平台处理器、Renderer、ImGui、合成纹理、呈现器
注册呈现器 Update、Render、Resize
窗口 Resize -> RequestResize
Start
window.Run()
Stop
销毁资源
```

- [App.cs](App.cs) 不再使用窗口 Render；窗口 Update 只服务平台处理器，不执行应用更新或渲染。
- [CameraHandler.cs](Handlers/CameraHandler.cs) 和 [ImGuiHandler.cs](Handlers/ImGuiHandler.cs) 通过平台处理器获取输入，运行期间相机和全部 ImGui 帧处理固定在渲染线程。
- Resize 事件中调用 [Renderer.Resize](Renderer.cs)，并重建应用最终合成纹理。
- 窗口尺寸通知直接将 framebuffer 尺寸交给 RequestResize；渲染期间统一使用呈现器 Width、Height。相机宽高比使用该尺寸，ImGui 逻辑尺寸由该尺寸除以本轮固定的 DPI 缩放得到，不再读取窗口尺寸。
- 当前 UI 直接写 drawable，需改为写入 B8G8R8A8UNorm 离屏合成纹理，用途包含 ColorAttachment、TransferSrc，然后提交该纹理，不能只提交场景颜色。
- 场景及 UI 生产命令先提交，再调用呈现器 Submit。其同步等待覆盖同 GraphicsQueue 的先前工作，正常路径不需要额外保存上一轮完成值；没有调用 Submit 或另有其他队列工作时，应用自行保证资源复用安全。
- 交换链关闭垂直同步，启用独立呈现队列。Context 和窗口由应用持有，所有工作停止后再释放。

## 8. 验证重点

- 一轮提交 0、1、多于 3 张图像，不死锁，顺序与跨组丢帧正确。
- Submit 返回后立即复用源纹理，图像正确。
- 满槽时 Stop、连续 resize、最小化恢复均能完成，不发生并发资源销毁。
- 尺寸请求不改变当前帧的 Width、Height；实际重建后、Resize 事件触发前才更新，同一轮 Update/Render 使用一致尺寸。
- 相机和 ImGui 收到同批输入，参数写入在窗口 Update 生效。
- DX12、Vulkan、Metal 验证层无相关同步错误。

实现文件：[FramePresenter.cs](FramePresenter.cs)、[PlatformHandler.cs](Handlers/PlatformHandler.cs)。平台输入在 [PlatformInput.cs](Handlers/PlatformInput.cs) 中定义。

- [Apple 独立呈现线程](https://github.com/apple/game-porting-toolkit/blob/main/game-porting-skills/skills/using-metalfx-frame-interpolation/references/present-thread.md)
