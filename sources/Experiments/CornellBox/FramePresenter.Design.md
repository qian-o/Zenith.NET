# 用户层 FramePresenter 设计草案

## 1. 定位与约束

在 CornellBox 用户层实现一个可直接修改的类，内部包含渲染线程和呈现线程。不纳入 RHI，不引入通用调度框架，不实现帧生成算法。

- 外部同步启动、停止、调整尺寸，窗口消息循环仍由应用控制。
- 渲染线程依次触发 Update、Render；用户在 Render 内主动 Submit 输出纹理。
- 一次 Render 可以提交 n 张生成帧及 1 张真实帧，提交顺序就是期望显示顺序。
- Submit 将输入复制到类持有的纹理槽位，不长期借用用户纹理。
- 渲染使用累积截止时间限制目标调用频率；呈现不增加软件限速。
- 正常时同组有序；积压时允许淘汰旧组尚未进入呈现的输出。
- 不保证实际 FPS、精确显示时刻或固定输入延迟。
- 不公开 Format、Width、Height、PresentRate、Completion；不提供异步生命周期方法。

本次只确定设计，不修改现有运行路径或 RHI。

## 2. 最小对外形态

以下为接口草图，不是已存在的类型：

```csharp
internal sealed class FramePresenter : IDisposable
{
    public FramePresenter(
        GraphicsContext context,
        SwapChainDesc desc);

    public event Action<double>? Update;
    public event Action<double>? Render;

    public void Start();
    public void Stop();
    public void Resize(uint width, uint height);

    public TimelineValue Submit(
        Texture texture,
        TextureLayout currentLayout,
        params ReadOnlySpan<TimelineValue> waits);

    public void Dispose();
}
```

- 渲染目标周期是类内部可直接修改的常量，初版取 1/60 秒；不提供频率构造参数或属性。
- 两个帧事件接收本轮实际启动时间与上轮实际启动时间之差，单位为秒；首轮和暂停恢复后的首轮使用目标周期，不补偿暂停时间，也不向 ImGui 传入零 DeltaTime。
- Update、Render 都是同步回调，在渲染线程执行，不支持 async void。
- Submit 只允许在当前 Render 回调内、由渲染线程调用。每个回调入口分配新的组编号。
- Context、窗口及原生 surface 的宿主对象由应用持有；呈现器持有并释放交换链和内部输出纹理。
- 构造参数给出初始尺寸、格式、垂直同步等信息；呈现器内部启用独立 PresentQueue，不对外再加队列策略参数。

不增加专门的错误事件。后台故障在发生时写入应用错误输出，保存原始异常并停止两个工作循环；Stop 在清理后向控制线程重新抛出故障。

## 3. 线程所有权

| 对象或操作 | 所在线程 |
|---|---|
| 窗口事件泵、原生输入回调、窗口/剪贴板/光标操作 | 窗口线程 |
| 最新输入快照的消费、相机更新、Update、Render、ImGui 构帧 | 渲染线程 |
| 用户场景渲染、输出快照复制 | 渲染线程，GraphicsQueue |
| 交换链创建、drawable、最终复制、Present、Resize、销毁 | 呈现线程，PresentQueue |
| Start、Stop、Resize、Dispose | 同一个外部控制线程，通常是窗口线程 |

控制线程同步等待时，工作线程不得反向同步请求控制线程执行任务。原生窗口或 layer 的线程亲和操作必须先在窗口线程完成，不能假设交换链工作线程可调用所有窗口 API。

初次 Start 前可只保存描述，延迟到呈现线程创建交换链。Stop 保留资源并允许再次 Start；Dispose 在线程退出前完成线程所属资源的销毁。

## 4. 渲染循环与输入时机

参考 Godot 的累积截止时间，不使用“每帧结束后固定睡一个周期”，不引入独立 pacing 线程。

累积截止时间算法仍需要目标周期，并不会根据工作耗时自动推导期望频率。这里将周期保留为内部常量，用户需要其他节奏时直接修改这个用户层类。

```text
period = Stopwatch.Frequency * 内部目标周期秒数
deadline = 当前单调时钟

循环：
    等待 deadline，允许控制请求打断
    等待可安全使用的输出容量，允许控制请求打断
    检查停止、暂停和故障

    记录实际启动时刻并计算 deltaSeconds
    Update(deltaSeconds)
    Render(deltaSeconds)

    deadline += period
    now = 当前单调时钟
    deadline = Clamp(deadline, now - period, now + period)
```

- 使用单调时钟，保留周期的小数精度，不能依赖墙上时间或每轮整数毫秒截断。
- 等待放在 Update 前，使输入尽量接近实际渲染时刻采样。
- 轻微迟到允许短期补偿；截止时间偏差限制在一个周期内，不补跑积累的模拟步骤或多次 Update。
- GPU、回调、背压耗时都计入周期。超预算时不额外睡完整周期。
- 首版使用可中断的阻塞等待，醒来复查时钟；不默认加入忙等、平台高精度定时器或预测耗时。
- 暂停、resize 和重新启动后重置截止时间及 deltaSeconds 基准。
- Render 若没有 Submit，呈现线程继续等待，渲染线程仍受周期限制，不会空转。

容量等待不必坚持只取完全空闲槽：过时且复制已经完成的槽也可以回收。但在新组真正发布之前，不为制造空槽而无条件删除上一组所有输出。

## 5. 三槽位与 Submit

固定维护 3 张输出纹理，不暴露缓冲数量。它是内存和在途工作上限，不是必须显示完的三帧 FIFO。

每个槽位记录：纹理、尺寸代际、组编号、组内序号、实际布局、快照复制完成值及状态。

```text
Free -> Copying/Queued -> Presenting -> Free
                    \-> Retiring -> Free
```

Retiring 表示取消了显示资格，但相关 GPU 使用还没有完成。

Submit 的执行顺序：

1. 验证线程、运行状态、纹理尺寸/格式/类型/用途和布局约定。
2. 获取一个安全可写槽位；不足时等待，不持有呈现线程所需的锁。
3. 在 GraphicsQueue 录制源纹理到槽位的复制及布局转换。
4. 源纹理从 currentLayout 转到 CopySrc，复制后恢复 currentLayout；槽位按真实旧布局转到 CopyDst，结束后保持 CopySrc。
5. 提交复制命令，连接 waits，取得复制完成值。
6. 在锁内发布槽位并执行跨组淘汰判定，然后唤醒呈现线程。
7. 返回复制完成值，不等待 GPU 复制结束。

用户应先提交产生源图像的命令，再调用 Submit，不能把仍在录制的生产命令留到 Submit 之后提交。

- 同 GraphicsQueue 的后续覆盖由队列顺序保护。
- 其他队列生产源图像时通过 waits 建立依赖；其他队列再次写入时等待返回值。
- 返回值完成之前不能销毁源纹理。CPU 直接覆盖相关内存也必须满足 GPU 生命周期要求。
- 首版只接受同 Context、同尺寸、同格式、单采样的 2D 输出，且具备 TransferSrc 用途；不暗中执行缩放、格式转换、色彩转换或 MSAA resolve。
- 内部纹理具备 TransferDst 和 TransferSrc 用途。初次使用可为 Undefined，之后记录实际布局，不把所有重用都当作 Undefined。
- 生成历史和场景资源由用户管理，快照复制不代表历史输入可以提前销毁。

每次 Submit 立即发布，不等 Render 回调结束，所以一组超过三个输出也能由两个线程流水处理，不会因整组无法放入缓冲而死锁。

## 6. 输入延迟与丢帧规则

一次 Render 内的输出构成一组；类不识别真实帧和生成帧，不公开帧类型参数。

固定规则：

1. 没有更新组时，同组按照 Submit 顺序消费，不因同组后续输出到达而删除前面的生成帧。
2. 新组首张输出成功提交复制并发布后，取消旧组尚未被呈现线程取走的输出的显示资格。
3. 新组进入 Render 本身不是丢帧触发点；否则渲染很慢或最终没有输出时，会不必要地饿死呈现。
4. 已被呈现线程取走的槽位视为 Presenting，不撤回；后续 GPU 提交或交换链呈现更不尝试撤回。
5. 被取消的槽位转入 Retiring，等复制完成才能重用；丢弃不等于立即覆盖纹理。
6. 新组没有安全槽位时仍需等待。可以先完成/消费旧输出以推进，不能为了“必须先有新组”造成循环等待。

发布、淘汰和呈现线程取走槽位的状态转移在同一短临界区完成。不能在锁外判定“还没被消费”，随后覆盖一个已经被取走的槽。

淘汰触发点选择“复制已提交”而非“复制已完成”，便于立即控制旧队列深度；代价是新组 GPU 迟到时可能暂时没有可显示旧输出。这是明确的低延迟取舍，不是保证无间断输出。

呈现线程只对 Ready.IsCompleted 的候选槽启动最终复制；未完成时采用可中断的短超时等待并重新检查队列，使尚未取走的旧组仍有淘汰机会。不在持锁状态下对它执行无限 TimelineValue.Wait。

限制：

- 如果组与组之间输入相同，组编号本身不会创造更低的输入年龄。
- 不提供帧时间戳，因此不能判断每张图像的真实模拟时间；用户负责按正确时序提交。
- 持续过载时可能经常舍弃生成帧，优先避免过时输出堆积；不保证每张真实帧必显。
- 驱动、GPU 和交换链内部的排队延迟不能由此队列全部消除。

## 7. 呈现路径与 RHI 边界

```text
等待存在可用输出或控制请求
选择并原子标记槽位 Presenting
PresentQueue 复制槽位图像到 drawable，完成布局转换
等待最终复制完成
调用现有 SwapChain.Present()
GPU 已不再读取内部槽位后归还槽位
```

- 不额外设置呈现周期；无输出时阻塞，有输出时顺序推进。
- 不把 Present 耗时当作恒定周期。关闭垂直同步时，不承诺显示刷新率限速。
- 当前 Vulkan 呈现没有 render-finished 等待信号量，首版保留最终复制提交后的 CPU Wait，不能仅删除等待。
- 保留现有 SwapChain.Present 的队列等待及获取下一张图像行为，不新增异步交换链接口。
- Present 后的队列值不是“屏幕已经显示完成”的通用证明。
- 两个逻辑队列若映射到同一原生队列，必须满足原生队列宿主同步要求；不同包装对象的锁并不自动足够。
- Timeline.Wait 当前持有时间线锁。不能在等待未来 signal 的同时阻止生产者提交该 signal；所有依赖先提交，禁止构造环形依赖。

现有 RHI 的 GPU 无限等待没有取消接口，因此 Stop 的“唤醒”只保证打断本类 CPU 等待，不能保证在 GPU/驱动挂起时有界返回。不能承诺强制终止工作线程或安全释放仍被 GPU 使用的资源。

## 8. 生命周期、Resize 与错误

- Start、Stop 幂等；Dispose 后禁止重启。
- Start 等待初始化成功再返回，初始化失败在调用线程报告。
- Stop 发出停止请求，唤醒期限等待和槽位等待，退出回调，撤销待呈现输出，等待已提交 GPU 使用，结束工作线程。
- 被控制请求打断且尚未实际提交的 Submit 明确抛出取消异常，不返回伪成功值。内部只把本类对应的控制取消当作正常退出，其他异常视为故障。
- Stop 不可能抢占任意用户回调。回调必须有限执行，不能等待窗口线程响应。
- Resize 在运行中建立暂停屏障，安全结束旧尺寸工作并重建内部资源后恢复；尺寸代际不同的输出绝不混用。
- 0 宽或高暂停两个工作循环，不创建零尺寸纹理；恢复有效尺寸后重建并继续。
- 由工作线程调用 Stop、Resize、Dispose 立即报错，避免线程等待自身。
- 首个后台故障立即写入应用错误输出并保存，停止两个循环并唤醒所有等待；不新增错误通知事件，不覆盖最初故障。
- Stop 在完成清理后向外重新抛出保存的故障。Dispose 即使遇到故障也必须尝试释放其余资源。
- 不静默忽略异常，不使用 Thread.Abort，不假定阻塞的驱动调用可安全取消。

## 9. CornellBox 对接边界

### 9.1 保留窗口线程，不直接搬运原有回调

[App.cs](App.cs) 目前在 window.Update/window.Render 中串行操作输入、相机、ImGui、场景和交换链。接入后窗口线程仍负责事件泵、输入采集和生命周期；Update/Render 实际逻辑交给呈现器渲染线程。

当前 App 创建交换链时没有启用垂直同步。接入验证需显式选择垂直同步，否则不能把“交换链自动按刷新率推进”当作已有条件。

窗口线程不能直接写渲染线程正在使用的相机或 UI 状态：

- [CameraHandler.cs](Handlers/CameraHandler.cs) 的 HashSet、朝向及鼠标状态目前由输入回调直接修改，需改成窗口线程收集事件，渲染线程在 Update 消费。
- framebuffer 尺寸、逻辑尺寸、DPI 使用一致的快照，不能在渲染线程随意读取原生窗口对象。
- ImGui 输入在窗口线程收集，在渲染线程开始 ImGui 帧前消费。
- [ImGuiHandler.cs](Handlers/ImGuiHandler.cs) 的光标、剪贴板等操作回到窗口线程处理；不能在窗口线程正同步 Stop/Resize 时向它发同步请求。
- ImGui Context 的选择和所有 ImGui 构帧调用固定在渲染线程，避免跨线程读写。

现有 ImGui 后端使用输入 List；不能假设它天然支持并发追加和清空。优先在 CornellBox 适配层排队，然后在渲染线程转发给原接口，不把并发改造扩散为无关 RHI 变更。

### 9.2 最终输出必须包含 UI

[Renderer.Color](Renderer.cs) 目前是场景色，UI 则在 [App.OnRender](App.cs) 中直接绘制到 drawable。不能只提交 Renderer.Color，否则 UI 会丢失。

新增应用持有的最终合成纹理，尺寸与构造描述一致，格式为 B8G8R8A8UNorm，用途包含 ColorAttachment 和 TransferSrc：

```text
Renderer.Render：场景输出
EndImGuiFrame：场景背景图 + UI 写入离屏合成纹理
提交生产命令
presenter.Submit(合成纹理, 当前布局)
```

场景色若仍只被 ImGui 采样，不需要为了呈现器给它增加 TransferSrc；实际被 Submit 的最终合成纹理需要具备该用途。未来生成帧和真实帧的 UI 合成时序由应用决定，不交给通用呈现器猜测。

### 9.3 不能直接删掉现有 Submit().Wait()

[PathTracingPass](Passes/PathTracingPass.cs)、[OutputPass](Passes/OutputPass.cs) 以及 ImGui 当前会复用 CPU 可写常量/顶点/索引缓冲。队列顺序只能保护 GPU 命令顺序，不能防止 CPU 提前覆写上一帧尚在读取的内存。

CornellBox 首次对接保留一份“上一轮渲染及快照复制完成值”，在下一轮 Update 前通过用户层适配保证完成，再覆写这些资源。可轮询 IsCompleted，让停止检查有机会执行；不要把 GPU 复用等待放在输入采样之后。

保守版本允许渲染 GPU 工作串行，但仍与呈现线程解耦。需要进一步提高 GPU 并行度时，再为这些每帧资源增加独立槽位；呈现器的三张输出纹理不能替代场景每帧资源管理。

### 9.4 尺寸与关闭顺序

为了同时调整 Renderer 和应用合成纹理，CornellBox 的 resize 接入先采用简单同步边界：

```text
窗口线程：
    presenter.Stop()
    等待应用其他已提交 GPU 使用
    有效尺寸时：renderer.Resize，并重建最终合成纹理
    presenter.Resize(framebufferWidth, framebufferHeight)
    非零尺寸时：presenter.Start()
```

单独调用呈现器的 Resize 不会自动调整 Renderer。最小化时停止并暂停，恢复有效尺寸再启动。处理完成前不允许新的渲染回调读取旧资源。

关闭时先 Stop/Dispose 呈现器，再释放 Renderer、相机和 ImGui，之后释放输入、窗口/原生 layer，最后释放 Context。后台故障先报告并停止工作循环；窗口循环仍由应用控制，Stop 在清理后报告保存的异常，应用需通过 finally 保证后续资源继续释放。

## 10. 实现验收条件

后续实现至少覆盖：

- 一轮 0、1、2、超过 3 次 Submit；超过槽位数量不死锁。
- 同组不互相淘汰，新组只淘汰尚未取走的旧输出。
- 未完成的复制被取消后不提前复用槽位，内存保持固定上界。
- 有输出容量和截止时间等待时均可 Stop；重复启停不泄漏线程。
- 满槽时 resize、连续 resize、零尺寸恢复、关闭中 resize。
- 回调异常、GPU 提交失败和初始化失败均能通知并完成可执行的清理。
- 源纹理跨队列依赖、提交后立即同队列重写、提前销毁误用。
- 带 UI 的 CornellBox 在 DX12、Vulkan、Metal 下检查验证层消息和图像正确性。
- 人为拖慢渲染/呈现后，旧输出不会无限积压，不为赶期限连续补跑大量回调。
- 测量输入采样、GPU 提交、输出取走、Present 调用的时间；不要用 Present 返回时间冒充实际显示时间。

## 参考

- [Godot OS::add_frame_delay](https://github.com/godotengine/godot/blob/master/core/os/os.cpp)：累积目标时间与偏差约束的参考，不依赖其代码实现。
- [Apple PresentThread](https://github.com/apple/game-porting-toolkit/blob/main/game-porting-skills/skills/using-metalfx-frame-interpolation/references/present-thread.md)：有限在途工作、离屏输出及独立呈现线程的参考；本设计不照搬其第三个 pacing 线程。
