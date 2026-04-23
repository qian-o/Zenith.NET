# RHI Redesign — Requirements Specification

> Working spec for the redesigned public surface. Not for commit. Mirrors `rhi-redesign.zh.draft.md`.
> The document is organized as nine numbered requirements; shared types live in §0.

## 0. Common Conventions

### 0.1 Naming and Code Style

- Every public value type is a `record struct` with public mutable fields.
- Multi-element parameters use `ReadOnlySpan<T>` / `params ReadOnlySpan<T>`. No `T[]`, no `IEnumerable<T>`.
- All method bodies use `{ ... }`. **Exception:** `ref` / `ref readonly` returning properties keep `=> ref _field;`.
- Every byte-denominated parameter or field carries the `*InBytes` suffix.
- Backend hooks use the `*Core` suffix only when they share a name with a non-`Core` wrapper (e.g. `Wait` / `WaitCore`); otherwise they keep their natural name.
- Every abstract member is plain `protected abstract`; same-assembly callers go through an `internal` wrapper.

### 0.2 Public Surface Boundaries

- No global-memory-barrier shape beyond the resource-less `Barrier(...)` form.
- No residency primitives (`MakeResident` / `Evict`) on the public surface.
- No long-lived `View` objects; view information is a call-site value.
- No long-lived `FrameBuffer` object.
- The only public synchronization result type is `CommandSubmission`.
- Texture image-layout (DX12 / VK) is fully internal to the backend; it is computed from `BarrierAccess` plus RenderPass attachment metadata.

### 0.3 Resource Handle Bases

```csharp
public abstract class GraphicsResource(GraphicsContext context) : DisposableObject, INativeObject
{
    public GraphicsContext Context { get; } = context;

    public abstract nint GetNativeObject(NativeObjectType type);
}

public abstract class Buffer(GraphicsContext context, BufferDesc desc) : GraphicsResource(context)
{
    private BufferDesc desc = desc;

    public ref readonly BufferDesc Desc => ref desc;

    /// <summary>
    /// Maps the buffer into a CPU-visible region. Only valid when <c>Desc.Flags</c> contains
    /// <see cref="BufferUsageFlags.MapRead"/> or <see cref="BufferUsageFlags.MapWrite"/>;
    /// must be paired with <see cref="Unmap"/>.
    /// </summary>
    public abstract MappedMemory Map();

    public abstract void Unmap();
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

### 0.4 BufferDesc / Capability Flags

Follows the existing framework design; no `MemoryAccess` enum is introduced. CPU visibility is expressed within the capability set via `BufferUsageFlags.MapRead` / `MapWrite`.

```csharp
[Flags]
public enum BufferUsageFlags
{
    None                  = 0,
    Vertex                = 1 << 0,
    Index                 = 1 << 1,
    Indirect              = 1 << 2,
    AccelerationStructure = 1 << 3,
    Constant              = 1 << 4,
    ShaderResource        = 1 << 5,
    UnorderedAccess       = 1 << 6,
    MapRead               = 1 << 7,
    MapWrite              = 1 << 8
}

public record struct BufferDesc
{
    public uint SizeInBytes;

    public uint StrideInBytes;

    public BufferUsageFlags Flags;
}
```

`BufferUsageFlags` is a **capability set**, immutable for the lifetime of the resource. It is layered against §6 `BarrierAccess`: `Flags` declares "which accesses are permitted", `BarrierAccess` describes "which access is happening right now". A buffer declared `Vertex | ShaderResource` can be bound directly via `SetVertexBuffer` — there is no "type conversion" step; barriers only carry visibility between previous and next access.

CPU upload paths split two ways:
- A buffer that declares `MapRead` / `MapWrite` is written application-side via paired `Map()` / `Unmap()`.
- Otherwise, writes flow through `CommandBuffer.Upload` / `CopyBuffer` against a staging buffer.

---

## 1. ReadOnlySpan over Arrays

Requirement: every multi-element input parameter on the public API uses `ReadOnlySpan<T>` / `params ReadOnlySpan<T>`. No `T[]`, `IList<T>`, `IEnumerable<T>`, or `IReadOnlyList<T>` appears in public signatures.

Rationale:
- Pairs naturally with .NET 10's `params ReadOnlySpan<T>` and collection expressions `[ ... ]`, giving zero-allocation call sites.
- The same signature accepts stack-allocated, array-backed, or single-element inputs uniformly.
- Backends can copy directly into native arrays without enumerating.

Surfaces (non-exhaustive):

| Parameter | Old | New |
|---|---|---|
| Submit waits | `Fence[]` | `ReadOnlySpan<CommandSubmission>` |
| Vertex buffer slots | `Buffer[]` + `ulong[]` | `ReadOnlySpan<Buffer>` + `ReadOnlySpan<ulong>` |
| Viewports / scissors | `Viewport[]` | `ReadOnlySpan<Viewport>` |
| RenderPass color attachments | `ColorAttachment[]` | `ReadOnlySpan<ColorAttachment>` |
| ResourceTable array bindings | `IBindableResource[]` | `ReadOnlySpan<BufferRange>` / `ReadOnlySpan<TextureView>` / `ReadOnlySpan<Sampler>` |
| Upload data | `byte[]` + offset/size | `ReadOnlySpan<T> where T : unmanaged` |

Exception: returning multiple elements as `ReadOnlySpan<T>` requires a backing owner that is more expensive than the convenience is worth. Single-element returns keep their concrete type; bulk returns keep `T[]` or expose indexed accessors.

---

## 2. Cross-queue Synchronization

Requirement: cross-queue dependencies are expressed through a single value type `CommandSubmission`, modelled after the timeline primitive in all three backends.

```csharp
public enum CommandQueueType
{
    Graphics,
    Compute,
    Copy
}

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

public abstract class CommandQueue(GraphicsContext context, CommandQueueType type) : GraphicsResource(context)
{
    public CommandQueueType Type { get; } = type;

    /// <summary>Acquires a recording-ready CommandBuffer, recycling a retired one when available.</summary>
    public CommandBuffer CommandBuffer();

    /// <summary>Waits until every submission issued on this queue has completed. Idempotent.</summary>
    public void WaitForIdle();

    /// <summary>Waits until this queue's timeline reaches <paramref name="value"/>.</summary>
    public void Wait(ulong value);

    protected abstract ulong GetCompletedValue();

    protected abstract void WaitCore(ulong value);

    protected abstract void SubmitCore(CommandBuffer commandBuffer,
                                       ReadOnlySpan<CommandSubmission> waits,
                                       ulong signalValue);

    protected abstract CommandBuffer CreateCommandBuffer();
}

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    public CommandQueue Queue { get; } = queue;

    /// <summary>Closes recording and enqueues this buffer on its owning queue.</summary>
    public CommandSubmission Submit(params ReadOnlySpan<CommandSubmission> waits);
}
```

Design points:
- Each queue owns one monotonic completion timeline. Each `Submit` advances `Value` and exposes the new point as `CommandSubmission(queue, value)`.
- Cross-queue dependency = pass an upstream `CommandSubmission` into a downstream `Submit(waits)`.
- `GetCompletedValue()` is a method, not a property: all three backends are call-shaped.
- CommandBuffer pooling is a queue-internal detail; the public surface only offers `CommandBuffer()` + `Submit(...)`.
- `GraphicsContext.Graphics` / `Compute` / `Copy` are the three queues; `Present` always runs on `Graphics`.

Backend timeline mapping:

| Backend | Timeline object | API |
|---|---|---|
| Metal 4 | one `MTLSharedEvent` per queue | `commandBuffer.EncodeSignalEvent(event, value)` / `event.SignaledValue` |
| Vulkan 1.4 | one timeline `VkSemaphore` per queue | `vkQueueSubmit2` `pSignalSemaphoreInfos[].value` / `vkGetSemaphoreCounterValue` |
| DX12 | one `ID3D12Fence` per queue | `queue.Signal(fence, value)` / `fence.GetCompletedValue()` |

---

## 3. Subresource / Offset / Range as Value Types

Requirement: replace the legacy `TextureSlice` / `TextureOffset` / `TextureExtent` with `TextureSubresource` / `TextureSubresourceLayers` / `TextureSubresourceRange` / `Offset3D` / `Extent3D`. The triplet expresses single-point / single-mip-multiple-layers / multi-mip-multi-layer ranges respectively.

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
```

| Type | Shape | Used by |
|---|---|---|
| `TextureSubresource` | one mip × one layer | RTV / DSV / Resolve endpoints |
| `TextureSubresourceLayers` | one mip × contiguous layer range | Copy / Upload |
| `TextureSubresourceRange` | contiguous mip range × contiguous layer range | Barrier / `TextureView.Range` |

Conventions:
- The public surface does not carry an aspect field; backends derive aspect (color / depth / stencil) from `Texture.Format`.
- Cube faces are addressed as `ArrayLayer = cubeIndex * 6 + face`; there is no separate face axis.
- `Texture` implicitly converts to `TextureSubresourceRange` (full coverage) so common single-resource calls fit on one line.
- Suffix naming follows VK: `Subresource` (point) / `SubresourceLayers` (slab) / `SubresourceRange` (volume).

---

## 4. Drop BufferView, unify on BufferRange

Requirement: remove the `BufferView` type and any `CreateBufferView` entry point. Buffer sub-ranges are expressed as a call-site `BufferRange` value.

```csharp
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

Rationale:
- Buffers do not need cross-format reinterpretation; offset/size/stride is the full set required to describe a sub-range.
- DX12 `D3D12_*_VIEW_DESC`, VK `VkDescriptorBufferInfo`, Metal `setBuffer:offset:` all take call-site offset/size; a long-lived view object is unnecessary.
- Shader-side interpretation (CBV / structured-SRV / byteaddress-SRV / typed-SRV / UAV) is decided by the slot's `ResourceLayout`, not by the buffer.
- The implicit conversion keeps single-resource calls like `Write(0, vbo)` and `SetVertexBuffer(0, vbo, 0)` on a single line.

---

## 5. TextureView Refactor (Format / ViewType)

Requirement: redefine `TextureView` as a call-site value. Drop swizzle (5-tuple → 4-tuple) and add `Format` / `ViewType` for compatible-family reinterpretation and dimensionality switches.

```csharp
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

public record struct TextureView
{
    public Texture Texture;

    public TextureSubresourceRange Range;

    /// <summary><c>null</c> derives from <c>Texture.Desc</c> + <c>Range</c>.</summary>
    public TextureViewType? ViewType;

    /// <summary><c>null</c> uses <c>Texture.Desc.Format</c>; otherwise reinterprets within the same family.</summary>
    public PixelFormat? Format;

    public static implicit operator TextureView(Texture texture)
    {
        return new()
        {
            Texture = texture,
            Range = texture,
            ViewType = null,
            Format = null
        };
    }
}
```

- Mirrors VK `VkImageViewCreateInfo` and Metal `newTextureViewWithPixelFormat:textureType:levels:slices:`.
- Backends lazily cache native views keyed by `(Texture, Range, ViewType, Format)`; that cache is an implementation detail.
- Channel swizzle is intentionally not on the public surface: single-channel → grayscale and similar patterns are handled in shader; BGRA ↔ RGBA is covered by `Format` reinterpretation within the same compatibility family. If a real need surfaces later, an optional `ComponentMapping?` field can be added back without breaking existing call sites.
- `Texture` implicitly converts to `TextureView` (full range, original format, original dimensionality) so common single-resource bindings fit on one line.

---

## 6. Barrier — 1:1 Synchronization Primitive

Requirement: remove every implicit layout transition. All visibility / execution dependency / layout switches are stated by the caller through a `(stage, access)` pair, mirroring Metal 4 / VK 1.4 / DX12 Enhanced Barriers.

```csharp
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
```

The CommandBuffer barrier entry points come in three shapes:

```csharp
public void Barrier(BarrierStage afterStages, BarrierAccess afterAccess,
                    BarrierStage beforeStages, BarrierAccess beforeAccess);

public void BufferBarrier(BufferRange range,
                          BarrierStage afterStages, BarrierAccess afterAccess,
                          BarrierStage beforeStages, BarrierAccess beforeAccess);

public void TextureBarrier(TextureView view,
                           BarrierStage afterStages, BarrierAccess afterAccess,
                           BarrierStage beforeStages, BarrierAccess beforeAccess);
```

Design points:
- `BarrierStage` describes **when** in the pipeline an access happens; `BarrierAccess` describes **what kind** of access it is. They mirror Metal 4 `MTL4RenderStages` + `MTL4VisibilityOptions`, VK 1.4 `VkPipelineStageFlags2` + `VkAccessFlags2`, DX12 `D3D12_BARRIER_SYNC` + `D3D12_BARRIER_ACCESS`.
- All three forms share the same parameter order: `(afterStages, afterAccess) → (beforeStages, beforeAccess)`.
- Texture image-layout: maintained internally by the backend, derived from `BarrierAccess` plus RenderPass attachment metadata. There is no public layout enum.
- The public surface does **not** offer simplified shortcuts; the caller must supply explicit stage + access.
- Adjacent same-stage / same-resource barriers are folded to no-ops by the backend; the caller is not required to deduplicate.

---

## 7. Drop FrameBuffer — Inline RenderPass + SwapChain Targets

Requirement: remove `FrameBuffer` / `RenderPassInfo` long-lived objects. RenderPass is expressed inline via `BeginRenderPass` / `EndRenderPass`; `SwapChain` directly exposes `CurrentColorTarget` / `CurrentDepthStencilTarget`, so backbuffers are shaped like ordinary `Texture` objects.

```csharp
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

// CommandBuffer
public void BeginRenderPass(ReadOnlySpan<ColorAttachment> colorAttachments,
                            DepthStencilAttachment? depthStencilAttachment);

public void EndRenderPass();
```

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
    /// Submits a present on the GraphicsQueue, waiting for <paramref name="waits"/>;
    /// the returned <see cref="CommandSubmission"/> is the next-backbuffer-writable point.
    /// </summary>
    public abstract CommandSubmission Present(params ReadOnlySpan<CommandSubmission> waits);

    public void Resize(uint width, uint height);

    public void Refresh(Surface surface);
}
```

Design points:
- Backbuffers are exposed as ordinary `Texture` instances (color + optional depth-stencil); they participate in `BeginRenderPass`, `TextureBarrier`, `CopyTexture`, `Write(...)` like any other texture.
- `BeginRenderPass` takes an attachment span; pass `null` explicitly when there is no depth target. The backend defaults `SetViewports` / `SetScissors` from attachment dimensions; the caller can override with a follow-up call.
- `Present(...)` is symmetric with `Submit(...)`: takes waits, returns a `CommandSubmission`.
- Current image index, acquire / present synchronization primitives are backend-internal.

---

## 8. ResourceTable Overloads + Drop IBindableResource

Requirement: `ResourceTable.Write` is overloaded by resource type (`BufferRange` / `TextureView` / `Sampler`); the public surface no longer routes through an `IBindableResource` polymorphic entry.

```csharp
public abstract class ResourceTable(GraphicsContext context, ResourceLayout layout) : GraphicsResource(context)
{
    public ResourceLayout Layout { get; } = layout;

    public void Write(uint binding, BufferRange range);

    public void Write(uint binding, TextureView view);

    public void Write(uint binding, Sampler sampler);

    public void Write(uint binding, ReadOnlySpan<BufferRange> ranges);

    public void Write(uint binding, ReadOnlySpan<TextureView> views);

    public void Write(uint binding, ReadOnlySpan<Sampler> samplers);

    protected abstract void WriteCore(uint binding, ReadOnlySpan<BufferRange> ranges);

    protected abstract void WriteCore(uint binding, ReadOnlySpan<TextureView> views);

    protected abstract void WriteCore(uint binding, ReadOnlySpan<Sampler> samplers);
}
```

CommandBuffer-side entry points:

```csharp
public void SetPipeline(Pipeline pipeline);

public void PushResourceTable(ResourceTable table);
```

Design points:
- The §4 / §5 implicit conversions on `Buffer` / `Texture` keep single-resource bindings on one line: `table.Write(0, vbo)` / `table.Write(1, tex)`.
- Buffer interpretation (CBV / SRV-structured / SRV-byteaddress / UAV / typed) is decided by the slot's `ResourceLayout`, not by the supplied `BufferRange`.
- One table per pipeline (aligned with the Metal 4 argument-table model).
- `PushResourceTable` does not take a stages parameter: stages live on each binding inside the layout.
- **Push-snapshot semantics**: `PushResourceTable` snapshots the table's contents into the command buffer at the call site; later `Write`s on the same `ResourceTable` do not affect already-pushed bindings, so a single `ResourceTable` can be `Write` + `Push`-ed repeatedly within a frame.
- Validation: UAV slots require the corresponding `UnorderedAccess` capability bit; shape / layout / format-family mismatches are reported at `Write` time.

Why no `IBindableResource`:
- The three resource kinds have entirely different native write paths (descriptor / argument-buffer slot / sampler heap); a polymorphic interface forces a runtime dispatch that overloads avoid at compile time.
- Overloads let the right `Write` route be selected at compile time, with no boxing or runtime type tests.
- Implicit `Buffer → BufferRange` / `Texture → TextureView` conversions already cover "I just want to pass the handle" ergonomics.

---

## 9. INativeObject — Unified Native Handle Entry

Requirement: every public type that wraps a native object implements `INativeObject` and exposes the underlying handle through `GetNativeObject(NativeObjectType)`. The public surface carries no platform-conditional properties.

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

    // Metal 4
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
    /// <summary>Returns 0 when the requested type does not match this object or the active backend.</summary>
    nint GetNativeObject(NativeObjectType type);
}

public abstract class GraphicsContext : DisposableObject, INativeObject
{
    public abstract nint GetNativeObject(NativeObjectType type);
}

// GraphicsResource already implements INativeObject in §0.3.
```

Design points:
- Naming follows native API prefixes: DX12 splits into `Dxgi` / `D3D12`; Metal 4 disambiguates from legacy Metal via `Mtl` / `Mtl4`; Vulkan uniformly uses `Vk`.
- `D3D12CpuDescriptorHandle*` is split per view kind to keep the entry point unambiguous.
- Non-handle scalars (e.g. `VkQueueFamilyIndex`) flow through the same entry, packed into `nint`.
- Every `GraphicsResource` subclass implements the interface; subclasses that have nothing to expose return 0 for every requested type.
- Callers are responsible for handling the "returned 0" degraded path; the public surface does not throw.

---

## Appendix A: Typical Frame Loop (combines §2 / §6 / §7)

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

Each step has the same shape: take a set of `CommandSubmission` waits, return one `CommandSubmission`. `Submit` and `Present` are symmetric on the public surface.
