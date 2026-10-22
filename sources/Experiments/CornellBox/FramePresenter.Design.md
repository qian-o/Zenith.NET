# 用户层 FramePresenter 设计草案

## 1. 定位

在 CornellBox 用户层实现一个包含渲染线程和呈现线程的类，不修改 RHI，不负责帧生成算法。

窗口线程负责平台消息和光标设置，渲染线程负责更新、渲染及尺寸调整，呈现线程负责消费输出。资源在启动前创建，停止后销毁。

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
- 构造参数提供初始有效尺寸、颜色格式及交换链选项；呈现器按选项创建交换链和三张输出纹理，复制到 drawable 和 Present 都使用交换链所在队列。
- Update、Render、Resize 都在渲染线程同步触发。帧事件参数为实际经过秒数；首轮从渲染循环开始计时，Resize 后重新计时。
- 用户在 Render 内按显示顺序调用 Submit，一次 Render 可以提交 n 张生成帧及一张真实帧。
- Width、Height 直接取自交换链当前尺寸，即已采用的 framebuffer 像素尺寸，供渲染线程读取。
- RequestResize 只记录最新请求尺寸，不更新 Width、Height，不影响当前任务，也不立即暂停呈现线程。
- 不公开格式、频率属性、错误事件或异步生命周期接口。

## 3. 渲染与呈现

### 渲染线程

渲染线程按实际执行耗时自然推进，不设目标帧率或额外限频等待：

```text
previousStart = 当前单调时钟

循环：
    等待空闲槽位
    读取停止标记和最新请求尺寸，已停止则退出
    请求尺寸为零：丢弃待呈现输出，等待新请求或停止
    请求尺寸与当前尺寸不同：重建资源
    否则：
        start = 当前单调时钟
        deltaSeconds = start - previousStart
        previousStart = start
        Update(deltaSeconds)
        Render(deltaSeconds)
```

槽位等待放在输入消费之前，避免 Update 读取暂时无法提交的输入。deltaSeconds 按相邻实际启动时刻计算，包含工作耗时和槽位等待；暂停及重建后重置计时基准，不将这段耗时带入恢复后的首轮更新。

### 呈现线程

呈现线程无输出时等待，有输出时依次向交换链所在队列提交复制到 drawable 的命令，再调用现有 Present；Present 返回后归还槽位，不额外等待复制或增加软件限速。只使用 RHI 接口，不检测图形 API 或原生队列，不在用户层补偿后端差异。交换链是否按刷新率推进取决于垂直同步等平台条件，不承诺固定呈现耗时或实际帧率。

## 4. Submit 与输出缓冲

Submit 是同步快照复制：

1. 从空闲队列取得槽位，没有空槽就等待。
2. 在 GraphicsQueue 上将输入纹理复制到槽位，连接 waits，并恢复源纹理布局。
3. 等待复制完成。
4. 放入待呈现队列，然后返回。

用户先提交生成源图像的命令，再调用 Submit。Submit 返回后，本类不再使用源纹理，用户可以复用；与本次复制无关的 GPU 使用仍由用户负责。

输入要求同 Context、与呈现器当前尺寸及格式一致、单采样 2D，具备 TransferSrc 用途。内部纹理具备 TransferSrc、TransferDst 用途；每次复制都整幅覆盖，因此从 Undefined 转换，不跟踪布局。不内置缩放、格式转换或 resolve。

槽位流转：

```text
空闲队列 -> Submit 复制 -> 待呈现队列 -> 呈现线程 -> 空闲队列
待呈现队列 -> 空闲队列（丢弃）
```

槽位只在空闲队列和待呈现队列这两个线程安全队列之间流转，或暂时由正在复制、呈现的线程持有。持有关系本身就是状态，不另设占用标记、组号或布局记录。复制完成后才放入待呈现队列，呈现结束后才归还槽位。

每次 Submit 独立发布，不等 Render 结束，所以提交数量可以超过槽位数量。

### 丢帧规则

一次 Render 的输出属于一组：

- 同组按提交顺序呈现，不互相淘汰。
- 新组首张图像复制完成并发布后，丢弃旧组尚在待呈现队列中的输出。
- 不撤回已经开始呈现的图像。
- 没有空闲槽位时仍然等待，不无限积压。

渲染线程在 Render 前标记下一次 Submit 为本组首张输出，该次 Submit 发布前清空待呈现队列。队列顺序就是呈现顺序，不需要组编号、帧类别或可配置丢帧策略。

## 5. 生命周期与尺寸调整

### 启停与销毁

Start 启动两个工作线程。Stop、Dispose 由外部线程调用，不在回调内等待自身退出；Dispose 在停止后释放内部资源。

Stop 不再启动新一轮回调。当前 Render 需要槽位时，呈现线程继续消费，直到 Render 完成；随后渲染线程丢弃尚未开始呈现的输出并结束待呈现队列，呈现线程完成当前呈现后随之退出，两个线程退出后 Stop 返回。呈现器自身的 GPU 工作此时都已完成：Submit 的复制同步等待，Present 返回时所在队列已完成，因此不再额外等待队列空闲。不要求显示完队列，不取消正在执行的 Submit；下次 Start 重新创建待呈现队列。

不封装 try/catch/finally，不保存或转发后台异常，不提供恢复和故障清理机制；异常直接暴露。驱动挂起时不保证停止耗时。

### 尺寸调整

RequestResize 只保存最新请求尺寸，处理后也不清空。渲染线程在每轮任务边界比较请求尺寸与交换链当前尺寸：相同则渲染，为零则暂停，不同则重建。暂停状态由请求尺寸推导，不另存标记，Stop 后再次 Start 仍然保持。

重建在渲染线程串行处理：

1. 清空待呈现输出，并收回全部槽位；收回会等待当前呈现结束，此后呈现线程空闲，呈现器的 GPU 使用也已结束。
2. 调整交换链并重建内部纹理，Width、Height 随交换链更新，再触发 Resize 事件调整应用资源。
3. 新槽位放回空闲队列，恢复运行；期间收到的新尺寸留到下一轮处理。

零尺寸请求在任务边界进入暂停：丢弃待呈现输出，不创建零尺寸纹理、不触发 Resize 事件，Width、Height 保持最后一次有效尺寸。恢复后的尺寸与当前一致时直接继续，否则按上述流程重建。

## 6. 平台处理器

平台处理器只负责消息收集和平台参数：

- 原生键鼠回调追加消息；渲染线程 Update 取出一批，依次交给相机和 ImGui。GLFW 在窗口失焦时会自行释放所有按下的键和鼠标按钮，不另设失焦消息。只有消息批次的追加和取出需要加锁。
- DPI 缩放在构造时由 framebuffer 尺寸与窗口尺寸计算一次，之后不再更新。
- 剪贴板读写直接调用平台键盘接口，不做缓存；只有 ImGui 复制、粘贴时才会在渲染线程调用。
- GLFW 与 SDL 都只允许在主线程修改光标。渲染线程只把最新光标形状写入一个 volatile 字段，由窗口 Update 应用；相同形状由 Silk 跳过。
- 不处理平台异常，也不替换窗口后端的错误回调。

只用消息集合和一个光标字段，不增加命令调度器、消费游标或版本系统。

## 7. CornellBox 对接

```text
初始化平台处理器、ImGui、呈现器、相机、Renderer
注册呈现器 Update、Render、Resize
窗口 framebuffer 尺寸变化 -> RequestResize
Start
window.Run()
Stop
销毁资源
```

- [App.cs](App.cs) 不再使用窗口 Render；窗口 Update 只服务平台处理器，不执行应用更新或渲染。
- [CameraHandler.cs](Handlers/CameraHandler.cs) 和 [ImGuiHandler.cs](Handlers/ImGuiHandler.cs) 通过平台处理器获取输入，运行期间相机和全部 ImGui 帧处理固定在渲染线程。
- Resize 事件中只调用 [Renderer.Resize](Renderer.cs)。
- 渲染期间统一使用呈现器 Width、Height，并按构造时确定的 DPI 缩放换算出逻辑尺寸，供相机和 ImGui 使用；鼠标坐标本身就是逻辑坐标，不再读取窗口尺寸。ImGui 的 framebuffer 缩放只在初始化时设置一次。
- UI 直接以 Load 方式叠加绘制到 Renderer 的输出纹理上，再提交该纹理，不另建合成纹理。输出纹理为 B8G8R8A8UNorm，用途为 ColorAttachment、TransferSrc，绘制后保持 ColorAttachment 布局，提交时如实传入该布局。输出 Pass 与 ImGui Pass 先后写同一纹理，中间以同布局 ColorAttachment Transition 同步，与 FluidTank 中跨 Pass 继续使用附件的做法一致。
- 场景及 UI 命令先提交，再调用呈现器 Submit。其同步等待覆盖同 GraphicsQueue 的先前工作，正常路径不需要额外保存上一轮完成值；没有调用 Submit 或另有其他队列工作时，应用自行保证资源复用安全。
- 交换链关闭垂直同步，启用独立呈现队列。Context 和窗口由应用持有，所有工作停止后再释放。

## 8. 验证重点

- 一轮提交 0、1、多于 3 张图像，不死锁，顺序与跨组丢帧正确。
- Submit 返回后立即复用源纹理，图像正确。
- 满槽时 Stop、连续 resize、最小化恢复均能完成，不发生并发资源销毁。
- 尺寸请求不改变当前帧的 Width、Height；实际重建后、Resize 事件触发前才更新，同一轮 Update/Render 使用一致尺寸。
- 相机和 ImGui 收到同批输入，光标在窗口 Update 中生效。
- DX12、Vulkan、Metal 验证层无相关同步错误。

实现文件：[FramePresenter.cs](FramePresenter.cs)、[PlatformHandler.cs](Handlers/PlatformHandler.cs)。平台输入在 [PlatformInput.cs](Handlers/PlatformInput.cs) 和 [PlatformInputType.cs](Handlers/PlatformInputType.cs) 中定义。

- [Apple 独立呈现线程](https://github.com/apple/game-porting-toolkit/blob/main/game-porting-skills/skills/using-metalfx-frame-interpolation/references/present-thread.md)
