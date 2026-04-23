# RHI Redesign — API Specification

> Working spec for the redesigned public surface. Not for commit. Mirrors `rhi-redesign.zh.draft.md`.
> The same surface is laid out as a C# skeleton under `sources/Zenith.NET.New/`.

## 1. Goals

- Public API maps 1:1 to Metal 4 / Vulkan 1.4 / DirectX 12 (Enhanced Barriers).
- Hot path is allocation-free; multi-element parameters use `ReadOnlySpan<T>`.
- Cross-queue synchronization via a single value type (`CommandSubmission`).
- Synchronization primitive is `(stage, access)` pairs, modelled after Metal 4 `barrierAfterStages:beforeStages:visibilityOptions:`.
- Inline render passes; no long-lived `FrameBuffer`.
- Subresource / sub-range information is a **call-site value type**, not a long-lived `View` object.
- One `ResourceTable` per pipeline, aligned with the Metal 4 argument-table model.

## 2. Conventions

- All public value types: `record struct` with public mutable fields.
- Multi-element parameters: `ReadOnlySpan<T>` / `params ReadOnlySpan<T>`. No `T[]`, no `IEnumerable<T>`.
- All method bodies use `{ ... }`. **Exception:** `ref` / `ref readonly` returning properties keep `=> ref _field;`.
- Every byte-denominated parameter or field carries the `*InBytes` suffix.
- Backend hooks use the `*Core` suffix only when they share a name with a non-`Core` wrapper (e.g. `Wait` / `WaitCore`); otherwise they keep their natural name.
- Every abstract member is plain `protected abstract`; same-assembly callers go through an `internal` wrapper.
- The only public synchronization result type is `CommandSubmission`.
- There are **no global barriers** and **no residency primitives** on the public surface.

## 3. Public Type Catalog

```csharp
// === Subresources ===

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

// === Buffer sub-range ===

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

// === Texture view (call-site value) ===

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

    /// <summary><c>null</c> derives from <c>Texture.Desc</c> + <c>Range</c>.</summary>
    public TextureViewType? ViewType;

    /// <summary><c>null</c> uses <c>Texture.Desc.Format</c>; otherwise reinterprets within the same family.</summary>
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

// === Synchronization (stage + access) ===

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

// === RenderPass attachments ===

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

// === Queue submission result ===

/// <summary>
/// One point on a queue's completion timeline, produced by Submit / Present.
/// <c>default</c> is the well-defined empty value: backends filter it out of any <c>waits</c>,
/// and <see cref="Wait"/> is a no-op on it.
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

Subresource triplet:

| Type | Shape | Used by |
|---|---|---|
| `TextureSubresource` | one mip × one layer | RTV / DSV / Resolve |
| `TextureSubresourceLayers` | one mip × contiguous layer range | Copy / Upload |
| `TextureSubresourceRange` | contiguous mip range × contiguous layer range | Barrier / `TextureView.Range` |

The public surface does not carry an aspect field; backends derive aspect (color / depth / stencil) from `Texture.Format`. Cube faces are addressed as `ArrayLayer = cubeIndex * 6 + face`; there is no separate face axis.

`TextureView` mirrors VK `VkImageViewCreateInfo` and Metal `newTextureViewWithPixelFormat:textureType:levels:slices:swizzle:`. The native view object is created lazily and cached per backend, keyed by `(Texture, Range, ViewType, Format, Swizzle)`. DX12 has no first-class swizzle; the backend rejects non-Identity swizzles unless the slot's layout opts in to a shader-side swizzle workaround.

`BarrierStage` describes **when** in the pipeline an access happens; `BarrierAccess` describes **what kind** of access it is. They mirror Metal 4 `MTL4RenderStages` + `MTL4VisibilityOptions`, VK 1.4 `VkPipelineStageFlags2` + `VkAccessFlags2`, and DX12 Enhanced Barriers `D3D12_BARRIER_SYNC` + `D3D12_BARRIER_ACCESS`.

## 4. Resource Handles

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

- View objects are not on the public surface. Backends may lazily cache native views keyed by the §3 value types; that cache is a backend implementation detail.
- Texture image-layout (DX12 / VK) is fully internal to the backend; it is computed from the access flags supplied to barriers and from RenderPass attachment metadata.

## 5. CommandQueue / CommandBuffer

Each `CommandQueue` owns one monotonic completion timeline. Each `Submit` advances that timeline by one and exposes the new value as `CommandSubmission(queue, value)`. Cross-queue dependencies are expressed by feeding upstream `CommandSubmission`s into a downstream `Submit(waits)`.

```csharp
public abstract class CommandQueue(GraphicsContext context, CommandQueueType type) : GraphicsResource(context)
{
    public CommandQueueType Type { get; } = type;

    /// <summary>Acquires a recording-ready CommandBuffer, recycling a retired one when available.</summary>
    public CommandBuffer CommandBuffer();

    /// <summary>Waits until every submission issued on this queue has completed. Idempotent.</summary>
    public void WaitForIdle();

    protected abstract ulong GetCompletedValue();

    protected abstract void WaitCore(ulong value);

    protected abstract void SubmitCore(CommandBuffer commandBuffer, ReadOnlySpan<CommandSubmission> waits, ulong signalValue);

    protected abstract CommandBuffer CreateCommandBuffer();
}

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    /// <summary>Closes recording and enqueues this buffer on its owning queue.</summary>
    public CommandSubmission Submit(params ReadOnlySpan<CommandSubmission> waits);
}
```

- `GetCompletedValue()` is a method, not a property: all three backends (DX12 `fence.GetCompletedValue()` / VK `vkGetSemaphoreCounterValue` / Metal `event.SignaledValue`) are call-shaped.
- `CommandBuffer` pooling is entirely a queue-internal detail; the public surface only offers `CommandBuffer()` and `Submit(...)`.
- `GraphicsContext` exposes three queues: `Graphics` / `Compute` / `Copy`. `Present` always runs on `Graphics`; `SwapChain` pulls that queue reference from the context.

### Backend Timeline Mapping

| Backend | Timeline object | API |
|---|---|---|
| Metal 4 | one `MTLSharedEvent` per queue | `commandBuffer.EncodeSignalEvent(event, value)` / `event.SignaledValue` / `event.NotifyListener` |
| Vulkan 1.4 | one timeline `VkSemaphore` per queue | `vkQueueSubmit2` `pSignalSemaphoreInfos[].value` / `vkGetSemaphoreCounterValue` / `vkWaitSemaphores` |
| DX12 | one `ID3D12Fence` per queue | `queue.Signal(fence, value)` / `fence.GetCompletedValue()` / `fence.SetEventOnCompletion` |

## 6. CommandBuffer Operations

```csharp
// === Synchronization ===

public void Barrier(BarrierStage afterStages, BarrierAccess afterAccess,
                    BarrierStage beforeStages, BarrierAccess beforeAccess);

public void BufferBarrier(BufferRange range,
                          BarrierStage afterStages, BarrierAccess afterAccess,
                          BarrierStage beforeStages, BarrierAccess beforeAccess);

public void TextureBarrier(TextureView view,
                           BarrierStage afterStages, BarrierAccess afterAccess,
                           BarrierStage beforeStages, BarrierAccess beforeAccess);

// === Inline RenderPass ===

public void BeginRenderPass(ReadOnlySpan<ColorAttachment> colorAttachments,
                            DepthStencilAttachment? depthStencilAttachment);

public void EndRenderPass();

// === Viewports / Scissors ===

public void SetViewports(ReadOnlySpan<Viewport> viewports);

public void SetScissors(ReadOnlySpan<Scissor> scissors);

// === Pipeline / Resource binding ===

public void SetPipeline(Pipeline pipeline);

public void PushResourceTable(ResourceTable table);

// === Vertex / Index ===

public void SetVertexBuffer(uint slot, Buffer buffer, ulong offsetInBytes);

public void SetVertexBuffers(uint firstSlot, ReadOnlySpan<Buffer> buffers, ReadOnlySpan<ulong> offsetsInBytes);

public void SetIndexBuffer(Buffer buffer, ulong offsetInBytes, IndexFormat format);

// === Draws / Dispatches ===

public void Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance);

public void DrawIndexed(uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance);

public void DrawIndirect(Buffer argsBuffer, ulong offsetInBytes, uint drawCount, uint strideInBytes);

public void DrawIndexedIndirect(Buffer argsBuffer, ulong offsetInBytes, uint drawCount, uint strideInBytes);

public void Dispatch(uint groupCountX, uint groupCountY, uint groupCountZ);

public void DispatchIndirect(Buffer argsBuffer, ulong offsetInBytes);

// === Buffer copy / upload ===

public void CopyBuffer(Buffer source, ulong sourceOffsetInBytes,
                       Buffer destination, ulong destinationOffsetInBytes,
                       ulong sizeInBytes);

public void Upload<T>(Buffer destination, ulong offsetInBytes, ReadOnlySpan<T> data) where T : unmanaged;

// === Texture copy / upload / resolve ===

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

- `BeginRenderPass` accepts an attachment span; pass `null` explicitly for no depth. The implementation auto-emits the attachment-side barriers and auto-fills `SetViewports` / `SetScissors` from attachment dimensions; the caller may override afterward.
- `PushResourceTable` takes no `stages` argument — stage information is carried per-binding in the table's layout. Only one table per pipeline is supported.
- **Push-snapshot semantics**: `PushResourceTable` snapshots `table` into the cmd buffer at the call site; subsequent `Write`s do not affect already-pushed bindings, so the same `ResourceTable` can be repeatedly `Write` + `Push`ed within a frame.
- `Barrier` is the global form (no resource argument). `BufferBarrier` / `TextureBarrier` scope the barrier to a `BufferRange` / `TextureView`. All three carry the same `(afterStages, afterAccess) → (beforeStages, beforeAccess)` shape.

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

- `Buffer` / `Texture` flow into the call through their implicit operators, keeping single-resource binds a single line.
- The interpretation of a buffer (CBV / structured SRV / byte-address SRV / UAV / typed) is fixed by the slot's `ResourceLayout`.
- A UAV slot requires `BufferUsageFlags.UnorderedAccess` / `TextureUsageFlags.UnorderedAccess`; shape / layout / format-family mismatches are reported at `Write` time.

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
    /// Submits a present on the GraphicsQueue, waiting on <paramref name="waits"/>;
    /// returns a <see cref="CommandSubmission"/> that signals when the next backbuffer is writable.
    /// </summary>
    public abstract CommandSubmission Present(params ReadOnlySpan<CommandSubmission> waits);

    public void Resize(uint width, uint height);

    public void Refresh(Surface surface);
}
```

- Backbuffers are exposed as plain `Texture`s (color + optional depth-stencil).
- `Present(...)` returns a `CommandSubmission` describing when the next backbuffer is writable; it accepts waits from any queue, symmetrically with `CommandBuffer.Submit(...)`.
- The current backbuffer index is entirely a backend concern.

## 8. Frame Loop

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

Every step has the same shape: take a set of `CommandSubmission` waits, return a `CommandSubmission`. `Submit` and `Present` are symmetric on the API surface.

## 9. Native Handles

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
    /// <summary>Returns 0 when the enum does not match this object or the current backend.</summary>
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

- Prefixes follow each native API: DX12 splits into `Dxgi` / `D3D12`; Metal 4 keeps `Mtl4` distinct from the older `Mtl`; Vulkan uses `Vk`. `D3D12CpuDescriptorHandle*` is split per view type to avoid ambiguity at a single entry point.
- Non-handle scalar information (e.g. `VkQueueFamilyIndex`) flows through the same entry point, carrying a `uint` inside an `nint`. The public surface never grows a platform-conditional property.
- Every `GraphicsResource` subclass implements the interface; subclasses with nothing to expose return 0.
