# RHI 重设计草案（中文临时版）

> 仅用于本轮设计讨论，不进入提交。最终英文版同步在 `rhi-redesign.draft.md`。

## 设计目标

- 公共 API 表面尽量小、调用形态统一（结构体一律 `record struct`，参数无 `in`）
- 帧循环代码短、零托管分配
- 与 DirectX 12 / Vulkan 1.3 / Metal 4 的真实命令模型 1:1 对应
- 后续 RDG 层直接复用，无需破坏性变更
- 第三方适配（ImGui / glTF / 性能分析器）无需翻译层

## 命名与代码风格约定

- 所有公共值类型为 `record struct`，公共字段可写；不使用 `ref struct`，不使用 `in` 参数
- 多元素入参一律 `ReadOnlySpan<T>` / `params ReadOnlySpan<T>`，不使用 `T[]` / `IEnumerable<T>`
- 方法体一律 `{ ... }`；**例外**：`ref` / `ref readonly` 返回的属性（如 `public ref readonly TDesc Desc => ref desc;`）保留 `=>` 形式
- 所有以字节为单位的字段与参数加 `*InBytes` 后缀
- `slot` / `firstSlot` / `binding` 等图形领域标准术语保留无后缀
- 后端覆写钩子统一以 `*Core` 结尾（项目内现有 `*Impl` 全部改名为 `*Core`，`SetImpl` / `PreprocessImpl` / `GetResultsImpl` / `ResizeImpl` / `RefreshImpl` / `WaitIdleImpl` / `SubmitImpl` / `CopyBufferImpl` 等等都同步更名）
- 公共同步结果只有一种：`CommandSubmission`

## 公共类型一览

```csharp
// === 子资源 ===

public record struct TextureSubresource
{
    public uint MipLevel;

    public uint ArrayLayer;
}

public record struct TextureSubresourceRange
{
    public uint BaseMipLevel;

    public uint LevelCount;

    public uint BaseArrayLayer;

    public uint LayerCount;
}

public record struct Offset3D
{
    public int X;

    public int Y;

    public int Z;
}

public record struct Extent3D
{
    public uint Width;

    public uint Height;

    public uint Depth;
}

// === Buffer 子范围 ===

public record struct BufferRange
{
    public Buffer Buffer;

    public ulong OffsetInBytes;

    public ulong SizeInBytes;

    public uint StrideInBytes;

    public static implicit operator BufferRange(Buffer buffer)
    {
        return new()
        {
            Buffer = buffer,
            OffsetInBytes = 0,
            SizeInBytes = buffer.Desc.SizeInBytes,
            StrideInBytes = buffer.Desc.StrideInBytes
        };
    }
}

// === 状态转换 ===

public enum TransitionState
{
    ShaderResource,
    UnorderedAccess
}

// === 渲染附件 ===

public enum LoadAction
{
    Load,
    Clear,
    DontCare
}

public enum StoreAction
{
    Store,
    DontCare,
    Resolve
}

public record struct ColorAttachmentDesc
{
    public Texture Texture;

    public TextureSubresource Subresource;

    public Texture? ResolveTexture;

    public TextureSubresource ResolveSubresource;

    public LoadAction LoadAction;

    public StoreAction StoreAction;

    public Vector4 ClearColor;
}

public record struct DepthStencilAttachmentDesc
{
    public Texture Texture;

    public TextureSubresource Subresource;

    public LoadAction DepthLoadAction;

    public StoreAction DepthStoreAction;

    public LoadAction StencilLoadAction;

    public StoreAction StencilStoreAction;

    public float ClearDepth;

    public byte ClearStencil;
}

// === 视口 / 裁剪 ===

public record struct Viewport
{
    public float X;

    public float Y;

    public float Width;

    public float Height;

    public float MinDepth;

    public float MaxDepth;
}

public record struct Rect
{
    public int X;

    public int Y;

    public uint Width;

    public uint Height;
}

// === 队列同步结果 ===

public readonly struct CommandSubmission(CommandQueue queue, ulong value)
{
    public void Wait()
    {
        if (queue is null || queue.CompletedValueCore >= value)
        {
            return;
        }

        queue.WaitCore(value);
    }
}
```

注：`ShaderStageFlags` 沿用项目现有定义（`None / Vertex / Pixel / Compute / Amplification / Mesh`），本设计不重新声明。

## 公共资源句柄

只有三个：`Buffer`、`Texture`、`Sampler`。

```csharp
public abstract class Texture(GraphicsContext context, TextureDesc desc) : GraphicsResource(context)
{
    private TextureDesc desc = desc;

    public ref readonly TextureDesc Desc => ref desc;

    public static implicit operator TextureSubresourceRange(Texture texture)
    {
        return new()
        {
            BaseMipLevel = 0,
            LevelCount = texture.Desc.MipLevels,
            BaseArrayLayer = 0,
            LayerCount = texture.Desc.ArrayLayers
        };
    }
}
```

`Buffer` / `Sampler` 同样以 `public ref readonly XxxDesc Desc => ref desc;` 暴露描述。

**移除**：`BufferView` / `BufferViewDesc` / `TextureView` / `TextureViewDesc` / `IBindableResource` / `TextureSlice` / `TextureAspect` / `TextureSubresourceLayers` / `BufferViewType` / `FrameBuffer` 系列 / `RenderPassDesc` / `ClearValue` 系列。

**view 缓存**：每个后端 `Texture` / `Buffer` 内部维护 `Dictionary<TextureSubresourceRange, T_view>` / `Dictionary<BufferRange, T_view>`，按 range 值类型为键，懒加载，与父资源同生命周期。同范围多次绑定共用一个后端对象。

## CommandQueue / CommandBuffer

```csharp
public abstract class CommandQueue(GraphicsContext context, CommandQueueType type) : GraphicsResource(context)
{
    private readonly Lock @lock = new();
    private readonly Queue<CommandBuffer> available = [];
    private readonly Queue<InFlightCommandBuffer> execution = [];

    private ulong nextValue = 1;
    private ulong lastSignaledValue;

    public CommandQueueType Type { get; } = type;

    /// <summary>从池中借出一个已 Begin 的命令缓冲。</summary>
    public CommandBuffer CommandBuffer()
    {
        using Lock.Scope _ = @lock.EnterScope();

        CollectCompleted();

        CommandBuffer commandBuffer = available.Count is 0 ? CreateCommandBufferCore() : available.Dequeue();

        commandBuffer.Begin();

        return commandBuffer;
    }

    /// <summary>等待此 queue 上所有已提交的命令完成。</summary>
    public void WaitForIdle()
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (lastSignaledValue is not 0)
        {
            WaitCore(lastSignaledValue);
        }

        CollectCompleted();
    }

    internal CommandSubmission Submit(CommandBuffer commandBuffer, ReadOnlySpan<CommandSubmission> waits)
    {
        using Lock.Scope _ = @lock.EnterScope();

        CollectCompleted();

        commandBuffer.End();

        ulong value = nextValue++;

        SubmitCore(commandBuffer, waits, value);

        lastSignaledValue = value;

        execution.Enqueue(new(commandBuffer, value));

        return new(this, value);
    }

    private void CollectCompleted()
    {
        ulong completed = CompletedValueCore;

        while (execution.Count > 0 && execution.Peek().Value <= completed)
        {
            CommandBuffer cmd = execution.Dequeue().CommandBuffer;

            cmd.Reset();

            available.Enqueue(cmd);
        }
    }

    /// <summary>当前已完成的时间线值。同程序集内（如 <see cref="CommandSubmission"/>）可直接访问，子类必须实现。</summary>
    protected internal abstract ulong CompletedValueCore { get; }

    protected abstract CommandBuffer CreateCommandBufferCore();

    protected abstract void SubmitCore(CommandBuffer commandBuffer, ReadOnlySpan<CommandSubmission> waits, ulong signalValue);

    /// <summary>等待此 queue 时间线达到给定值。同程序集内（如 <see cref="CommandSubmission"/>）可直接访问，子类必须实现。</summary>
    protected internal abstract void WaitCore(ulong value);

    private readonly record struct InFlightCommandBuffer(CommandBuffer CommandBuffer, ulong Value);
}

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    public CommandSubmission Submit(params ReadOnlySpan<CommandSubmission> waits)
    {
        return queue.Submit(this, waits);
    }
}
```

要点：
- `CommandQueue` 不再公开 `Wait(ulong)`。`CommandSubmission.Wait()` 通过 `protected internal` 直接调用 `CompletedValueCore` 与 `WaitCore`，省掉一层包装方法
- `*Core` 后缀替代旧的 `*Impl`，覆盖项目内所有抽象覆写点
- 后端时间线：DX12 `ID3D12Fence` / Vulkan timeline `VkSemaphore`（核心于 1.2，VK 1.3 标配） / Metal `MTLSharedEvent`。Vulkan 二进制 `VkFence` 仅在 swapchain 内部使用

## 资源状态转换

```csharp
public void Transition(Buffer buffer, TransitionState newState);

public void Transition(Texture texture, TransitionState newState);

public void MemoryBarrier();
```

- 没有子范围 transition 重载（RDG 用例，公共 API 不暴露；后端内部仍提供给 RDG 直调）
- `MemoryBarrier()` 用于跨资源、跨阶段的全局内存同步；CommandBuffer 同时维护"上一次 dispatch 写过的 UAV 集合"，下一次 dispatch 若读到同一资源会自动插入 barrier 作为 fallback
- 渲染目标 / 深度 / copy / 顶点 / 索引 / CBV / indirect / present 状态都由对应操作隐式转换
- 用户只在 `ShaderResource` ↔ `UnorderedAccess` 之间显式转换

每个 `Texture` / `Buffer` 跟踪当前 `TransitionState`，初始化时由后端默认设置，第一次显式 `Transition` 时根据当前值生成 from→to barrier。

## ResourceTable

```csharp
public abstract class ResourceTable
{
    public void Write(uint binding, BufferRange range);

    public void Write(uint binding, TextureSubresourceRange range);

    public void Write(uint binding, Sampler sampler);

    public void Write(uint binding, ReadOnlySpan<BufferRange> ranges);

    public void Write(uint binding, ReadOnlySpan<TextureSubresourceRange> ranges);

    public void Write(uint binding, ReadOnlySpan<Sampler> samplers);
}
```

- `Buffer` / `Texture` 的隐式转换让单资源绑定一行代码
- 没有 `BufferViewType` 参数，buffer 解释方式由对应槽位的 `ResourceLayout` 决定
- 没有 `IBindableResource` 标记接口

## CommandBuffer 操作

```csharp
// 顶点 / 索引
public void SetVertexBuffer(uint slot, Buffer buffer, ulong offsetInBytes);

public void SetVertexBuffers(uint firstSlot, ReadOnlySpan<Buffer> buffers, ReadOnlySpan<ulong> offsetsInBytes);

public void SetIndexBuffer(Buffer buffer, ulong offsetInBytes, IndexFormat format);

// Buffer 拷贝 / 上传
public void CopyBuffer(Buffer source, ulong sourceOffsetInBytes,
                       Buffer destination, ulong destinationOffsetInBytes,
                       ulong sizeInBytes);

public void Upload<T>(Buffer destination, ulong offsetInBytes, ReadOnlySpan<T> data) where T : unmanaged;

// Texture 拷贝 / 上传 / Resolve
public void CopyTexture(Texture source, TextureSubresourceRange sourceRange, Offset3D sourceOrigin,
                        Texture destination, TextureSubresourceRange destinationRange, Offset3D destinationOrigin,
                        Extent3D extent);

public void CopyBufferToTexture(Buffer source, ulong sourceOffsetInBytes, uint sourceRowPitchInBytes, uint sourceImageHeight,
                                Texture destination, TextureSubresourceRange destinationRange, Offset3D destinationOrigin,
                                Extent3D extent);

public void CopyTextureToBuffer(Texture source, TextureSubresourceRange sourceRange, Offset3D sourceOrigin, Extent3D extent,
                                Buffer destination, ulong destinationOffsetInBytes, uint destinationRowPitchInBytes, uint destinationImageHeight);

public void ResolveTexture(Texture source, TextureSubresource sourceSubresource,
                           Texture destination, TextureSubresource destinationSubresource);

public void Upload<T>(Texture destination, TextureSubresourceRange range, Offset3D origin, Extent3D extent, ReadOnlySpan<T> data) where T : unmanaged;

// 状态转换
public void Transition(Buffer buffer, TransitionState newState);

public void Transition(Texture texture, TransitionState newState);

public void MemoryBarrier();

// 资源绑定（按 stage 推送）
public void PushResourceTable(ShaderStageFlags stages, ResourceTable table);

// 渲染 pass
public void BeginRenderPass(ReadOnlySpan<ColorAttachmentDesc> colorAttachments,
                            DepthStencilAttachmentDesc? depthStencilAttachment);

public void EndRenderPass();

// 视口 / 裁剪（复数 + span）
public void SetViewports(ReadOnlySpan<Viewport> viewports);

public void SetScissors(ReadOnlySpan<Rect> scissors);

// 绘制 / 调度
public void Draw(...);

public void DrawIndexed(...);

public void Dispatch(uint groupsX, uint groupsY, uint groupsZ);
```

要点：
- 全部参数无 `in`
- 字节相关参数统一 `*InBytes` 后缀
- copy / upload 用 `TextureSubresourceRange`（拷贝时校验 `LevelCount == 1`），不再有 `TextureSubresourceLayers`
- `Aspect` 字段消失，aspect 由 `Texture.Format` 推断
- `BeginRenderPass` 直接接收 attachment span，没有 `RenderPassDesc` 类型；无深度时显式传 `null`
- `PushResourceTable` 接受 `ShaderStageFlags`，不再有 set 索引；后端按 stage 与资源 layout 直接绑定

### `PushResourceTable` 的设计取舍

- `SetResourceTable(uint set, ResourceTable)` 假设有 set 概念，仅 Vulkan 描述符集天然契合，DX12 / Metal 均需要做翻译层
- 借鉴 Metal 的 `setVertexBuffer:atIndex:` / `setFragmentBuffer:atIndex:` 风格：调用方告诉 RHI"把这张表绑给这些 stage"
- DX12：根据 stages 选择对应 root parameter（VS/PS/CS visibility）
- Vulkan：layout 编译期固定，按 stage 走 push descriptor 或独立 set
- Metal：对应 stage 的 argument table

## SwapChain

```csharp
public abstract class SwapChain(GraphicsContext context, SwapChainDesc desc) : GraphicsResource(context)
{
    private SwapChainDesc desc = desc;

    public ref readonly SwapChainDesc Desc => ref desc;

    public abstract uint Width { get; }

    public abstract uint Height { get; }

    /// <summary>当前 backbuffer 索引。仅供后端与同程序集内部使用。</summary>
    internal abstract uint CurrentImageIndex { get; }

    public abstract Texture CurrentColorTarget { get; }

    public abstract Texture? CurrentDepthStencilTarget { get; }

    /// <summary>
    /// 提交 present，等待 <paramref name="waits"/> 完成；返回的 submission 表示下一张 backbuffer 已就绪。
    /// 首帧没有上一帧 present 可等待时，外部应自行准备首张就绪信号
    /// （典型做法：传 <c>default(CommandSubmission)</c>，由后端识别为"无需等待"）。
    /// </summary>
    public abstract CommandSubmission Present(params ReadOnlySpan<CommandSubmission> waits);

    public void Resize(uint width, uint height);

    public void Refresh(Surface surface);
}
```

设计要点：
- `Acquire` 完全消失。SwapChain 内部在 `Present` 时同步前进 backbuffer 索引并准备下一张图像的就绪信号
- `Present` 返回 `CommandSubmission`，与 `CommandBuffer.Submit` 形态完全一致
- 不提供首帧便利字段；首帧的"零号 submission"由用户自行准备
- backbuffer 直接是 `Texture`，无 view 类型
- `CurrentImageIndex` 不公开，仅后端跨文件协作时使用

后端映射：

| 后端 | `Present` | 下一帧就绪信号 |
|---|---|---|
| DX12 | `IDXGISwapChain::Present`，`waits` 转为 queue Wait | 内部用 frame latency event 转成 `CommandSubmission` |
| Vulkan 1.3 | `vkQueuePresentKHR` 等待 `renderFinished[N]` 二进制 semaphore | `vkAcquireNextImageKHR` 在 Present 调用末尾执行，结果包装成时间线对齐的 `CommandSubmission` |
| Metal 4 | `drawable.Present()` 后等下一个 drawable | 下一帧 drawable 的可用事件 |

## 帧循环

```csharp
// 用户决定首帧 image-ready 的来源；最简单的方式是 default。
CommandSubmission imageReady = default;

while (running)
{
    CommandBuffer cmd = ctx.Graphics.CommandBuffer();

    ReadOnlySpan<ColorAttachmentDesc> colors =
    [
        new()
        {
            Texture = swapChain.CurrentColorTarget,
            Subresource = new() { MipLevel = 0, ArrayLayer = 0 },
            LoadAction = LoadAction.Clear,
            StoreAction = StoreAction.Store,
            ClearColor = new(0, 0, 0, 1)
        }
    ];

    DepthStencilAttachmentDesc? depth = swapChain.CurrentDepthStencilTarget is { } ds
        ? new()
        {
            Texture = ds,
            Subresource = new(),
            DepthLoadAction = LoadAction.Clear,
            DepthStoreAction = StoreAction.DontCare,
            ClearDepth = 1.0f
        }
        : null;

    cmd.BeginRenderPass(colors, depth);

    Span<Viewport> viewports = [new() { X = 0, Y = 0, Width = swapChain.Width, Height = swapChain.Height, MinDepth = 0, MaxDepth = 1 }];
    Span<Rect>     scissors  = [new() { X = 0, Y = 0, Width = swapChain.Width, Height = swapChain.Height }];

    cmd.SetViewports(viewports);
    cmd.SetScissors(scissors);
    // draws
    cmd.EndRenderPass();

    CommandSubmission frame = cmd.Submit(imageReady);

    imageReady = swapChain.Present(frame);
}
```

形态：每一步都是"接收一组 `CommandSubmission`，返回一个 `CommandSubmission`"。

## RDG 兼容性

虽然简化版砍掉了子范围 transition，未来 RDG 层可以：
- 在 RDG 自身的命令缓冲适配器内调用后端私有方法 `TransitionRangeCore`，跳过公共 API
- 用自己的 hazard 跟踪覆盖隐式 UAV barrier；显式 `MemoryBarrier()` 仍可被 RDG 复用
- 复用 `CommandSubmission` 处理跨队列依赖
- 复用 `record struct` 子资源类型作为 hash key
