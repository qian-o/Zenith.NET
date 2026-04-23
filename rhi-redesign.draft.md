# RHI Redesign

> Working document for the redesign discussion. Not for commit. The Chinese mirror lives in `rhi-redesign.zh.draft.md`.

## 1. Goals

- Small, uniform public API surface that maps 1:1 to DirectX 12 / Vulkan 1.4 / Metal 4.
- Short frame-loop code with no managed allocations on the hot path.
- Cross-queue synchronization (Graphics / Compute / Copy) as a first-class primitive.
- Explicit resource state transitions between `ShaderResource` and `UnorderedAccess`; every other transition is implicit from the operation.
- Inline render passes; no long-lived `FrameBuffer`.
- Subresource / sub-range information expressed at the **call site** as a value type, not as a long-lived `View` object.
- One `ResourceTable` per pipeline (no secondary descriptor sets), matching the Metal 4 argument-table model and the other two backends' root/descriptor models at 1:1.

## 2. API-Surface Conventions

- All public value types: `record struct` with public mutable fields.
- Multi-element parameters: `ReadOnlySpan<T>` / `params ReadOnlySpan<T>`. No `T[]`, no `IEnumerable<T>`.
- All method bodies use `{ ... }`. **Exception:** `ref` / `ref readonly` returning properties keep `=> ref _field;`.
- Every byte-denominated parameter or field carries an `*InBytes` suffix.
- Backend-hook naming: the `*Core` suffix exists **only** to disambiguate from a same-named non-`Core` wrapper (e.g. `Wait` / `WaitCore`, `Submit` / `SubmitCore`). Hooks without a wrapper use their natural name.
- Every abstract member is plain `protected abstract`; same-assembly callers go through an `internal` wrapper.
- The only public synchronization result type is `CommandSubmission`.

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

// === Resource state (public surface) ===

public enum TransitionState
{
    ShaderResource,
    UnorderedAccess
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

| Type | Shape | Used by | Backend mapping |
|---|---|---|---|
| `TextureSubresource` | one mip × one layer | RTV / DSV / Resolve | DX12 plane+mip+slice singleton; VK `aspect+mip+layer` singleton; Metal `level+slice` |
| `TextureSubresourceLayers` | one mip × contiguous layer range | Copy / Upload | `VkImageSubresourceLayers`; DX12 issues one `CopyTextureRegion` per layer; Metal `blitEncoder` per layer |
| `TextureSubresourceRange` | contiguous mip range × contiguous layer range | View / Transition | `VkImageSubresourceRange`; DX12 view desc base+count; Metal `MTLTextureView` |

The public surface does **not** carry an aspect field; backends derive aspect (color / depth / stencil) from `Texture.Format`. Cube faces use `ArrayLayer = cubeIndex * 6 + face`, matching VK / DX12 / Metal — no separate face axis.

## 4. Resource Handles

`Buffer` / `Texture` / `Sampler` no longer implement `IBindableResource` (the interface is removed):

```csharp
public abstract class Buffer(GraphicsContext context, BufferDesc desc) : GraphicsResource(context)
{
    private BufferDesc desc = desc;

    public ref readonly BufferDesc Desc => ref desc;

    // Map / Unmap / Upload kept
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

**View caches** (per-backend, on each `Texture` / `Buffer`): the design permits backends to lazily cache the native view objects keyed by the value types in §3, but no part of this is on the public surface.

**State tracking:** every `Texture` / `Buffer` carries its current `TransitionState`; the initial value is set by the backend at creation. The first explicit `Transition` synthesizes the from→to barrier against that cached value. Only `ShaderResource` ↔ `UnorderedAccess` is user-visible; all other transitions (render-target / depth-stencil / copy / vertex / index / CBV / indirect / present) are driven implicitly by the operation issuing them.

**Removed from the framework (these all exist today):** `BufferView` / `BufferViewDesc` / `TextureView` / `TextureViewDesc` / `BufferViewType` / `IBindableResource` / `TextureSlice` (incl. `Face`) / `TextureOffset` / `TextureExtent` / `FrameBuffer` / `FrameBufferDesc` / `FrameBufferAttachment` / `ClearValue` / `ClearValues` (static factory) / `ClearFlags` / `GraphicsContext.CreateBufferView` / `CreateTextureView` / `CreateFrameBuffer`. The `Output` type itself stays — it remains `GraphicsPipelineDesc.Output`; only the `FrameBuffer.Output` use site disappears.

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

- `GetCompletedValue()` is a **method**, not a property: all three backends (DX12 `fence.GetCompletedValue()` / VK `vkGetSemaphoreCounterValue` / Metal `event.SignaledValue`) are call-shaped.
- `CommandBuffer` pooling is entirely a queue-internal detail; the public surface only offers `CommandBuffer()` and `Submit(...)`.
- Three queues are exposed on `GraphicsContext`: `Graphics` / `Compute` / `Copy`. `Present` always runs on `Graphics`; `SwapChain` pulls that queue reference from the context and never exposes it on its own surface.

### Backend Timeline Mapping

| Backend | Timeline object | API |
|---|---|---|
| DX12 | one `ID3D12Fence` per queue | `queue.Signal(fence, value)` / `fence.GetCompletedValue()` / `fence.SetEventOnCompletion` |
| Vulkan 1.4 | one timeline `VkSemaphore` per queue (core feature) | `vkQueueSubmit2` `pSignalSemaphoreInfos[].value` / `vkGetSemaphoreCounterValue` / `vkWaitSemaphores` |
| Metal 4 | one `MTLSharedEvent` per queue | `commandBuffer.EncodeSignalEvent(event, value)` / `event.SignaledValue` / `event.NotifyListener` |

All three backends support this timeline model natively; the table is a feasibility record, not a prescription.

## 6. CommandBuffer Operations

```csharp
// === State ===

public void Transition(Buffer buffer, TransitionState newState);

public void Transition(Texture texture, TransitionState newState);

public void MemoryBarrier();

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

Notes:

- `BeginRenderPass` accepts an attachment span; pass `null` explicitly for no depth. The implementation auto-`Transition`s the attachments and auto-fills `SetViewports` / `SetScissors` from attachment dimensions; the caller may issue `SetViewports` / `SetScissors` afterward to override.
- `PushResourceTable` takes no `stages` argument: stage information is carried per-binding in `ResourceTable.Layout` (DX12 root-parameter visibility and Metal argument-table placement are derived from it; the VK set's stage mask is fixed at layout creation time). Only one table per pipeline is supported; there is no `setIndex`.
- **Push-snapshot semantics**: `PushResourceTable` snapshots the current contents of `table` into the cmd buffer at the call site; subsequent `Write`s to that `table` do not affect already-pushed bindings. All three backends provide this natively (DX12 descriptor copy on bind, VK `vkCmdPushDescriptorSet`, Metal 4 `setArgumentTable:`), so the same `ResourceTable` can be repeatedly `Write` + `Push`ed within a frame.
- `Transition` is only called explicitly between `ShaderResource` and `UnorderedAccess`. Render-target / depth-stencil / copy / vertex / index / CBV / indirect / present states are transitioned implicitly by the corresponding operation.
- `MemoryBarrier()` is a global cross-resource / cross-stage memory barrier; the caller decides when to issue it.

### ResourceTable

`Write` becomes strongly typed, replacing the current `Write(uint, params IBindableResource[])` + runtime type-switch:

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

- `Buffer` / `Texture` flow into the call through their implicit operators, keeping single-resource binds a single line.
- The interpretation of a buffer (CBV / structured SRV / byte-address SRV / UAV / typed) is fixed by the slot's `ResourceLayout`; there is no public `BufferViewType`.
- Validation: a UAV slot requires `BufferUsageFlags.UnorderedAccess` / `TextureUsageFlags.UnorderedAccess`; shape / layout mismatches are reported at `Write` time.

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

Design points:

- Backbuffers are exposed as plain `Texture`s (color + optional depth-stencil). No `FrameBuffer`, no image index.
- `Present(...)` returns a `CommandSubmission` describing when the next backbuffer is writable; it accepts waits from any queue, symmetrically with `CommandBuffer.Submit(...)`.
- The current backbuffer index is entirely a backend concern and is never on the public surface.

All three backends support the shape `Present(waits) → CommandSubmission` natively: DX12 `IDXGISwapChain3::Present` with `graphicsQueue.Wait(fence, value)` for each wait; Vulkan 1.4 a bridging `vkQueueSubmit2` that translates timeline waits into a binary `renderFinished` semaphore consumed by `vkQueuePresentKHR`; Metal 4 `commandBuffer.EncodeWaitForEvent(...)` → `commandBuffer.Present(drawable)` → `commandBuffer.Commit()`.

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

    // Viewports / scissors are auto-filled from attachment dimensions; override afterward if needed.
    cmd.BeginRenderPass(colors, depth);
    // draws
    cmd.EndRenderPass();

    CommandSubmission frame = cmd.Submit(imageReady);

    imageReady = swapChain.Present(frame);
}
```

Every step has the same shape: take a set of `CommandSubmission` waits, return a `CommandSubmission`. `Submit` and `Present` are symmetric on the API surface.

## 9. Three-Backend Feasibility Matrix

Every public primitive in §3–§7 has a native equivalent on all three backends:

| Concept | DX12 | Vulkan 1.4 | Metal 4 |
|---|---|---|---|
| Inline RenderPass | `ID3D12GraphicsCommandList4::BeginRenderPass` / `EndRenderPass` | `vkCmdBeginRendering` / `vkCmdEndRendering` | `MTL4CommandBuffer::MakeRenderCommandEncoder` / `endEncoding` |
| LoadAction | `RenderPassBeginningAccessType` | `VkAttachmentLoadOp` | `MTLLoadAction` |
| StoreAction (incl. Resolve) | `RenderPassEndingAccessType` | `VkAttachmentStoreOp` + `pResolveAttachments` + `resolveMode` | `MTLStoreAction` + `resolveTexture` |
| Explicit Transition (SRV ↔ UAV) | `ResourceBarrier(Transition / UAV)` | `vkCmdPipelineBarrier2` (`VkImageMemoryBarrier2` / `VkBufferMemoryBarrier2`) | `MTL4ComputeCommandEncoder.BarrierAfterEncoderStages` |
| Global `MemoryBarrier()` | `ResourceBarrier(UAV)` global | `vkCmdPipelineBarrier2` with `VK_ACCESS_2_MEMORY_READ/WRITE_BIT` | `BarrierAfterEncoderStages` with all stages |
| Aspect inference source | format → plane index | `Texture.Format` → `VkImageAspectFlags` | format → automatic |

## 10. RDG Interface Self-Check

The RDG is built on top of this RHI by the consumer, not by us. This section only answers: "Does the RHI expose every primitive an RDG needs?"

| RDG requirement | Provided by RHI | Entry point |
|---|---|---|
| Explicit resource state transitions | ✅ | `CommandBuffer.Transition(Buffer/Texture, TransitionState)` |
| Global memory barrier | ✅ | `CommandBuffer.MemoryBarrier()` |
| Sub-range addressing (alias / hazard keys) | ✅ | `BufferRange` / `TextureSubresourceRange` (`record struct`, hashable) |
| Cross-queue dependencies | ✅ | `CommandSubmission` waits (symmetric on `Submit` / `Present`) |
| Queue completion query / wait | ✅ | `CommandQueue.WaitForIdle()` / `CommandSubmission.Wait()` |
| Short-lived RenderPass | ✅ | Inline `BeginRenderPass(colorAttachments, depth)` |

Conclusion: an RDG implementation can be built without any further changes to the RHI public surface.

## 11. Interop / Native Handles

For third-party libraries that need backend-native handles: Skia / DLSS / FSR / RenderDoc / tooling. Design principles:

- The public surface exposes only `nint` plus an enum; no backend types and no platform-conditional properties.
- The RHI does not perform work on behalf of the external library, and provides no escape hatch for external code to mutate the RHI's cached resource state.
- The interop API is a side-effect of state the RHI already maintains internally — zero extra runtime cost.

**State-preservation contract**: any resource exposed to an external library through `GetNativeObject(...)` must be returned in the underlying state matching its current `TransitionState`:

- DX12: `ShaderResource` → `D3D12_RESOURCE_STATE_NON_PIXEL_SHADER_RESOURCE | PIXEL_SHADER_RESOURCE`; `UnorderedAccess` → `D3D12_RESOURCE_STATE_UNORDERED_ACCESS`.
- Vulkan: `ShaderResource` → `VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL`; `UnorderedAccess` → `VK_IMAGE_LAYOUT_GENERAL`.
- Metal 4: state is driver-managed; no caller cooperation required.

Compute post-processing libraries (DLSS / XeSS / FFX-FSR / NRD) honour this contract by design (state at entry == state at exit). Libraries that actively rewrite layout (Skia and similar) are not suitable for the shared-cmd path; instead use:

1. **Texture copy** (recommended): let the external library render onto its own private resource, then copy the output into an RHI texture (a future import entry point may avoid the copy; see § 11.5).
2. **Dedicated swapchain**: pure-UI applications can hand the entire swapchain to the external library; the RHI does not participate in that swapchain's rendering.
3. **Configure the library to restore layout**: e.g. Skia's `GrBackendTexture::setVkImageLayout` can request the layout to be restored to the RHI's expected value after every flush.

### 11.1 NativeObjectType

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

Prefixes follow the original native APIs: DX12 splits into `Dxgi` / `D3D12`; Metal 4 keeps `Mtl4` distinct from the older `Mtl`; Vulkan uses `Vk`. `D3D12CpuDescriptorHandle*` is split per view type to avoid ambiguity at a single entry point.

Non-handle scalar information (e.g. `VkQueueFamilyIndex`) flows through the same entry point, carrying a `uint` inside an `nint`. The public surface never grows a platform-conditional property.

### 11.2 INativeObject Interface

```csharp
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

Every `GraphicsResource` subclass (`Buffer` / `Texture` / `Sampler` / `CommandQueue` / `CommandBuffer` / `SwapChain` / `Pipeline` / `Shader` / `ResourceTable` / `ResourceLayout`) implements the interface. Backends use a `switch`; subclasses with nothing to expose return 0. Third-party code can program against the interface without distinguishing context from resource.

Backend implementation examples:

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

// Subclass that does not currently expose anything
public override nint GetNativeObject(NativeObjectType type) => 0;
```

### 11.3 Interop Calling Convention

No dedicated `BeginExternalCommands` / `EndExternalCommands` verbs. Call ordering and RHI cache interaction are concerns the caller resolves at development time, not at the runtime API boundary. The caller guarantees:

1. The external library is invoked outside any `BeginRenderPass` / `EndRenderPass` (typical interop is compute, so this holds naturally).
2. If the call ordering would let the RHI's cached PSO / descriptors / vertex+index buffers / viewports / scissors collide with the external library, the caller re-issues `SetPipeline` / `PushResourceTable` etc. after the external library returns.
3. **Issue one `cmd.MemoryBarrier()`** (see § 6) after the external library returns to isolate its writes from subsequent RHI commands.

Combined with the state-preservation contract from § 11, interop relies only on `GetNativeObject` and `MemoryBarrier`; no dedicated API is required.
### 11.4 Caller Templates

```csharp
// === DLSS (DX12) — shared cmd, follows the state-preservation contract ===
cmd.Transition(colorIn,  TransitionState.ShaderResource);
cmd.Transition(colorOut, TransitionState.UnorderedAccess);

DLSS.Evaluate(
    cmd.GetNativeObject(NativeObjectType.D3D12GraphicsCommandList),
    colorIn.GetNativeObject(NativeObjectType.D3D12Resource),
    colorOut.GetNativeObject(NativeObjectType.D3D12Resource));
// On return: colorIn stays NON_PIXEL_SHADER_RESOURCE, colorOut stays UNORDERED_ACCESS.
cmd.MemoryBarrier();   // isolate DLSS writes from later RHI commands
cmd.Transition(colorOut, TransitionState.ShaderResource);

// === FSR/FFX (Vulkan) — shared cmd, same contract ===
cmd.Transition(colorIn,  TransitionState.ShaderResource);
cmd.Transition(colorOut, TransitionState.UnorderedAccess);

Ffx.Fsr2Dispatch(
    cmd.GetNativeObject(NativeObjectType.VkCommandBuffer),
    colorIn.GetNativeObject(NativeObjectType.VkImage),
    colorOut.GetNativeObject(NativeObjectType.VkImage));
cmd.MemoryBarrier();
cmd.Transition(colorOut, TransitionState.ShaderResource);

// === Skia (Vulkan) — texture-copy path ===
// Skia actively rewrites layout, so it does not fit the shared-cmd contract.
// Let Skia render onto its own private VkImage, then copy into an RHI-owned
// Texture (TransferDst | Sampled) from inside Skia's own command buffer; have
// Skia signal a timeline semaphore on flush and feed it into the next RHI
// Submit as a CommandSubmission wait. The exact SkiaSharp surface for sharing
// a VkImage and the flush-signal is volatile, so the integration stays caller-side.

// === RenderDoc — device handle only ===
nint device = ctx.GetNativeObject(NativeObjectType.D3D12Device);
RenderDocApi.StartFrameCapture(device, IntPtr.Zero);
// ... render one frame ...
RenderDocApi.EndFrameCapture(device, IntPtr.Zero);
```

### 11.5 Out of Scope

- No "import external native resource" entry point in this iteration; if needed later, add `GraphicsContext.ImportTexture(TextureDesc, nint, TransitionState)` with the same shape.
- No dedicated interop scope verbs (`BeginExternalCommands` / `EndExternalCommands`); call ordering and cache interaction are caller-side concerns, with a single `MemoryBarrier()` at runtime (see § 11.3).
