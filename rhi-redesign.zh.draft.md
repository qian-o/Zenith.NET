# RHI 重设计草案（中文临时版）

> 仅用于本轮设计讨论，不进入提交。最终英文版同步在 `rhi-redesign.draft.md`。

## 1. 目标与非目标

**目标**

- 公共 API 表面小、调用形态统一，与 DirectX 12 / Vulkan 1.4 / Metal 4 命令模型 1:1 对应
- 帧循环代码短，热路径零托管分配
- 跨队列同步（Graphics / Compute / Copy）一等公民
- 显式资源状态转换，作为未来 RDG 的稳定基座
- 内联 RenderPass，去掉长生命周期 `FrameBuffer`
- 子范围 / 子资源信息在**调用现场**以值类型表达，不再有公共 `View` 对象

**非目标**

- RHI 内不做完整 hazard tracking（仅缓存"当前状态"用于计算显式 transition 的 from 端）
- 一次 submit 一个同步对象
- 把 `WaitForIdle` 当作主同步手段
- 本轮不实现 RDG、bindless、descriptor buffer
- 不重构 `ResourceTable` / `ResourceLayout` / `Sampler` / `Pipeline` / shader IO 之外的部分
- 不在公共面暴露 per-aspect (color/depth/stencil) 寻址、子范围 transition、首帧 image-ready 等待

## 2. 命名与代码风格约定

- 所有公共值类型：`record struct`，公共字段可写
- 多元素入参一律 `ReadOnlySpan<T>` / `params ReadOnlySpan<T>`，不使用 `T[]` / `IEnumerable<T>`
- 方法体一律 `{ ... }`；**例外**：`ref` / `ref readonly` 返回的属性保留 `=> ref _field;`
- 字节单位字段与参数加 `*InBytes` 后缀
- 抽象后端钩子命名：**只有**与同名非 `Core` 包装共存时才用 `*Core` 后缀（如 `Wait` / `WaitCore`、`Submit` / `SubmitCore`）；其余一律用自然名
- 抽象成员一律 `protected abstract`，同程序集内部调用走 `internal` 包装
- 公共同步结果只有一种：`CommandSubmission`

## 3. 公共类型一览

```csharp
// === 子资源 ===

public record struct TextureSubresource
{
    public uint MipLevel;

    public uint ArrayLayer;
}

public record struct TextureSubresourceLayers
{
    public uint MipLevel;

    public uint BaseArrayLayer;

    public uint LayerCount;
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
    public uint X;

    public uint Y;

    public uint Z;
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

// === 资源状态（公共面） ===

public enum TransitionState
{
    ShaderResource,
    UnorderedAccess
}

// === RenderPass 附件 ===

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

public record struct ColorAttachment
{
    public Texture Texture;

    public TextureSubresource Subresource;

    public Texture? ResolveTexture;

    public TextureSubresource ResolveSubresource;

    public LoadAction LoadAction;

    public StoreAction StoreAction;

    public Vector4 ClearColor;
}

public record struct DepthStencilAttachment
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

// === 队列同步结果 ===

/// <summary>
/// 一次 Submit / Present 产生的一个时间线点。
/// <c>default</c> 是合法的"空"值，后端会从 <c>waits</c> 中过滤掉；其上 <see cref="Wait"/> 为空操作。
/// </summary>
public readonly struct CommandSubmission(CommandQueue? queue, ulong value)
{
    public CommandQueue? Queue = queue;

    public ulong Value = value;

    public void Wait()
    {
        Queue?.Wait(Value);
    }
}
```

子资源三件套对照：

| 类型 | 表达 | 用途 | 后端对照 |
|---|---|---|---|
| `TextureSubresource` | 单 mip × 单 layer | RTV / DSV / Resolve | DX12 plane+mip+slice 单点；VK `aspect+mip+layer` 单点；Metal `level+slice` |
| `TextureSubresourceLayers` | 单 mip × 连续 layer 段 | Copy / Upload | `VkImageSubresourceLayers`；DX12 按 layer 循环 `CopyTextureRegion`；Metal `blitEncoder` 逐层 |
| `TextureSubresourceRange` | 连续 mip 段 × 连续 layer 段 | View / Transition | `VkImageSubresourceRange`；DX12 view desc 的 base+count；Metal `MTLTextureView` |

公共面**不**暴露 aspect 字段；后端从 `Texture.Format` 推断 `VkImageAspectFlags` / DX12 plane index。Cube 面以 `ArrayLayer = cubeIndex * 6 + face` 表达，与 VK / DX12 / Metal 一致，无独立 face 轴。

## 4. 资源句柄

`Buffer` / `Texture` / `Sampler` 不再实现 `IBindableResource`（接口删除）：

```csharp
public abstract class Buffer(GraphicsContext context, BufferDesc desc) : GraphicsResource(context)
{
    private BufferDesc desc = desc;

    public ref readonly BufferDesc Desc => ref desc;

    // Map / Unmap / Upload 保留
}

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

public abstract class Sampler(GraphicsContext context, SamplerDesc desc) : GraphicsResource(context)
{
    private SamplerDesc desc = desc;

    public ref readonly SamplerDesc Desc => ref desc;
}
```

**View 缓存**（每个后端 `Texture` / `Buffer` 内部）：

- `Dictionary<TextureSubresourceRange, T_view>` —— SRV / UAV
- `Dictionary<TextureSubresource, T_rtv_dsv>` —— RTV / DSV
- `Dictionary<BufferRange, T_view>` —— typed buffer view

按值类型 key 懒加载，与父资源同生命周期；同一 (resource, range) 组合在所有调用方共享一个后端对象。

**资源状态跟踪**：每个 `Texture` / `Buffer` 内部跟踪当前 `TransitionState`，初值由后端默认写入；首次显式 `Transition` 时根据当前值生成 from→to barrier。

**确认从框架删除（这些当前真实存在）**：`BufferView` / `BufferViewDesc` / `TextureView` / `TextureViewDesc` / `BufferViewType` / `IBindableResource` / `TextureSlice`（含 `Face`）/ `TextureOffset` / `TextureExtent` / `FrameBuffer` / `FrameBufferDesc` / `FrameBufferAttachment` / `ClearValue` / `ClearValues` 静态类 / `ClearFlags` 枚举 / `GraphicsContext.CreateBufferView` / `CreateTextureView` / `CreateFrameBuffer`。`Output` 类型保留并继续作为 `GraphicsPipelineDesc.Output`，仅 `FrameBuffer.Output` 一处使用消失。

## 5. CommandQueue / CommandBuffer

每个 `CommandQueue` 持有一条单调递增的完成时间线；一次 `Submit` 在时间线上推进一个值，并以 `CommandSubmission(queue, value)` 公开该点。跨队列依赖通过把上游 `CommandSubmission` 传入下游 `Submit(waits)` 表达。

```csharp
public abstract class CommandQueue(GraphicsContext context, CommandQueueType type) : GraphicsResource(context)
{
    private readonly Lock @lock = new();
    private readonly Queue<CommandBuffer> available = [];
    private readonly Queue<InFlightCommandBuffer> execution = [];

    private ulong lastSignaledValue;

    public CommandQueueType Type { get; } = type;

    public CommandBuffer CommandBuffer()
    {
        using Lock.Scope _ = @lock.EnterScope();

        CollectCompleted();

        CommandBuffer commandBuffer = available.Count is 0 ? CreateCommandBuffer() : available.Dequeue();

        commandBuffer.Begin();

        return commandBuffer;
    }

    /// <summary>等待此 queue 上所有已提交的命令完成。幂等。</summary>
    public void WaitForIdle()
    {
        Wait(lastSignaledValue);
    }

    internal void Wait(ulong value)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (GetCompletedValue() >= value)
        {
            CollectCompleted();

            return;
        }

        WaitCore(value);

        CollectCompleted();
    }

    internal CommandSubmission Submit(CommandBuffer commandBuffer, ReadOnlySpan<CommandSubmission> waits)
    {
        using Lock.Scope _ = @lock.EnterScope();

        CollectCompleted();

        commandBuffer.End();

        lastSignaledValue++;

        SubmitCore(commandBuffer, waits, lastSignaledValue);

        execution.Enqueue(new(commandBuffer, lastSignaledValue));

        return new(this, lastSignaledValue);
    }

    private void CollectCompleted()
    {
        ulong value = GetCompletedValue();

        while (execution.Count > 0 && execution.Peek().Value <= value)
        {
            CommandBuffer commandBuffer = execution.Dequeue().CommandBuffer;

            commandBuffer.Reset();

            available.Enqueue(commandBuffer);
        }
    }

    protected abstract CommandBuffer CreateCommandBuffer();

    protected abstract ulong GetCompletedValue();

    protected abstract void WaitCore(ulong value);

    protected abstract void SubmitCore(CommandBuffer commandBuffer, ReadOnlySpan<CommandSubmission> waits, ulong signalValue);

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

- `GetCompletedValue()` 是**方法**而非属性：三端后端（DX12 `fence.GetCompletedValue()` / VK `vkGetSemaphoreCounterValue` / Metal `event.SignaledValue`）都是调用语义。
- CommandBuffer 池化由 queue 自管：`CollectCompleted()` 在每次 `Submit` / `Wait` 时根据时间线值回收实例，重置后入 `available` 复用。

### 后端时间线对照

| 后端 | 时间线对象 | API |
|---|---|---|
| DX12 | 每 queue 一个 `ID3D12Fence` | `queue.Signal(fence, value)` / `fence.GetCompletedValue()` / `fence.SetEventOnCompletion` |
| Vulkan 1.4 | 每 queue 一个 timeline `VkSemaphore`（核心特性） | `vkQueueSubmit2` 的 `pSignalSemaphoreInfos[].value` / `vkGetSemaphoreCounterValue` / `vkWaitSemaphores` |
| Metal 4 | 每 queue 一个 `MTLSharedEvent` | `commandBuffer.EncodeSignalEvent(event, value)` / `event.SignaledValue` / `event.NotifyListener` |

二进制 `VkFence` 仅在 swapchain image acquire 内部使用。

### 与 SwapChain Present 的关系

`Present` 始终在 GraphicsQueue 上执行。`SwapChain.Present(waits)` 内部由 GraphicsQueue 提交 present，跨队列 wait 通过 GraphicsQueue 端的"等待对方时间线"指令落地（详见第 7 节后端映射表）。SwapChain 实现从 `GraphicsContext` 取 GraphicsQueue 引用即可，无需对外暴露。

为简化 barrier，Vulkan 后端所有资源以 concurrent / shared 语义创建（三队列 `VK_SHARING_MODE_CONCURRENT`）；跨队列同步完全通过 `CommandSubmission` waits 表达，用户代码从不写 ownership-transfer barrier。

## 6. CommandBuffer 操作

```csharp
// === 状态 ===

public void Transition(Buffer buffer, TransitionState newState);

public void Transition(Texture texture, TransitionState newState);

public void MemoryBarrier();

// === RenderPass（内联） ===

public void BeginRenderPass(ReadOnlySpan<ColorAttachment> colorAttachments,
                            DepthStencilAttachment? depthStencilAttachment);

public void EndRenderPass();

// === 视口 / 裁剪 ===

public void SetViewports(ReadOnlySpan<Viewport> viewports);

public void SetScissors(ReadOnlySpan<Scissor> scissors);

// === Pipeline / 资源绑定 ===

public void SetPipeline(Pipeline pipeline);

public void PushResourceTable(ShaderStageFlags stages, ResourceTable table);

// === 顶点 / 索引 ===

public void SetVertexBuffer(uint slot, Buffer buffer, ulong offsetInBytes);

public void SetVertexBuffers(uint firstSlot, ReadOnlySpan<Buffer> buffers, ReadOnlySpan<ulong> offsetsInBytes);

public void SetIndexBuffer(Buffer buffer, ulong offsetInBytes, IndexFormat format);

// === 绘制 / 调度 ===

public void Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance);

public void DrawIndexed(uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance);

public void DrawIndirect(Buffer argsBuffer, ulong offsetInBytes, uint drawCount, uint strideInBytes);

public void DrawIndexedIndirect(Buffer argsBuffer, ulong offsetInBytes, uint drawCount, uint strideInBytes);

public void Dispatch(uint groupCountX, uint groupCountY, uint groupCountZ);

public void DispatchIndirect(Buffer argsBuffer, ulong offsetInBytes);

// === Buffer 拷贝 / 上传 ===

public void CopyBuffer(Buffer source, ulong sourceOffsetInBytes,
                       Buffer destination, ulong destinationOffsetInBytes,
                       ulong sizeInBytes);

public void Upload<T>(Buffer destination, ulong offsetInBytes, ReadOnlySpan<T> data) where T : unmanaged;

// === Texture 拷贝 / 上传 / Resolve ===

public void CopyTexture(Texture source, TextureSubresourceLayers sourceLayers, Offset3D sourceOrigin,
                        Texture destination, TextureSubresourceLayers destinationLayers, Offset3D destinationOrigin,
                        Extent3D extent);

public void CopyBufferToTexture(Buffer source, ulong sourceOffsetInBytes, uint sourceRowPitchInBytes, uint sourceImageHeight,
                                Texture destination, TextureSubresourceLayers destinationLayers, Offset3D destinationOrigin,
                                Extent3D extent);

public void CopyTextureToBuffer(Texture source, TextureSubresourceLayers sourceLayers, Offset3D sourceOrigin, Extent3D extent,
                                Buffer destination, ulong destinationOffsetInBytes, uint destinationRowPitchInBytes, uint destinationImageHeight);

public void ResolveTexture(Texture source, TextureSubresource sourceSubresource,
                           Texture destination, TextureSubresource destinationSubresource);

public void Upload<T>(Texture destination, TextureSubresourceLayers layers, Offset3D origin, Extent3D extent, ReadOnlySpan<T> data) where T : unmanaged;
```

要点：

- `BeginRenderPass` 接受 attachment span，无深度时显式传 `null`；内部按 attachment 自动 `Transition` + 自动填 `SetViewports` / `SetScissors`，调用方可在其后再调一次 `SetViewports` / `SetScissors` 覆盖。
- `PushResourceTable` 加 `ShaderStageFlags stages`：DX12 root parameter visibility / Metal argument table 都按 stage 区分；VK 的 set 在 layout 里编译期固定，stages 用于校验匹配。
- `Transition` 只在 `ShaderResource` ↔ `UnorderedAccess` 之间显式调用；render target / 深度模板 / copy / 顶点 / 索引 / CBV / indirect / present 状态由对应操作隐式转换。
- `MemoryBarrier()`：跨资源 / 跨阶段全局内存屏障，由调用方自行决定何时发出。

### ResourceTable

`Write` 强类型重载，取代当前 `Write(uint, params IBindableResource[])` + 运行时 type-switch：

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

- `Buffer` / `Texture` 经隐式转换让单资源绑定保持一行。
- buffer 解释（CBV / SRV-structured / SRV-byteaddress / UAV / typed）由槽位的 `ResourceLayout` 决定，公共面无 `BufferViewType`。
- 校验：UAV 槽位要求 `BufferUsageFlags.UnorderedAccess` / `TextureUsageFlags.UnorderedAccess`，shape 与 layout 不匹配在 `Write` 时报错。

## 7. SwapChain

```csharp
public abstract class SwapChain(GraphicsContext context, SwapChainDesc desc) : GraphicsResource(context)
{
    private SwapChainDesc desc = desc;

    public ref readonly SwapChainDesc Desc => ref desc;

    public abstract uint Width { get; }

    public abstract uint Height { get; }

    public abstract Texture CurrentColorTarget { get; }

    public abstract Texture? CurrentDepthStencilTarget { get; }

    /// <summary>
    /// 在 GraphicsQueue 上提交 present，等待 <paramref name="waits"/>；
    /// 返回的 <see cref="CommandSubmission"/> 表示下一张 backbuffer 可写。
    /// </summary>
    public abstract CommandSubmission Present(params ReadOnlySpan<CommandSubmission> waits);

    public void Resize(uint width, uint height);

    public void Refresh(Surface surface);
}
```

设计要点：

- backbuffer 以普通 `Texture` 公开（颜色 + 可选深度模板）；不公开 `FrameBuffer`，不公开 image index。
- `Present(...)` 返回 `CommandSubmission`，与 `CommandBuffer.Submit(...)` 形态完全一致；接收任意队列的 wait。
- 当前 image index 在后端内部维护（`VKSwapChain.ImageIndex` / `DXSwapChain.BufferIndex`），公共抽象类不感知。

后端映射：

| 后端 | `Present(waits)` 实现 | "下一帧就绪" 信号 |
|---|---|---|
| DX12 | 对每个 wait 调 `graphicsQueue.Wait(otherFence, otherValue)` → `IDXGISwapChain3::Present` | frame latency event 包装为 `CommandSubmission`（值取自 graphics queue 时间线） |
| Vulkan 1.4 | 一次桥接 submit `vkQueueSubmit2(graphicsQueue, waits=timeline values, signal=renderFinished[N])` → `vkQueuePresentKHR(graphicsQueue, wait=renderFinished[N])` | 下一帧 `vkAcquireNextImageKHR` 返回的 binary `imageAvailable[N]`，再用一次 graphics queue 的 "wait binary, signal timeline" 桥接 submit 包装为 `CommandSubmission` |
| Metal 4 | `commandBuffer.EncodeWaitForEvent(...)` 转译每个 wait → `commandBuffer.Present(drawable)` → `commandBuffer.Commit()` | 下一个 drawable 的可用事件包装为 `CommandSubmission` |

## 8. 帧循环

```csharp
CommandSubmission imageReady = default;

while (running)
{
    CommandBuffer cmd = ctx.Graphics.CommandBuffer();

    ReadOnlySpan<ColorAttachment> colors =
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

    DepthStencilAttachment? depth = swapChain.CurrentDepthStencilTarget is { } ds
        ? new()
        {
            Texture = ds,
            Subresource = new(),
            DepthLoadAction = LoadAction.Clear,
            DepthStoreAction = StoreAction.DontCare,
            ClearDepth = 1.0f
        }
        : null;

    // viewport / scissor 已按 attachment 自动填充；如需自定义可后续覆盖
    cmd.BeginRenderPass(colors, depth);
    // draws
    cmd.EndRenderPass();

    CommandSubmission frame = cmd.Submit(imageReady);

    imageReady = swapChain.Present(frame);
}
```

每一步形态一致：拿一组 `CommandSubmission` waits，返回一个 `CommandSubmission`。`Submit` 与 `Present` 在 API 表面完全对称。

## 9. 三端 RenderPass / 状态映射

| 概念 | DX12 | Vulkan 1.4 | Metal 4 |
|---|---|---|---|
| Begin / End RenderPass | `OMSetRenderTargets` 或 `BeginRenderPass(RENDER_PASS_RENDER_TARGET_DESC)` / `EndRenderPass` | `vkCmdBeginRendering(VkRenderingInfo)` / `vkCmdEndRendering` | `commandBuffer.RenderCommandEncoder(MTLRenderPassDescriptor)` / `endEncoding` |
| LoadAction | `BeginningAccessType` (Discard / Preserve / Clear) | `loadOp` (DONT_CARE / LOAD / CLEAR) | `MTLLoadAction` |
| StoreAction | `EndingAccessType` (Discard / Preserve / Resolve) | `storeOp` (DONT_CARE / STORE / NONE) + `resolveMode` | `MTLStoreAction` |
| Resolve | `EndingAccessResolveSubresourceParameters` | `pResolveAttachments` + `resolveMode` | `MTLRenderPassDescriptor.resolveTexture` |
| 显式 Transition | `ResourceBarrier(Transition / UAV)` | `vkCmdPipelineBarrier2(VkImageMemoryBarrier2 / VkBufferMemoryBarrier2)` | `commandEncoder.MemoryBarrier(scope, after, before)` |
| Aspect 推断源 | format → plane index | `Texture.Format` → `VkImageAspectFlags` | format → 自动 |

## 10. RDG 接口自检

RDG 由 RHI 用户自行封装，本节只回答：「本 RHI 是否暴露了 RDG 所需的所有原语？」

| RDG 需求 | 本 RHI 是否提供 | 对应入口 |
|---|---|---|
| 显式资源状态转换 | ✅ | `CommandBuffer.Transition(Buffer/Texture, TransitionState)` |
| 全局内存屏障 | ✅ | `CommandBuffer.MemoryBarrier()` |
| 子范围寻址（用作 alias / hazard key） | ✅ | `BufferRange` / `TextureSubresourceRange`（`record struct`，可哈希） |
| 跨队列依赖 | ✅ | `CommandSubmission` waits（`Submit` / `Present` 同形态） |
| 队列完成查询 / 等待 | ✅ | `CommandQueue.WaitForIdle()` / `CommandSubmission.Wait()` |
| 短生命周期 RenderPass | ✅ | 内联 `BeginRenderPass(colorAttachments, depth)` |

结论：RDG 实现方可在不修改 RHI 公共面的前提下完成封装。

## 11. 互操作 / 原生句柄

面向 Skia / DLSS / FSR / RenderDoc / 工具链等需要拿后端原生句柄的场景。设计原则：

- 公共面只暴露 `nint` + 一组枚举；不出现任何后端类型 / 平台条件属性
- RHI 不替外部库做事；调用方按既有模板自管 state 协议
- 互操作 API 是 RHI 内部本就维护的状态的副产品，零额外运行时成本

### 11.1 NativeObjectType 枚举

```csharp
public enum NativeObjectType
{
    // DirectX 12
    DxgiFactory,
    DxgiAdapter,
    D3D12Device,
    D3D12CommandQueue,
    D3D12GraphicsCommandList,
    D3D12Resource,
    D3D12CpuDescriptorHandleSampler,
    D3D12CpuDescriptorHandleRtv,
    D3D12CpuDescriptorHandleDsv,
    D3D12CpuDescriptorHandleSrv,
    D3D12CpuDescriptorHandleUav,

    // Metal
    MtlDevice,
    Mtl4CommandQueue,
    Mtl4CommandBuffer,
    MtlTexture,
    MtlBuffer,
    MtlSamplerState,

    // Vulkan
    VkInstance,
    VkPhysicalDevice,
    VkDevice,
    VkQueue,
    VkQueueFamilyIndex,
    VkCommandBuffer,
    VkImage,
    VkImageView,
    VkBuffer,
    VkSampler
}
```

命名沿用各原生 API 既有前缀：DX12 体系按 `Dxgi` / `D3D12` 区分；Metal 4 与旧 Metal 共存时用 `Mtl` / `Mtl4` 区分；Vulkan 一律 `Vk`。`D3D12CpuDescriptorHandle*` 按 view 类型拆开，避免单入口语义模糊。

非句柄的标量信息（如 `VkQueueFamilyIndex`）也走同一入口，以 `nint` 承载 `uint`，公共面不挂任何平台条件属性。

### 11.2 INativeObject 接口

```csharp
public interface INativeObject
{
    /// <summary>枚举不匹配本对象 / 当前后端时返回 0。</summary>
    nint GetNativeObject(NativeObjectType type);
}

public abstract class GraphicsContext : INativeObject
{
    public abstract nint GetNativeObject(NativeObjectType type);
}

public abstract class GraphicsResource(GraphicsContext context) : DisposableObject, INativeObject
{
    public GraphicsContext Context { get; } = context;

    public abstract nint GetNativeObject(NativeObjectType type);
}
```

所有 `GraphicsResource` 子类（`Buffer` / `Texture` / `Sampler` / `CommandQueue` / `CommandBuffer` / `SwapChain` / `Pipeline` / `Shader` / `ResourceTable` / `ResourceLayout`）一律实现该接口；后端按 `switch` 实现，不感兴趣的子类返回 0。第三方代码可走接口编程，不必区分 context / resource。

后端实现示例：

```csharp
// VKTexture
public override nint GetNativeObject(NativeObjectType type) => type switch
{
    NativeObjectType.VkImage     => (nint)Image.Handle,
    NativeObjectType.VkImageView => (nint)View.Handle,
    _ => 0
};

// DXBuffer
public override nint GetNativeObject(NativeObjectType type) => type switch
{
    NativeObjectType.D3D12Resource => (nint)Resource.Handle,
    _ => 0
};

// 暂不暴露的子类
public override nint GetNativeObject(NativeObjectType type) => 0;
```

### 11.3 CommandBuffer 互操作动词

```csharp
public abstract class CommandBuffer
{
    /// <summary>
    /// 进入互操作段：结束当前 RenderPass / Encoder（如有），清空 RHI 内部绑定缓存
    /// （PSO / 描述符 / 顶点索引 / viewport / scissor）。
    /// 不向 cmd 写入命令（VK 例外：会调一次 vkCmdEndRendering）。
    /// 进入后请通过 GetNativeObject(...) 拿原生句柄交给外部库。
    /// Begin / End 必须成对调用，禁止嵌套。
    /// </summary>
    public abstract void BeginExternalCommands();

    /// <summary>
    /// 结束互操作段：插入一个全局 MemoryBarrier 以隔离外部库的写入与后续 RHI 命令。
    /// 不重新打开 RenderPass / Encoder；调用方按需 BeginRenderPass / SetPipeline。
    /// </summary>
    public abstract void EndExternalCommands();

    /// <summary>
    /// 同步 RHI 资源状态缓存为 newState，不写 cmd、不发 barrier。
    /// 下一次 Transition 据此算 from→to。允许在 External scope 内或外调用。
    /// </summary>
    public abstract void SetState(Texture texture, TransitionState newState);

    public abstract void SetState(Buffer buffer, TransitionState newState);
}
```

后端实现要点：

- `BeginExternalCommands`：
    - 公共部分：清 `cachedPipeline` / `cachedRootSignature`(DX12) / `cachedDescriptorSets/Heaps` / `cachedVertexBuffers` / `cachedIndexBuffer` / `cachedViewports` / `cachedScissors`
    - VK：若当前在 dynamic rendering scope，调一次 `vkCmdEndRendering`
    - DX12 / Metal：仅清字段，不写 cmd（DX12 不调 `ID3D12GraphicsCommandList::ClearState`，那会真往 cmd 写命令）
    - 置内部 `inExternalScope = true`；若已为 true → 抛异常
- `EndExternalCommands`：
    - 调用一次内部 `MemoryBarrier()`（VK = `vkCmdPipelineBarrier2(ALL → ALL, MEMORY_READ|WRITE → MEMORY_READ|WRITE)`；DX12 = `ResourceBarrier(UAV, nullptr)` 或 `D3D12_GLOBAL_BARRIER`；Metal = `MemoryBarrier(scope=AllResources, after=AllStages, before=AllStages)`）
    - 置 `inExternalScope = false`；若已为 false → 抛异常
    - 不重开 RenderPass / Encoder
- `SetState`：直接写 `texture.CurrentState = newState` / `buffer.CurrentState = newState`，零写 cmd。

### 11.4 调用方模板

```csharp
// === DLSS (DX12) — 共享 cmd，state 不变 ===
cmd.Transition(colorIn,  TransitionState.ShaderResource);
cmd.Transition(colorOut, TransitionState.UnorderedAccess);

cmd.BeginExternalCommands();
DLSS.Evaluate(
    cmd.GetNativeObject(NativeObjectType.D3D12GraphicsCommandList),
    colorIn.GetNativeObject(NativeObjectType.D3D12Resource),
    colorOut.GetNativeObject(NativeObjectType.D3D12Resource));
cmd.EndExternalCommands();   // 自动插入 barrier，隔离 DLSS 写入与后续 RHI 命令

cmd.BeginRenderPass(...);

// === FSR/FFX (Vulkan) — 共享 cmd，state 由外部库改 ===
cmd.Transition(colorIn,  TransitionState.ShaderResource);
cmd.Transition(colorOut, TransitionState.UnorderedAccess);

cmd.BeginExternalCommands();
Ffx.Fsr2Dispatch(
    cmd.GetNativeObject(NativeObjectType.VkCommandBuffer),
    colorIn.GetNativeObject(NativeObjectType.VkImage),
    colorOut.GetNativeObject(NativeObjectType.VkImage));
cmd.SetState(colorOut, TransitionState.ShaderResource);
cmd.EndExternalCommands();

cmd.BeginRenderPass(...);

// === Skia (Vulkan) — 不共享 cmd，自带提交 ===
nint instance = ctx.GetNativeObject(NativeObjectType.VkInstance);
nint device   = ctx.GetNativeObject(NativeObjectType.VkDevice);
nint queue    = ctx.Graphics.GetNativeObject(NativeObjectType.VkQueue);
uint family   = (uint)ctx.Graphics.GetNativeObject(NativeObjectType.VkQueueFamilyIndex);
var grContext = GRContext.MakeVulkan(instance, device, queue, family, /*...*/);
// Skia 自管 cmd；与 RHI 共享的资源在下次 RHI 使用前对相应 RHI 句柄调 SetState 同步缓存状态即可。

// === RenderDoc — 仅 device 句柄 ===
nint device = ctx.GetNativeObject(NativeObjectType.D3D12Device);
RenderDocApi.StartFrameCapture(device, IntPtr.Zero);
// ... 渲染一帧 ...
RenderDocApi.EndFrameCapture(device, IntPtr.Zero);
```

### 11.5 不做的事

- 不提供「导入外部 native 资源」入口（首版）；如未来需要：通过 `GraphicsContext.ImportTexture(TextureDesc, nint, TransitionState)` 加入，签名同形态
- 调用方自己保证调用外部库时不在 RenderPass 内（典型互操作都是 compute，天然如此）

## 12. ZenithHelper 受影响成员

[ZenithHelper](sources/Zenith.NET/ZenithHelper.cs) 中以下函数依赖被删除的 `TextureSlice`（含 `Face`） / `TextureViewDesc`，或依赖 "cube 面隐式乘进子资源计数" 模型，新设计下不再适用，需删除或以新型重写：

- `FaceCount(TextureDesc)` —— cube / cubeArray 以 6-layer array 表达，`ArrayLayers` 含 face 总层数，face 不再是独立轴。
- `FaceIndex(TextureDesc, TextureSlice)` —— 依赖 `TextureSlice.Face`。
- `FlattenArrayLayerCount(TextureDesc)` —— "把 face 乘进 array layer" 的折叠不再需要，直接 `desc.ArrayLayers`。
- `FlattenArrayLayerIndex(TextureDesc, TextureSlice)` —— 同上。
- `FlattenArrayLayerRange(TextureViewDesc)` —— `TextureViewDesc` 删除。
- `SubresourceCount(TextureDesc)` —— 新模型 = `MipLevels * ArrayLayers`，使用侧可内联。
- `SubresourceIndex(TextureDesc, TextureSlice)` —— 新模型直接 `MipLevel + ArrayLayer * MipLevels`，使用侧内联。
- `SubresourceSizeInBytes(TextureDesc, TextureSlice)` —— 改为 `TextureSubresource` 入参重写（仅需 `MipLevel`，其余复用现有 `MipDimensions` + `SizeInBytes`）。

不受影响：纯格式 / 几何计算 helper（`MipDimensions`、`SizeInBytes(PixelFormat,...)`、`ElementFormat` 字节表等）保持不变。
# RHI 重设计草案（中文临时版）

> 仅用于本轮设计讨论，不进入提交。最终英文版同步在 `rhi-redesign.draft.md`。

