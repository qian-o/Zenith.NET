# RHI 重设计草案（中文临时版）

> 仅用于本轮设计讨论，不进入提交。最终英文版同步在 `rhi-redesign.draft.md`。

## 1. 目标

- 公共 API 表面小、调用形态统一，与 DirectX 12 / Vulkan 1.4 / Metal 4 命令模型 1:1 对应
- 帧循环代码短，热路径零托管分配
- 跨队列同步（Graphics / Compute / Copy）一等公民
- 显式资源状态转换仅限 `ShaderResource` ↔ `UnorderedAccess`，其余转换均由对应操作隐式完成
- 内联 RenderPass，去掉长生命周期 `FrameBuffer`
- 子范围 / 子资源信息在**调用现场**以值类型表达，不再有公共 `View` 对象
- 每个 pipeline 一张 `ResourceTable`（不设次级 descriptor set），对齐 Metal 4 argument-table 模型与另两端的 root/descriptor 模型

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

**View 缓存**（每个后端 `Texture` / `Buffer` 内部）：设计允许后端按 §3 的值类型做 key 懒加载缓存原生 view 对象，但此缓存完全属于后端内部，不在公共面出现。

**资源状态跟踪**：每个 `Texture` / `Buffer` 持有当前 `TransitionState`，初值由后端在创建时写入。首次显式 `Transition` 根据该缓存值生成 from→to barrier。仅 `ShaderResource` ↔ `UnorderedAccess` 向用户可见；render target / 深度模板 / copy / 顶点 / 索引 / CBV / indirect / present 等状态由对应操作隐式转换。

**确认从框架删除（这些当前真实存在）**：`BufferView` / `BufferViewDesc` / `TextureView` / `TextureViewDesc` / `BufferViewType` / `IBindableResource` / `TextureSlice`（含 `Face`）/ `TextureOffset` / `TextureExtent` / `FrameBuffer` / `FrameBufferDesc` / `FrameBufferAttachment` / `ClearValue` / `ClearValues` 静态类 / `ClearFlags` 枚举 / `GraphicsContext.CreateBufferView` / `CreateTextureView` / `CreateFrameBuffer`。`Output` 类型保留并继续作为 `GraphicsPipelineDesc.Output`，仅 `FrameBuffer.Output` 一处使用消失。

## 5. CommandQueue / CommandBuffer

每个 `CommandQueue` 持有一条单调递增的完成时间线；一次 `Submit` 在时间线上推进一个值，并以 `CommandSubmission(queue, value)` 公开该点。跨队列依赖通过把上游 `CommandSubmission` 传入下游 `Submit(waits)` 表达。

```csharp
public abstract class CommandQueue(GraphicsContext context, CommandQueueType type) : GraphicsResource(context)
{
    public CommandQueueType Type { get; } = type;

    /// <summary>获取一个可录制的 CommandBuffer，必要时复用已退役实例。</summary>
    public CommandBuffer CommandBuffer();

    /// <summary>等待此 queue 上所有已提交命令完成。幂等。</summary>
    public void WaitForIdle();

    protected abstract ulong GetCompletedValue();

    protected abstract void WaitCore(ulong value);

    protected abstract void SubmitCore(CommandBuffer commandBuffer, ReadOnlySpan<CommandSubmission> waits, ulong signalValue);

    protected abstract CommandBuffer CreateCommandBuffer();
}

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    /// <summary>结束录制并在其所属 queue 上入队。</summary>
    public CommandSubmission Submit(params ReadOnlySpan<CommandSubmission> waits);
}
```

- `GetCompletedValue()` 是**方法**而非属性：三端后端（DX12 `fence.GetCompletedValue()` / VK `vkGetSemaphoreCounterValue` / Metal `event.SignaledValue`）都是调用语义。
- CommandBuffer 池化完全是 queue 的内部实现细节；公共面只暴露 `CommandBuffer()` 与 `Submit(...)`。
- `GraphicsContext` 暴露三条队列：`Graphics` / `Compute` / `Copy`。`Present` 始终在 `Graphics` 上执行；`SwapChain` 从 context 拿到该队列引用，自身公共面不暴露队列。

### 后端时间线对照

| 后端 | 时间线对象 | API |
|---|---|---|
| DX12 | 每 queue 一个 `ID3D12Fence` | `queue.Signal(fence, value)` / `fence.GetCompletedValue()` / `fence.SetEventOnCompletion` |
| Vulkan 1.4 | 每 queue 一个 timeline `VkSemaphore`（核心特性） | `vkQueueSubmit2` 的 `pSignalSemaphoreInfos[].value` / `vkGetSemaphoreCounterValue` / `vkWaitSemaphores` |
| Metal 4 | 每 queue 一个 `MTLSharedEvent` | `commandBuffer.EncodeSignalEvent(event, value)` / `event.SignaledValue` / `event.NotifyListener` |

三端均原生支持上述 timeline 模型；本表仅用于可行性存档，并非实现规定。

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

public void PushResourceTable(ResourceTable table);

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
- `PushResourceTable` 不接受 stages 参数：stages 信息由 `ResourceTable.Layout` 的每个 binding 自带（DX12 root parameter visibility / Metal argument table 写入位置据此推导；VK 的 set 在 layout 里编译期固定）。每个 pipeline 仅支持一张 table，无 setIndex。
- **Push-snapshot 语义**：`PushResourceTable` 在调用现场把 `table` 的当前内容快照进 cmd buffer；之后对该 `table` 的 `Write` 不影响已 push 的绑定。三端均原生支持此语义（DX12 绑定时拷贝 descriptor、VK `vkCmdPushDescriptorSet`、Metal 4 `setArgumentTable:`），因此同一个 `ResourceTable` 可以在帧内反复 `Write` + `Push`。
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
- `Present(...)` 返回 `CommandSubmission`，与 `CommandBuffer.Submit(...)` 形态对称；接收任意队列的 wait。
- 当前 image index 完全是后端事务，不在公共面出现。

三端均原生支持 `Present(waits) → CommandSubmission` 的形态：DX12 以 `IDXGISwapChain3::Present` 搭配每个 wait 的 `graphicsQueue.Wait(fence, value)`；Vulkan 1.4 用一次桥接 `vkQueueSubmit2`，把 timeline waits 翻译成供 `vkQueuePresentKHR` 消费的 binary `renderFinished` semaphore；Metal 4 为 `commandBuffer.EncodeWaitForEvent(...)` → `commandBuffer.Present(drawable)` → `commandBuffer.Commit()`。

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

## 9. 三端可行性对照

§3–§7 的每个公共原语，三端均有原生对应：

| 概念 | DX12 | Vulkan 1.4 | Metal 4 |
|---|---|---|---|
| 内联 RenderPass | `ID3D12GraphicsCommandList4::BeginRenderPass` / `EndRenderPass` | `vkCmdBeginRendering` / `vkCmdEndRendering` | `MTL4CommandBuffer::MakeRenderCommandEncoder` / `endEncoding` |
| LoadAction | `RenderPassBeginningAccessType` | `VkAttachmentLoadOp` | `MTLLoadAction` |
| StoreAction（含 Resolve） | `RenderPassEndingAccessType` | `VkAttachmentStoreOp` + `pResolveAttachments` + `resolveMode` | `MTLStoreAction` + `resolveTexture` |
| 显式 Transition（SRV ↔ UAV） | `ResourceBarrier(Transition / UAV)` | `vkCmdPipelineBarrier2`（`VkImageMemoryBarrier2` / `VkBufferMemoryBarrier2`） | `MTL4ComputeCommandEncoder.BarrierAfterEncoderStages` |
| 全局 `MemoryBarrier()` | `ResourceBarrier(UAV)` 全局 | `vkCmdPipelineBarrier2` 配 `VK_ACCESS_2_MEMORY_READ/WRITE_BIT` | `BarrierAfterEncoderStages` 覆盖全阶段 |
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
- RHI 不替外部库做事，也不提供任何 escape hatch 让外部代码修改 RHI 的资源状态缓存
- 互操作 API 是 RHI 内部本就维护的状态的副产品，零额外运行时成本

**状态不变契约**：通过 `GetNativeObject(...)` 暴露给外部库的资源，外部库返回后必须将其留在与当前 `TransitionState` 一致的底层状态：

- DX12：`ShaderResource` → `D3D12_RESOURCE_STATE_NON_PIXEL_SHADER_RESOURCE | PIXEL_SHADER_RESOURCE`；`UnorderedAccess` → `D3D12_RESOURCE_STATE_UNORDERED_ACCESS`
- Vulkan：`ShaderResource` → `VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL`；`UnorderedAccess` → `VK_IMAGE_LAYOUT_GENERAL`
- Metal 4：状态由 driver 自管，无需配合

DLSS / XeSS / FFX-FSR / NRD 这类 compute 后处理库本就遵守该契约（入口前后状态一致）。主动修改 layout 的库（典型如 Skia）不适合共享 cmd，应采用：

1. **纹理拷贝**（推荐）：让外部库在其私有资源上渲染，结束后以 `Texture` 包装其输出（未来 import 接口，见 § 11.5）或直接以原生拷贝 API 搬进 RHI 资源。
2. **獨占 swapchain**：纯 UI 应用可将整个 swapchain 让渡给外部库，RHI 不参与该 swapchain 的渲染。
3. **配置外部库还原 layout**：Skia 可通过 `GrBackendTexture::setVkImageLayout` 要求每次 flush 后还原到 RHI 期望的 layout。

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

### 11.3 互操作调用约定

互操作不引入专用的 `BeginExternalCommands` / `EndExternalCommands` 动词。这类调用顺序 / RHI 缓存联动问题属于开发期能够一次性保证的事项，RHI 不以运行时 API 兜底。调用方需自行满足：

1. 调用外部库时不在 `BeginRenderPass` / `EndRenderPass` 之间（典型互操作都是 compute，天然如此）。
2. 若调用顺序让 RHI 缓存的 PSO / descriptor / VB / IB / viewport / scissor 与外部库可能冲突，由调用方在外部库返回后重新 `SetPipeline` / `PushResourceTable` 等。
3. **外部库返回后调一次 `cmd.MemoryBarrier()`**（见 § 6）隔离外部写入与后续 RHI 命令。

加上 § 11 引言的"状态不变契约"，互操作场景仅依赖 `GetNativeObject` + `MemoryBarrier`，无需专用 API。

### 11.4 调用方模板

```csharp
// === DLSS (DX12) — 共享 cmd，遵守状态不变契约 ===
cmd.Transition(colorIn,  TransitionState.ShaderResource);
cmd.Transition(colorOut, TransitionState.UnorderedAccess);

DLSS.Evaluate(
    cmd.GetNativeObject(NativeObjectType.D3D12GraphicsCommandList),
    colorIn.GetNativeObject(NativeObjectType.D3D12Resource),
    colorOut.GetNativeObject(NativeObjectType.D3D12Resource));
// 返回时：colorIn 仍为 NON_PIXEL_SHADER_RESOURCE，colorOut 仍为 UNORDERED_ACCESS
cmd.MemoryBarrier();   // 隔离 DLSS 写入与后续 RHI 命令
cmd.Transition(colorOut, TransitionState.ShaderResource);

// === FSR/FFX (Vulkan) — 共享 cmd，同契约 ===
cmd.Transition(colorIn,  TransitionState.ShaderResource);
cmd.Transition(colorOut, TransitionState.UnorderedAccess);

Ffx.Fsr2Dispatch(
    cmd.GetNativeObject(NativeObjectType.VkCommandBuffer),
    colorIn.GetNativeObject(NativeObjectType.VkImage),
    colorOut.GetNativeObject(NativeObjectType.VkImage));
cmd.MemoryBarrier();
cmd.Transition(colorOut, TransitionState.ShaderResource);

// === Skia (Vulkan) — 纹理拷贝路径 ===
// Skia 主动修改 layout，不适合共享 cmd 契约。
// 让 Skia 在其私有 VkImage 上完成绘制，然后在 Skia 自己的 cmd buffer 里
// 把结果拷贝到一张 RHI 拥有的 Texture（TransferDst | Sampled）；Skia 在
// flush 时 signal 一个 timeline semaphore，由调用方包装为 CommandSubmission
// 传入下一次 RHI Submit 的 wait。由于 SkiaSharp 共享 VkImage / flush 信号
// 的 API 易变，该集成始终留在调用方侧。

// === RenderDoc — 仅 device 句柄 ===
nint device = ctx.GetNativeObject(NativeObjectType.D3D12Device);
RenderDocApi.StartFrameCapture(device, IntPtr.Zero);
// ... 渲染一帧 ...
RenderDocApi.EndFrameCapture(device, IntPtr.Zero);
```

### 11.5 不做的事

- 不提供「导入外部 native 资源」入口（首版）；如未来需要：通过 `GraphicsContext.ImportTexture(TextureDesc, nint, TransitionState)` 加入，签名同形态
- 不提供互操作 scope 动词（`BeginExternalCommands` / `EndExternalCommands`）；调用顺序与缓存联动由开发期保证，运行时仅需一次 `MemoryBarrier()`（见 § 11.3）
