# RHI 重设计草案 — API 规范（中文临时版）

> 仅用于本轮设计讨论，不进入提交。最终英文版同步在 `rhi-redesign.draft.md`。
> 同一份规范以 C# 骨架放在 `sources/Zenith.NET.New/`。

## 1. 目标

- 公共 API 与 Metal 4 / Vulkan 1.4 / DirectX 12（Enhanced Barriers）1:1 对齐
- 热路径零托管分配；多元素入参一律 `ReadOnlySpan<T>`
- 跨队列同步以单一值类型 `CommandSubmission` 表达
- 同步原语：`(stage, access)` 二元组，对齐 Metal 4 `barrierAfterStages:beforeStages:visibilityOptions:`
- 内联 RenderPass，去掉长生命周期 `FrameBuffer`
- 子范围 / 子资源信息为**调用现场**值类型，不再有公共 `View` 对象
- 每个 pipeline 一张 `ResourceTable`，对齐 Metal 4 argument-table 模型

## 2. 命名与代码风格约定

- 所有公共值类型：`record struct`，公共字段可写
- 多元素入参一律 `ReadOnlySpan<T>` / `params ReadOnlySpan<T>`，不使用 `T[]` / `IEnumerable<T>`
- 方法体一律 `{ ... }`；**例外**：`ref` / `ref readonly` 返回的属性保留 `=> ref _field;`
- 字节单位字段与参数加 `*InBytes` 后缀
- 抽象后端钩子命名：**只有**与同名非 `Core` 包装共存时才用 `*Core` 后缀（如 `Wait` / `WaitCore`）；其余一律用自然名
- 抽象成员一律 `protected abstract`，同程序集内部调用走 `internal` 包装
- 公共同步结果只有一种：`CommandSubmission`
- 公共面**无**全局内存屏障、**无**驻留集（residency）相关原语

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

// === Texture view（调用现场值） ===

public enum TextureViewType
{
    Texture1D,
    Texture2D,
    Texture3D,
    TextureCube,
    Texture1DArray,
    Texture2DArray,
    TextureCubeArray
}

public enum ComponentSwizzle
{
    Identity,
    Zero,
    One,
    R,
    G,
    B,
    A
}

public record struct ComponentMapping
{
    public ComponentSwizzle R;

    public ComponentSwizzle G;

    public ComponentSwizzle B;

    public ComponentSwizzle A;

    public static ComponentMapping Identity => new()
    {
        R = ComponentSwizzle.Identity,
        G = ComponentSwizzle.Identity,
        B = ComponentSwizzle.Identity,
        A = ComponentSwizzle.Identity
    };
}

public record struct TextureView
{
    public Texture Texture;

    public TextureSubresourceRange Range;

    /// <summary><c>null</c> 表示按 <c>Texture.Desc</c> + <c>Range</c> 推导。</summary>
    public TextureViewType? ViewType;

    /// <summary><c>null</c> 表示沿用 <c>Texture.Desc.Format</c>；非 null 时按同一兼容族重解释。</summary>
    public PixelFormat? Format;

    public ComponentMapping Swizzle;

    public static implicit operator TextureView(Texture texture)
    {
        return new()
        {
            Texture = texture,
            Range = texture,
            ViewType = null,
            Format = null,
            Swizzle = ComponentMapping.Identity
        };
    }
}

// === 同步（stage + access） ===

[Flags]
public enum BarrierStage : uint
{
    None                       = 0,
    All                        = ~0u,
    Draw                       = 1u << 0,
    VertexInput                = 1u << 1,
    VertexShader               = 1u << 2,
    PixelShader                = 1u << 3,
    EarlyDepthStencil          = 1u << 4,
    LateDepthStencil           = 1u << 5,
    RenderTarget               = 1u << 6,
    ComputeShader              = 1u << 7,
    RayTracing                 = 1u << 8,
    Copy                       = 1u << 9,
    Resolve                    = 1u << 10,
    IndirectArgument           = 1u << 11,
    AccelerationStructureBuild = 1u << 12,
}

[Flags]
public enum BarrierAccess : uint
{
    None                       = 0,
    VertexBuffer               = 1u << 0,
    IndexBuffer                = 1u << 1,
    ConstantBuffer             = 1u << 2,
    ShaderRead                 = 1u << 3,
    UnorderedAccessRead        = 1u << 4,
    UnorderedAccessWrite       = 1u << 5,
    RenderTarget               = 1u << 6,
    DepthStencilRead           = 1u << 7,
    DepthStencilWrite          = 1u << 8,
    CopySource                 = 1u << 9,
    CopyDestination            = 1u << 10,
    ResolveSource              = 1u << 11,
    ResolveDestination         = 1u << 12,
    IndirectArgument           = 1u << 13,
    Present                    = 1u << 14,
    AccelerationStructureRead  = 1u << 15,
    AccelerationStructureWrite = 1u << 16,
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

子资源三件套：

| 类型 | 表达 | 用途 |
|---|---|---|
| `TextureSubresource` | 单 mip × 单 layer | RTV / DSV / Resolve |
| `TextureSubresourceLayers` | 单 mip × 连续 layer 段 | Copy / Upload |
| `TextureSubresourceRange` | 连续 mip 段 × 连续 layer 段 | Barrier / `TextureView.Range` |

公共面不暴露 aspect 字段，后端从 `Texture.Format` 推断。Cube 面以 `ArrayLayer = cubeIndex * 6 + face` 表达，无独立 face 轴。

`TextureView` 对齐 VK `VkImageViewCreateInfo` 与 Metal `newTextureViewWithPixelFormat:textureType:levels:slices:swizzle:` 的参数集；背后的原生 view 由后端按 `(Texture, Range, ViewType, Format, Swizzle)` 为 key 懒加载并缓存，纯实现细节。DX12 没有原生 swizzle，后端在槽位 layout 未声明 shader-side swizzle 兼容时拒绝非 Identity 的 swizzle。

`BarrierStage` 描述访问发生在管线**何处**；`BarrierAccess` 描述访问的**性质**。两者对齐 Metal 4 `MTL4RenderStages` + `MTL4VisibilityOptions`、VK 1.4 `VkPipelineStageFlags2` + `VkAccessFlags2`、DX12 Enhanced Barriers `D3D12_BARRIER_SYNC` + `D3D12_BARRIER_ACCESS`。

## 4. 资源句柄

```csharp
public abstract class Buffer(GraphicsContext context, BufferDesc desc) : GraphicsResource(context)
{
    private BufferDesc desc = desc;

    public ref readonly BufferDesc Desc => ref desc;
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

- View 对象不在公共面出现；后端可按 §3 的值类型做 key 懒加载缓存原生 view，纯实现细节。
- DX12 / VK 的 texture image-layout 完全在后端内部维护，由 barrier 携带的 access flags 与 RenderPass attachment 元数据推导。

## 5. CommandQueue / CommandBuffer

每个 `CommandQueue` 持有一条单调递增的完成时间线；一次 `Submit` 推进一个值，并以 `CommandSubmission(queue, value)` 公开该点。跨队列依赖通过把上游 `CommandSubmission` 传入下游 `Submit(waits)` 表达。

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

- `GetCompletedValue()` 是方法而非属性：三端后端（DX12 `fence.GetCompletedValue()` / VK `vkGetSemaphoreCounterValue` / Metal `event.SignaledValue`）都是调用语义。
- CommandBuffer 池化完全是 queue 的内部实现细节；公共面只暴露 `CommandBuffer()` 与 `Submit(...)`。
- `GraphicsContext` 暴露三条队列：`Graphics` / `Compute` / `Copy`。`Present` 始终在 `Graphics` 上执行；`SwapChain` 从 context 取该队列引用。

### 后端时间线对照

| 后端 | 时间线对象 | API |
|---|---|---|
| Metal 4 | 每 queue 一个 `MTLSharedEvent` | `commandBuffer.EncodeSignalEvent(event, value)` / `event.SignaledValue` / `event.NotifyListener` |
| Vulkan 1.4 | 每 queue 一个 timeline `VkSemaphore` | `vkQueueSubmit2` 的 `pSignalSemaphoreInfos[].value` / `vkGetSemaphoreCounterValue` / `vkWaitSemaphores` |
| DX12 | 每 queue 一个 `ID3D12Fence` | `queue.Signal(fence, value)` / `fence.GetCompletedValue()` / `fence.SetEventOnCompletion` |

## 6. CommandBuffer 操作

```csharp
// === 同步 ===

public void Barrier(BarrierStage afterStages, BarrierAccess afterAccess,
                    BarrierStage beforeStages, BarrierAccess beforeAccess);

public void BufferBarrier(BufferRange range,
                          BarrierStage afterStages, BarrierAccess afterAccess,
                          BarrierStage beforeStages, BarrierAccess beforeAccess);

public void TextureBarrier(TextureView view,
                           BarrierStage afterStages, BarrierAccess afterAccess,
                           BarrierStage beforeStages, BarrierAccess beforeAccess);

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

- `BeginRenderPass` 接受 attachment span，无深度时显式传 `null`；内部按 attachment 自动发出对应的同步 barrier，并按 attachment 尺寸自动填 `SetViewports` / `SetScissors`，调用方可在其后再调一次覆盖。
- `PushResourceTable` 不接受 stages 参数：stages 信息由 table 的 layout 在每个 binding 上自带。每个 pipeline 仅支持一张 table。
- **Push-snapshot 语义**：`PushResourceTable` 在调用现场把 `table` 的当前内容快照进 cmd buffer；之后对该 `table` 的 `Write` 不影响已 push 的绑定，因此同一个 `ResourceTable` 可以在帧内反复 `Write` + `Push`。
- `Barrier` 是不带资源的全局形式；`BufferBarrier` / `TextureBarrier` 把 barrier 限定到 `BufferRange` / `TextureView`。三者形态一致，都是 `(afterStages, afterAccess) → (beforeStages, beforeAccess)`。

### ResourceTable

```csharp
public abstract class ResourceTable
{
    public void Write(uint binding, BufferRange range);

    public void Write(uint binding, TextureView view);

    public void Write(uint binding, Sampler sampler);

    public void Write(uint binding, ReadOnlySpan<BufferRange> ranges);

    public void Write(uint binding, ReadOnlySpan<TextureView> views);

    public void Write(uint binding, ReadOnlySpan<Sampler> samplers);
}
```

- `Buffer` / `Texture` 经隐式转换让单资源绑定保持一行。
- buffer 解释（CBV / SRV-structured / SRV-byteaddress / UAV / typed）由槽位的 `ResourceLayout` 决定。
- 校验：UAV 槽位要求 `BufferUsageFlags.UnorderedAccess` / `TextureUsageFlags.UnorderedAccess`，shape / layout / 格式族不匹配在 `Write` 时报错。

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

- backbuffer 以普通 `Texture` 公开（颜色 + 可选深度模板）。
- `Present(...)` 返回 `CommandSubmission`，与 `CommandBuffer.Submit(...)` 形态对称；接收任意队列的 wait。
- 当前 image index 完全是后端事务。

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

    cmd.BeginRenderPass(colors, depth);
    // draws
    cmd.EndRenderPass();

    CommandSubmission frame = cmd.Submit(imageReady);

    imageReady = swapChain.Present(frame);
}
```

每一步形态一致：拿一组 `CommandSubmission` waits，返回一个 `CommandSubmission`。`Submit` 与 `Present` 在 API 表面对称。

## 9. 原生句柄

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

- 命名沿用各原生 API 既有前缀：DX12 体系按 `Dxgi` / `D3D12` 区分；Metal 4 与旧 Metal 共存时用 `Mtl` / `Mtl4` 区分；Vulkan 一律 `Vk`。`D3D12CpuDescriptorHandle*` 按 view 类型拆开，避免单入口语义模糊。
- 非句柄的标量信息（如 `VkQueueFamilyIndex`）也走同一入口，以 `nint` 承载 `uint`，公共面不挂任何平台条件属性。
- 所有 `GraphicsResource` 子类一律实现该接口；不感兴趣的子类返回 0。
