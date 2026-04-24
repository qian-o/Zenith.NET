# RHI Redesign — Working Draft

> Working spec for the next public surface. Not for commit. Mirrors `rhi-redesign.zh.draft.md`.
> Rebuilt against the current `Zenith.NET`, `Extensions`, and `Views` folders. Validation-layer design is intentionally deferred.
> The document stays organized as nine numbered requirements; shared conventions live in §0.

## 0. Common Conventions

### 0.1 Naming and Code Style

- Every public value type is a `record struct` with public mutable fields.
- Multi-element inputs use `ReadOnlySpan<T>` / `params ReadOnlySpan<T>`. No `T[]`, `IEnumerable<T>`, or `IReadOnlyList<T>` on the low-level surface.
- Every byte-denominated field or parameter uses the `*InBytes` suffix.
- Backend hooks use plain `protected abstract` members; wrappers above them are only for validation or convenience.
- Public convenience wrappers may remain when they do not obscure the low-level model, for example `Submit(bool waitForCompletion = false)` on top of the timeline submit path.

### 0.2 Public Surface Boundaries

- `BufferView` stays removed. Buffer subranges are expressed as `BufferRange`.
- `TextureView` stays a long-lived `GraphicsResource` created by `GraphicsContext.CreateTextureView(...)`.
- `ResourceTable` stays a long-lived `GraphicsResource` with explicit typed `Write(...)` overloads.
- There is no public `FrameBuffer` / `RenderPassInfo` object. Render passes are inline.
- CPU upload and readback are described with `BufferData`, `TextureData`, and `TextureDataLayout`.
- The view layer renders into a `Texture target`; the platform view decides whether that target is a swapchain backbuffer or a CPU-readback texture.
- This round only adds the missing low-level synchronization pieces: explicit texture layout transitions, a lightweight `PipelineBarrier` for read-write hazards, queue timelines, and a completed `NativeObjectType`.
- Residency, heap management, and validation-layer redesign remain out of scope.

### 0.3 Core Object Skeleton

```csharp
public abstract class GraphicsContext : DisposableObject, INativeObject
{
    public Backend Backend { get; }

    public Capabilities Capabilities { get; }

    public CommandQueue Graphics { get; }

    public CommandQueue Compute { get; }

    public CommandQueue Copy { get; }

    public SwapChain CreateSwapChain(SwapChainDesc desc);

    public Buffer CreateBuffer(BufferDesc desc);

    public Texture CreateTexture(TextureDesc desc);

    public TextureView CreateTextureView(TextureViewDesc desc);

    public ResourceTable CreateResourceTable(ResourceTableDesc desc);

    public abstract nint GetNativeObject(NativeObjectType type);
}

public abstract class GraphicsResource(GraphicsContext context) : DisposableObject, INativeObject
{
    public GraphicsContext Context { get; } = context;

    public abstract nint GetNativeObject(NativeObjectType type);
}

public abstract class Buffer(GraphicsContext context, BufferDesc desc) : GraphicsResource(context)
{
    public ref readonly BufferDesc Desc => ref desc;

    public abstract MappedMemory Map();

    public abstract void Unmap();

    public void Upload(uint offsetInBytes, BufferData data);

    public void Download(uint offsetInBytes, BufferData data);
}

public abstract class Texture(GraphicsContext context, TextureDesc desc) : GraphicsResource(context)
{
    public ref readonly TextureDesc Desc => ref desc;

    public void Upload(TextureSubresource subresource, Offset3D offset, Extent3D extent, TextureData data);

    public void Download(TextureSubresource subresource, Offset3D offset, Extent3D extent, TextureData data);
}
```

### 0.4 Explicit CPU Data Descriptors

```csharp
public record struct BufferData
{
    public nint Pointer;

    public uint SizeInBytes;
}

public record struct TextureData
{
    public nint Pointer;

    public TextureDataLayout Layout;
}

public record struct TextureDataLayout
{
    public uint SizeInBytes;

    public uint RowPitchInBytes;

    public uint SlicePitchInBytes;
}
```

Design points:
- `TextureDataLayout` is a pure layout descriptor. It does not carry offsets.
- Byte offsets stay on copy entry points such as `CopyBufferToTexture(..., uint srcOffsetInBytes, TextureDataLayout srcLayout, ...)`.
- Tight layouts can be computed with `ZenithHelper`, but callers remain responsible for the final `SizeInBytes` / `RowPitchInBytes` / `SlicePitchInBytes` they pass.
- Backend-specific copy alignment remains backend-local; it is not lifted back into `GraphicsContext`.

---

## 1. ReadOnlySpan at Multi-element Boundaries

Requirement: every multi-element public input keeps using `ReadOnlySpan<T>` / `params ReadOnlySpan<T>`.

Representative surfaces:

| Surface | Shape |
|---|---|
| Render pass color attachments | `ReadOnlySpan<ColorAttachment>` |
| Viewports / scissors | `ReadOnlySpan<Viewport>` / `ReadOnlySpan<Scissor>` |
| ResourceTable array writes | `ReadOnlySpan<Buffer>` / `ReadOnlySpan<BufferRange>` / `ReadOnlySpan<Texture>` / `ReadOnlySpan<TextureView>` / `ReadOnlySpan<Sampler>` / `ReadOnlySpan<TopLevelAccelerationStructure>` |
| Timeline waits | `params ReadOnlySpan<CommandSubmission>` |
| Batch transitions | `ReadOnlySpan<TextureTransition>` |

Rationale:
- Accepts stack, array, and single-element inputs uniformly.
- Matches the current `BeginRenderPass`, `SetScissors`, `SetViewports`, and `ResourceTable.Write` family.
- Keeps the future timeline and transition APIs allocation-free at call sites.

Exception:
- Returning multiple elements as `ReadOnlySpan<T>` still implies an owner. Concrete return types remain acceptable when they avoid hidden lifetime coupling.

---

## 2. Explicit Upload / Download / Copy Model

Requirement: CPU data movement remains descriptor-based. Public texture upload does not move back to `ReadOnlySpan<T>`-shaped APIs.

```csharp
public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    public void Upload(Buffer buffer, uint offsetInBytes, BufferData data);

    public void Download(Buffer buffer, uint offsetInBytes, BufferData data);

    public void Upload(Texture texture, TextureSubresource subresource, Offset3D offset, Extent3D extent, TextureData data);

    public void Download(Texture texture, TextureSubresource subresource, Offset3D offset, Extent3D extent, TextureData data);

    public void CopyBuffer(Buffer src, uint srcOffsetInBytes, Buffer dest, uint destOffsetInBytes, uint sizeInBytes);

    public void CopyBufferToTexture(Buffer src, uint srcOffsetInBytes, TextureDataLayout srcLayout, Texture dest, TextureSubresource destSubresource, Offset3D destOffset, Extent3D destExtent);

    public void CopyTexture(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Texture dest, TextureSubresource destSubresource, Offset3D destOffset, Extent3D extent);

    public void CopyTextureToBuffer(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Extent3D srcExtent, Buffer dest, uint destOffsetInBytes, TextureDataLayout destLayout);
}
```

Design points:
- `Buffer.Upload` / `Buffer.Download` short-circuit to `Map()` / `Unmap()` when the buffer is CPU-visible; otherwise they stage through `Context.Copy`.
- `Texture.Upload` / `Texture.Download` always go through `CommandBuffer`, which keeps the copy path explicit and backend-controlled.
- Offsets and pitches are intentionally separate. `TextureDataLayout` describes memory layout; `offsetInBytes` chooses where inside the staging or destination buffer the copy starts.
- `Extensions.ImageSharp` and `Views.Avalonia.Surface` already follow this model today.

---

## 3. Subresource, Offset, Extent, and Buffer Range Values

Requirement: subresource and range inputs remain small value types, aligned with the current code and explicit copy/render surfaces.

```csharp
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
```

Conventions:
- The public surface carries no explicit aspect flag. Backends derive color / depth / stencil from `Texture.Desc.Format`.
- Cube faces remain linearized through `ArrayLayer = cubeIndex * 6 + face`.
- `BufferRange` is the only public buffer subrange shape. No `BufferView` factory comes back.

---

## 4. TextureView Remains a Long-lived Resource

Requirement: keep `TextureView` as an owned resource object. The old "call-site value only" direction is not aligned with the current binding model or extension usage.

```csharp
public record struct TextureViewDesc
{
    public Texture Texture;

    public TextureType Type;

    public PixelFormat Format;

    public TextureSubresourceRange Range;
}

public abstract class TextureView(GraphicsContext context, TextureViewDesc desc) : GraphicsResource(context)
{
    public ref readonly TextureViewDesc Desc => ref desc;
}
```

Design points:
- `GraphicsContext.CreateTextureView(...)` stays on the public surface.
- Binding a plain `Texture` remains the default whole-resource path.
- Binding a `TextureView` is reserved for explicit subresource, dimensionality, or format reinterpretation.
- This matches the current `ImGui` renderer, which keeps view objects alive and binds them through `ResourceTable`.
- Backends are free to let `Texture` carry a default native view internally, but the explicit `TextureView` object remains the public escape hatch.

---

## 5. Flat Binding Model: ResourceBinding[] + ResourceTable

Requirement: the binding model remains flat and concrete: `ResourceBinding[]` describes slots, `ResourceTable` holds values, and `Write(...)` stays typed by resource kind.

```csharp
public enum ResourceType
{
    ConstantBuffer,
    StructuredBuffer,
    StructuredBufferReadWrite,
    Texture,
    TextureReadWrite,
    Sampler,
    AccelerationStructure
}

public record struct ResourceBinding
{
    public ResourceType Type;

    public uint Count;
}

public record struct ResourceTableDesc
{
    public ResourceBinding[] Bindings;
}

public abstract class ResourceTable(GraphicsContext context, ResourceTableDesc desc) : GraphicsResource(context)
{
    public ref readonly ResourceTableDesc Desc => ref desc;

    public abstract void Write(uint binding, Buffer buffer);

    public abstract void Write(uint binding, BufferRange bufferRange);

    public abstract void Write(uint binding, Texture texture);

    public abstract void Write(uint binding, TextureView textureView);

    public abstract void Write(uint binding, Sampler sampler);

    public abstract void Write(uint binding, TopLevelAccelerationStructure topLevelAccelerationStructure);

    public abstract void Write(uint binding, ReadOnlySpan<Buffer> buffers);

    public abstract void Write(uint binding, ReadOnlySpan<BufferRange> bufferRanges);

    public abstract void Write(uint binding, ReadOnlySpan<Texture> textures);

    public abstract void Write(uint binding, ReadOnlySpan<TextureView> textureViews);

    public abstract void Write(uint binding, ReadOnlySpan<Sampler> samplers);

    public abstract void Write(uint binding, ReadOnlySpan<TopLevelAccelerationStructure> topLevelAccelerationStructures);
}
```

CommandBuffer-side entry points stay simple:

```csharp
public void SetPipeline(GraphicsPipeline pipeline);

public void SetPipeline(ComputePipeline pipeline);

public void SetPipeline(MeshShadingPipeline pipeline);

public void PushResourceTable(ResourceTable resourceTable);
```

Design points:
- There is no separate `ResourceLayout` object.
- There is no `IBindableResource` polymorphic entry.
- `ResourceBindings` on pipeline descriptors and `Bindings` on `ResourceTableDesc` intentionally share the same flat shape.
- `Texture` and `TextureView` both remain first-class write targets because "default whole-resource binding" and "explicit view binding" are both used in current code.
- Record all `Write(...)` operations before `PushResourceTable(...)`; the backend binds the table's current contents against the current pipeline.
- This maps cleanly to DX12 descriptor tables, Vulkan push descriptors, and Metal encoder-side resource table binding.

---

## 6. Inline RenderPass, Output, SwapChain Targets, and View Integration

Requirement: render targets stay inline, and the view layer keeps the "render into a `Texture target`" contract.

```csharp
public record struct Output
{
    public PixelFormat[] ColorAttachments;

    public PixelFormat? DepthStencilAttachment;

    public SampleCount SampleCount;
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

public abstract class SwapChain(GraphicsContext context, SwapChainDesc desc) : GraphicsResource(context)
{
    public ref readonly SwapChainDesc Desc => ref desc;

    public abstract Texture CurrentColorTarget { get; }

    public abstract Texture? CurrentDepthStencilTarget { get; }

    public void Resize(uint width, uint height);

    public void Refresh(Surface surface);
}

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    public void BeginRenderPass(ReadOnlySpan<ColorAttachment> colorAttachments, DepthStencilAttachment? depthStencilAttachment);

    public void EndRenderPass();

    public void SetScissors(ReadOnlySpan<Scissor> scissors);

    public void SetViewports(ReadOnlySpan<Viewport> viewports);
}
```

Design points:
- No `FrameBuffer` object comes back.
- `Output` continues to live on pipeline descriptors and describes attachment compatibility, not a runtime render target object.
- `BeginRenderPass(...)` seeds default scissors / viewports from the attachment size, and callers can override them afterward.
- `SwapChain.CurrentColorTarget` and `CurrentDepthStencilTarget` behave like ordinary `Texture` instances.
- `RenderEventArgs` remains `Texture`-centric: view code passes a target texture into user rendering rather than leaking platform swapchain details into the callback.
- The view layer supports both patterns that already exist in the repo:
  - swapchain-backed targets in WinForms / WPF / WinUI-style views
  - offscreen target + `Texture.Download(...)` present paths such as Avalonia

---

## 7. Explicit Texture Layout Transitions + Lightweight PipelineBarrier

Requirement: add the missing explicit texture layout transition interface and a minimal pipeline barrier for read-write shader hazards. Do not bring back the old full `(stage, access)` matrix.

```csharp
public enum TextureLayout
{
    Undefined,
    Common,
    ShaderResource,
    UnorderedAccess,
    RenderTarget,
    DepthStencilRead,
    DepthStencilWrite,
    CopySource,
    CopyDestination,
    ResolveSource,
    ResolveDestination,
    Present
}

public record struct TextureTransition
{
    public Texture Texture;

    public TextureSubresourceRange Range;

    public TextureLayout Before;

    public TextureLayout After;
}

public enum PipelineBarrierScope
{
    All,
    Draw,
    Compute,
    MeshShading,
    RayTracing,
    Copy,
    AccelerationStructureBuild
}

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    public void Transition(Texture texture, TextureSubresourceRange range, TextureLayout before, TextureLayout after);

    public void Transition(TextureView textureView, TextureLayout before, TextureLayout after);

    public void Transition(ReadOnlySpan<TextureTransition> transitions);

    public void PipelineBarrier(PipelineBarrierScope before, PipelineBarrierScope after);
}
```

Design points:
- `Transition(...)` is the explicit texture state / layout interface. No hidden layout changes remain in copy, sampling, render-pass, or present flows.
- `TextureView` overloads reuse `textureView.Desc.Range`; `textureView.Desc.Format` does not affect layout selection.
- `PipelineBarrier(...)` is the intentionally small UAV-style ordering primitive for cases like "dispatch A writes `RWTexture` / `RWBuffer`, dispatch B touches it again". It does not change layouts and it does not cross queues.
- No public `BufferBarrier` is added in this round. Buffer copy / bind ordering continues to follow command semantics, and explicit user-authored synchronization is reserved for layout transitions and shader read-write hazard isolation.
- Backend mapping is straightforward:
  - DX12: resource-state transitions plus UAV barriers
  - Vulkan: image memory barriers plus lightweight memory barriers
  - Metal: texture usage-state transitions plus encoder memory barriers / fences as needed

---

## 8. Timeline-based Cross-queue Synchronization

Requirement: add a canonical timeline submission model without removing the current convenience wrappers.

```csharp
public enum CommandQueueType
{
    Graphics,
    Compute,
    Copy
}

public readonly record struct CommandSubmission(CommandQueue? Queue, ulong Value)
{
    public bool IsEmpty => Queue is null;

    public void Wait()
    {
        Queue?.Wait(Value);
    }
}

public abstract class CommandQueue(GraphicsContext context, CommandQueueType type) : GraphicsResource(context)
{
    public CommandQueueType Type { get; } = type;

    public CommandBuffer CommandBuffer();

    public void WaitIdle();

    public void Wait(ulong value);

    public abstract ulong GetCompletedValue();
}

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    public CommandQueue Queue { get; } = queue;

    public CommandSubmission Submit(params ReadOnlySpan<CommandSubmission> waits);

    public void Submit(bool waitForCompletion = false);
}

public abstract class SwapChain(GraphicsContext context, SwapChainDesc desc) : GraphicsResource(context)
{
    public abstract CommandSubmission Present(params ReadOnlySpan<CommandSubmission> waits);

    public abstract void Present();
}
```

Design points:
- The timeline path is the canonical low-level model: `Submit(waits)` and `Present(waits)` both return a `CommandSubmission`.
- `Submit(bool waitForCompletion = false)` and parameterless `Present()` stay as convenience wrappers for the current one-queue, one-frame-loop usage already present in `Extensions` and `Views`.
- Each queue owns one monotonic completion timeline.
- `default` / empty submissions are ignored in wait sets.
- `Present(waits)` returns the point at which the presented backbuffer becomes writable again.
- Backend mapping follows the natural primitive on each API:
  - DX12: one `ID3D12Fence` per queue
  - Vulkan: one timeline `VkSemaphore` per queue
  - Metal 4: one `MTLSharedEvent` per queue

---

## 9. Unified Native Handles and a Completed NativeObjectType

Requirement: every public object that wraps a native object keeps using `INativeObject`, and `NativeObjectType` is completed around stable native roles rather than backend-specific cast helpers.

```csharp
public enum NativeObjectType
{
    // DirectX 12 / DXGI
    DxgiFactory,
    DxgiAdapter,
    DxgiSwapChain,
    D3D12Device,
    D3D12CommandQueue,
    D3D12Fence,
    D3D12GraphicsCommandList,
    D3D12PipelineState,
    D3D12RootSignature,
    D3D12Resource,
    D3D12DescriptorHeap,
    D3D12CpuDescriptorHandleRtv,
    D3D12CpuDescriptorHandleDsv,
    D3D12CpuDescriptorHandleSrv,
    D3D12CpuDescriptorHandleUav,
    D3D12CpuDescriptorHandleSampler,
    D3D12QueryHeap,

    // Metal / Metal 4
    MtlDevice,
    Mtl4CommandQueue,
    Mtl4CommandBuffer,
    MtlSharedEvent,
    MtlTexture,
    MtlBuffer,
    MtlSamplerState,
    MtlRenderPipelineState,
    MtlComputePipelineState,
    MtlDepthStencilState,
    MtlHeap,
    MtlAccelerationStructure,
    CaMetalLayer,
    CaMetalDrawable,

    // Vulkan
    VkInstance,
    VkPhysicalDevice,
    VkSurfaceKHR,
    VkSwapchainKHR,
    VkDevice,
    VkQueue,
    VkQueueFamilyIndex,
    VkSemaphore,
    VkCommandBuffer,
    VkPipeline,
    VkPipelineLayout,
    VkShaderModule,
    VkImage,
    VkImageView,
    VkBuffer,
    VkSampler,
    VkDescriptorSet,
    VkQueryPool,
    VkAccelerationStructureKHR
}

public interface INativeObject
{
    /// <summary>Returns 0 when the requested native role does not match this object or the active backend.</summary>
    nint GetNativeObject(NativeObjectType type);
}
```

Design points:
- The enum is organized around stable native roles that map cleanly to current Zenith object categories.
- One public object may expose several native roles. For example, a DX12 pipeline may expose both `D3D12PipelineState` and `D3D12RootSignature`.
- Several Zenith object kinds may expose the same native role. For example, DX12 buffers, textures, and acceleration structures all surface `D3D12Resource`.
- Non-pointer scalars such as `VkQueueFamilyIndex` still flow through the same `nint` entry point.
- `GetNativeObject(...)` does not add reference counts or retain ownership. Returned handles are only valid while the Zenith object is alive.
- Returning `0` is the only degradation path; unsupported combinations do not throw.

---

## Appendix A: Current Upload Path

```csharp
CommandBuffer commandBuffer = context.Copy.CommandBuffer();

commandBuffer.Upload(texture,
                     default,
                     default,
                     new() { Width = width, Height = height, Depth = 1 },
                     new()
                     {
                         Pointer = pixels,
                         Layout = new()
                         {
                             SizeInBytes = sizeInBytes,
                             RowPitchInBytes = rowPitchInBytes,
                             SlicePitchInBytes = slicePitchInBytes
                         }
                     });

commandBuffer.Submit(true);
```

This matches the current `ImageSharp` extension and the current offscreen-view presentation path.

## Appendix B: Current Binding Path

```csharp
ResourceBinding[] bindings =
[
    new() { Type = ResourceType.ConstantBuffer, Count = 1 },
    new() { Type = ResourceType.Texture, Count = 1 },
    new() { Type = ResourceType.Sampler, Count = 1 }
];

ResourceTable table = context.CreateResourceTable(new() { Bindings = bindings });
table.Write(0, constants);
table.Write(1, textureView);
table.Write(2, sampler);

commandBuffer.SetPipeline(pipeline);
commandBuffer.PushResourceTable(table);
```

This matches the current `ImGui` extension: long-lived `ResourceTable`, explicit `Write(...)`, then `PushResourceTable(...)` under the current pipeline.

## Appendix C: Timeline-shaped Multi-queue Flow

```csharp
CommandSubmission copyDone = copyCommandBuffer.Submit();

CommandSubmission graphicsDone = graphicsCommandBuffer.Submit(copyDone);

CommandSubmission presentDone = swapChain.Present(graphicsDone);
```

The existing `Submit(true)` / `Present()` convenience calls remain valid for the simple single-queue path; `CommandSubmission` only becomes necessary once work crosses queues or frames.