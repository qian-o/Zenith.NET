# RHI Redesign

This document defines the proposed evolution of Zenith.NET's RHI: queue submission, synchronization, resource transitions, the resource and subresource model, render passes, and the swapchain. It is a temporary design note for API iteration and does not describe the current implementation.

The redesign targets five deficiencies in the current RHI:

1. **No cross-queue submission synchronization.** `WaitIdle()` is the only available primitive.
2. **No explicit resource state transition API.** Backends transition resources implicitly inside individual operations, which a render dependency graph (RDG) cannot rely on.
3. **`FrameBuffer` is a heavy long-lived object** that does not match the inline render-pass model used by modern APIs (`vkCmdBeginRendering`, `ID3D12GraphicsCommandList4::BeginRenderPass`, `MTLRenderPassDescriptor`), and it forces `SwapChain` to expose a `FrameBuffer`.
4. **The `Resource → View` two-level binding model duplicates work.** Every `Buffer` / `Texture` constructs a default `View` (DX12 lazily allocates CBV+SRV+UAV descriptor tokens; Vulkan eagerly allocates a full-range `VkImageView`); creating an explicit `BufferView` / `TextureView` for the same range allocates a second set. `BufferView` / `TextureView` are public types that almost never need to be public.
5. **Subresource shapes are bespoke.** `TextureSlice { MipLevel, ArrayLayer, Face }` carries a `Face` axis none of the three target APIs has, and is the same type for both copy addressing and view addressing.

The end goal is a small, canonical RHI that maps 1:1 to DirectX 12, **Vulkan 1.3**, and Metal 4, that an RDG layer can consume without further breaking changes, and that third-party adapters (ImGui backends, asset loaders, profilers) can drive without translation.

## API-Surface Conventions

- All value types are `record struct` with public mutable fields. No `ref struct`.
- No `in` parameters anywhere on the public RHI. Small structs (≤ 16 B) are cheaper to pass by value; uniformity beats marginal perf wins on larger structs.
- All multi-element parameters are `ReadOnlySpan<T>` (or `params ReadOnlySpan<T>` in trailing position). No `T[]`, no `IEnumerable<T>`, no `params T[]`.
- All method bodies use `{ ... }`, never expression-bodied `=>`. **Exception:** `ref` / `ref readonly` returning properties keep the `=> ref _field;` form (e.g. `public ref readonly TDesc Desc => ref desc;`), which has no equivalent block syntax that reads as cleanly.
- Every byte-denominated parameter or field carries the `*InBytes` suffix (`OffsetInBytes`, `SizeInBytes`, `RowPitchInBytes`, ...). Index-style identifiers (`slot`, `firstSlot`, `binding`) follow standard graphics terminology and stay unsuffixed.
- Abstract hooks that backends override carry the `*Core` suffix. This is a project-wide rename of every existing `*Impl` member (`WaitIdleImpl`, `SubmitImpl`, `SetImpl`, `PreprocessImpl`, `GetResultsImpl`, `ResizeImpl`, `RefreshImpl`, `CopyBufferImpl`, etc.). Where appropriate, `*Core` is exposed as a property (`CompletedValueCore`).
- The only public synchronization result type is `CommandSubmission`. Both `CommandBuffer.Submit` and `SwapChain.Present` return it.
- Existing enums and types are reused, not redefined. In particular, the existing `ShaderStageFlags` (`None / Vertex / Pixel / Compute / Amplification / Mesh`, `[Flags]`) is the stage selector for `PushResourceTable`; this design adds no new stage enum.

## Goals

- Keep frame-loop user code short and readable.
- Avoid per-submit, per-pass, and per-bind managed allocations on the hot path.
- Support cross-queue synchronization between `Graphics`, `Compute`, and `Copy`.
- Expose explicit resource transitions for the two shader-side states an RDG actually needs.
- Replace `FrameBuffer` with attachments passed directly to `BeginRenderPass`.
- Have `SwapChain` expose backbuffer textures and a single uniform `Present(...)` returning a `CommandSubmission`.
- Reduce public RHI handles to `Buffer`, `Texture`, `Sampler`.
- Express sub-range information at the **call site** as a value type, not as a long-lived resource.
- Cache backend descriptors / `VkImageView`s / `MtlTexture` slices on the parent resource keyed by the value-type range, so identical bindings share one backend object.

## Non-Goals

- Implicit / tracked resource state inside the RHI beyond a single "current state" cache used to compute the source of explicit transitions.
- One synchronization object per submission.
- Using `WaitForIdle()` as the primary synchronization model.
- Building the full render dependency graph in this iteration.
- Bindless / descriptor-buffer paths.
- Removing `ResourceTable` / `ResourceLayout`. Those are independent and stay.
- Reworking `Sampler`, pipeline state, or shader IO in this iteration.
- Exposing per-aspect (color / depth / stencil) addressing on the public surface; backend infers aspect from `Texture.Format`.
- Exposing sub-range transitions; that is an RDG concern handled through internal backend primitives.
- Owning a "first-frame image ready" wait inside `SwapChain`; the caller is responsible for seeding the first submit.

---

## Part 1 — Queue Synchronization

### Model Summary

Each `CommandQueue` owns one **monotonic completion timeline** that backs every submission to that queue.

- A submission produces a `CommandSubmission` value identifying one point on its queue's timeline.
- Cross-queue dependencies are expressed by passing prior `CommandSubmission` values to the next `Submit` call.
- The timeline object itself is **not** a public type. Backends override timeline operations directly on `CommandQueue`.
- A separate **binary host fence** type stays internal to backends for swapchain image acquire / present, where binary semantics are mandatory (Vulkan).

The naming is deliberate: the term *fence* retains its Vulkan / DX12 binary meaning and is only used internally. The timeline behavior lives on `CommandQueue`. There is no public type named `Fence` for general queue synchronization.

### Public API

```csharp
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

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    public CommandSubmission Submit(params ReadOnlySpan<CommandSubmission> waits)
    {
        return queue.Submit(this, waits);
    }
}
```

`CommandSubmission` reaches into `CompletedValueCore` and `WaitCore` directly via `protected internal` access — there is no public `CommandQueue.Wait(ulong)` method to wrap them. `default(CommandSubmission)` carries a null queue reference and is treated as "already complete / no wait needed".

### `CommandQueue` Surface

The public surface is intentionally narrow: pool a command buffer, wait for the queue to drain, submit. Per-value waits are not part of the public surface — they are reached by calling `submission.Wait()`, which routes through `protected internal` hooks on the queue.

```csharp
public abstract class CommandQueue(GraphicsContext context, CommandQueueType type) : GraphicsResource(context)
{
    private readonly Lock @lock = new();
    private readonly Queue<CommandBuffer> available = [];
    private readonly Queue<InFlightCommandBuffer> execution = [];

    private ulong nextValue = 1;
    private ulong lastSignaledValue;

    public CommandQueueType Type { get; } = type;

    /// <summary>Borrow a command buffer from the pool; the returned buffer has already had Begin called.</summary>
    public CommandBuffer CommandBuffer()
    {
        using Lock.Scope _ = @lock.EnterScope();

        CollectCompleted();

        CommandBuffer commandBuffer = available.Count is 0 ? CreateCommandBufferCore() : available.Dequeue();

        commandBuffer.Begin();

        return commandBuffer;
    }

    /// <summary>Block until every submission on this queue has completed.</summary>
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

    /// <summary>Latest value the timeline has signalled to. Reachable from <see cref="CommandSubmission"/> in the same assembly.</summary>
    protected internal abstract ulong CompletedValueCore { get; }

    protected abstract CommandBuffer CreateCommandBufferCore();

    protected abstract void SubmitCore(CommandBuffer commandBuffer, ReadOnlySpan<CommandSubmission> waits, ulong signalValue);

    /// <summary>Block until the timeline reaches <paramref name="value"/>. Reachable from <see cref="CommandSubmission"/> in the same assembly.</summary>
    protected internal abstract void WaitCore(ulong value);

    private readonly record struct InFlightCommandBuffer(CommandBuffer CommandBuffer, ulong Value);
}
```

### Why Not a Public `Fence` / `Timeline` Type

The timeline is in 1:1 correspondence with its queue. There is no scenario where a user holds a fence independent of its queue, so promoting it to a public type only adds API surface and collides with the Vulkan binary `VkFence` (which has no value).

Folding the timeline into `CommandQueue` keeps the public surface minimal and removes the naming conflict with the binary fence used by swapchain code.

### Backend Mapping

| Backend | Timeline object | Submit | `CompletedValueCore` | `WaitCore` |
|---|---|---|---|---|
| DX12 | one `ID3D12Fence` per queue | for each wait `queue.Wait(otherFence, otherValue)` → `ExecuteCommandLists` → `queue.Signal(fence, value)` | `fence.GetCompletedValue()` | `SetEventOnCompletion` + `WaitOne` |
| Vulkan 1.3 | one **timeline** `VkSemaphore` per queue (core in 1.2, ubiquitous in 1.3) | `VkTimelineSemaphoreSubmitInfo` packs wait `(semaphore, value)` pairs and the signal value into `VkSubmitInfo` | `vkGetSemaphoreCounterValue` | `vkWaitSemaphores` |
| Metal 4 | one `MTLSharedEvent` per queue | for each wait the command buffer encodes `EncodeWait(otherEvent, otherValue)`; before commit it encodes `EncodeSignal(event, value)` | `event.SignaledValue` | `event.Wait(value, timeout)` |

The existing internal `VKFence` (binary `VkFence`) remains, used only by swapchain image acquire on Vulkan.

### Usage

```csharp
// Single-queue
graphicsCmd.Submit().Wait();

// Cross-queue
CommandSubmission upload = copyCmd.Submit();
CommandSubmission cull   = computeCmd.Submit(upload);
CommandSubmission frame  = graphicsCmd.Submit(cull);
frame.Wait();
```

---

## Part 2 — Canonical Subresource Types

The following value types replace the single `TextureSlice`. They are used by transitions (Part 3), bindings (Part 5), command-buffer copies (Part 6), and render-pass attachments (Part 7).

There are exactly two texture-subresource shapes:

- `TextureSubresource` — single mip × single array layer. Used by RTV / DSV / `ResolveTexture`.
- `TextureSubresourceRange` — contiguous mip range × contiguous array-layer range. Used by views, transitions, and copy / upload (which validate `LevelCount == 1` at the call site).

There is no public `TextureAspect` type — aspect (color vs depth vs stencil) is derived from `Texture.Format` by every backend that needs it. Stencil-only sampling is a rare, advanced case left to a future opt-in extension.

### `TextureSubresource` — single subresource

```csharp
public record struct TextureSubresource
{
    public uint MipLevel;

    public uint ArrayLayer;
}
```

Identifies exactly one subresource. Used by render-pass attachments (a render target is always a single mip × single array layer) and by `ResolveTexture`. Cube faces use `ArrayLayer = cubeIndex * 6 + face`; there is no `Face` field.

### `TextureSubresourceRange` — view / transition / copy addressing

```csharp
public record struct TextureSubresourceRange
{
    public uint BaseMipLevel;

    public uint LevelCount;

    public uint BaseArrayLayer;

    public uint LayerCount;
}
```

Maps to:

- Vulkan: `VkImageSubresourceRange` directly (aspect filled in from `Texture.Format`).
- DX12: `D3D12_TEX*_ARRAY_*` view descs (`MostDetailedMip`, `MipLevels`, `FirstArraySlice`, `ArraySize`; `PlaneSlice` derived from format).
- Metal: `MTLTexture.MakeTextureView(levelRange, sliceRange)`.

For copy / upload paths, callers set `LevelCount = 1`; the validation layer rejects multi-mip copies up-front rather than lowering them silently.

### `Offset3D` and `Extent3D`

```csharp
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
```

Used by the `Copy*` overloads to specify source / destination origin and size. Match `VkOffset3D` / `VkExtent3D`; trivially translated to DX12 `D3D12_BOX` and Metal origin / size pairs. `Z` / `Depth` collapse to `0` / `1` for 2D textures.

### `BufferRange`

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

Field naming aligns with `VkDescriptorBufferInfo` (`buffer`, `offset`, `range`), promoted to the `*InBytes` suffix to match `BufferDesc`. `StrideInBytes = 0` falls back to `Buffer.Desc.StrideInBytes`, eliminating the current double-storage of stride.

There is no `BufferViewType` field. The interpretation (constant / structured / byte-address / typed) is fixed by the corresponding `ResourceLayout` slot at bind time. This avoids exposing a knob whose meaning differs between DX12 (which has typed buffers natively) and Metal (which does not) and removes a redundant choice the user would otherwise have to keep in sync with their layout.

---

## Part 3 — Explicit Resource Transitions

### Why the RHI Must Expose Transitions

Every backend has a notion of resource state:

- DX12: `D3D12_RESOURCE_STATES` (or enhanced barriers' `D3D12_BARRIER_LAYOUT` / `D3D12_BARRIER_ACCESS`).
- Vulkan 1.3: `VkImageLayout` + `VkAccessFlags2` + `VkPipelineStageFlags2` (sync2 is core in 1.3).
- Metal: implicit hazard tracking, with explicit options via `MTLResourceUsage` and barriers between encoders.

Today these transitions are emitted opaquely inside per-call paths. An RDG cannot batch or reorder barriers if it cannot see them. The redesign exposes the two shader-side states as first-class commands and leaves every other state implicit.

### `TransitionState`

```csharp
public enum TransitionState
{
    ShaderResource,
    UnorderedAccess
}
```

`TransitionState` is shared by `Texture` and `Buffer`. The two states the user actually has to choose between are the same on both: a sampled / read-only resource (`ShaderResource`) versus an unordered read-write resource (`UnorderedAccess`). Backends pick a sensible initial state per resource at creation time, and the first explicit `Transition` call resolves the source state from the resource's tracked field.

### Why `TransitionState` Has Only Two Values

Every other state is implied by the operation that consumes the resource and is therefore transitioned by the RHI itself, not by the user:

- **Render-target / depth-stencil** — implied by `BeginRenderPass`. The pass transitions every color attachment to render-target state and every depth attachment to depth-stencil state. `EndRenderPass` does **not** auto-revert; the texture stays in render-target state until the user transitions it back, which is what an RDG wants.
- **Copy source / dest** — implied by `Copy*` and `Resolve*` commands.
- **Present** — implied by `SwapChain.Present`. The swapchain transitions the current backbuffer to the platform's present state as part of `Present(...)`.
- **Vertex / index / constant / indirect buffer** — implied by `SetVertexBuffer`, `SetIndexBuffer`, `PushResourceTable` (CBV slot), and `Draw*Indirect` / `Dispatch*Indirect`. DX12 promotes / decays these automatically; Vulkan derives the access mask at barrier time.

The user therefore only writes explicit transitions when moving a resource between **shader-side states** (`ShaderResource` ↔ `UnorderedAccess`) or when bringing a resource out of one of the implicit states back to a shader-side state for the next pass.

### Why There Is No Per-Resource Buffer Enum

Most backends do not need explicit per-state tracking for buffers:

- **DX12** auto-promotes / decays buffer states between `COMMON` and the implicit usage of each operation.
- **Vulkan** has no buffer layout; access is described by `VkAccessFlags2` derived from operation usage at barrier time.
- **Metal** tracks buffer hazards implicitly.

A buffer therefore only needs the same two-state surface a texture exposes — hence the shared `TransitionState`.

### `MemoryBarrier()` and Implicit UAV Tracking

`MemoryBarrier()` is part of the public API. It expresses "make all preceding writes globally visible before any following read" without naming a specific resource — exactly the semantics of a UAV / global memory barrier across shader stages.

In addition to the explicit call, the `CommandBuffer` keeps a small set of UAV resources written by the current dispatch chain and inserts the appropriate barrier when the next dispatch reads or writes one of those resources. The cost is one hash-set lookup per dispatch; the benefit is that the hazard cannot be forgotten in the easy case.

`MemoryBarrier()` is the escape hatch for cases the implicit tracker cannot express — e.g. cross-resource hazards, hazards spanning compute → graphics, or RDG-driven scenarios where the layer wants a coarse global flush.

### Transition Tracking

Each `Texture` and `Buffer` tracks its current `TransitionState`. Tracking lives on the RHI resource so the user only states the **target** state, never the previous one.

- The user calls `cmd.Transition(tex, TransitionState.ShaderResource)`; the RHI reads the source state from the resource's tracked state, emits the backend barrier, and updates tracking.
- An RDG that wants global control can reset tracked state itself before issuing pre-planned barriers.

### Public Commands on `CommandBuffer`

```csharp
public void Transition(Buffer buffer, TransitionState newState);

public void Transition(Texture texture, TransitionState newState);

public void MemoryBarrier();
```

Backends implement three primitives:

```csharp
protected abstract void TransitionCore(Buffer buffer, TransitionState oldState, TransitionState newState);

protected abstract void TransitionCore(Texture texture, TransitionState oldState, TransitionState newState);

protected abstract void MemoryBarrierCore();
```

The public `Transition` commands are deliberately scalar and whole-resource. Each backend coalesces consecutive `Transition` calls into one batched barrier op at the next non-transition command, so users get clean call sites without giving up batched submission to the driver.

There is no public sub-range `Transition(Texture, TextureSubresourceRange, ...)` overload. Sub-range hazard tracking is an RDG concern; backends still expose the necessary internal primitive (`TransitionRangeCore`) for an RDG layer to call directly without touching the public surface.

### Required States for Operations

Each operation either *requires* a user-managed state on its resource argument or *implies* the state itself. Implicit states are transitioned by the operation; the user never writes them.

| Operation | Required state | Source |
|---|---|---|
| `PushResourceTable` SRV resource | `ShaderResource` | user |
| `PushResourceTable` UAV resource | `UnorderedAccess` | user |
| `BeginRenderPass` color attachment | render-target (implicit) | RHI |
| `BeginRenderPass` depth attachment | depth-stencil (implicit) | RHI |
| `CopyBuffer` source / dest | copy source / dest (implicit) | RHI |
| `CopyBufferToTexture` source / dest | copy source / dest (implicit) | RHI |
| `CopyTextureToBuffer` source / dest | copy source / dest (implicit) | RHI |
| `CopyTexture` source / dest | copy source / dest (implicit) | RHI |
| `ResolveTexture` source / dest | copy source / dest (implicit) | RHI |
| `SetVertexBuffer` / `SetIndexBuffer` | vertex / index buffer (implicit) | RHI |
| `PushResourceTable` CBV slot | constant buffer (implicit) | RHI |
| `Draw*Indirect` / `Dispatch*Indirect` argument buffer | indirect argument (implicit) | RHI |
| `Dispatch` reading a UAV that an earlier dispatch wrote | UAV barrier (implicit; user may also call `MemoryBarrier()`) | RHI |
| `SwapChain.Present` backbuffer | present (implicit) | RHI |

The validation layer enforces correctness on `PushResourceTable`: passing a UAV resource in `ShaderResource` or vice versa is a loud error.

### Cross-Queue Ownership

Vulkan requires explicit queue family ownership transfer for resources used on multiple queues with `VK_SHARING_MODE_EXCLUSIVE`. To keep the RHI simple:

- All resources are created with concurrent / shared semantics (Vulkan: `VK_SHARING_MODE_CONCURRENT` across the three queues; DX12 / Metal need no equivalent).
- Cross-queue synchronization is therefore expressed entirely through `CommandSubmission` waits. The RHI does not require ownership-transfer barriers in user code.

Concurrent sharing has a small driver-side cost on Vulkan but eliminates an entire class of barrier bugs, which matches the framework's "easy to use first" stance.

---

## Part 4 — Public Resource Surface

Three public RHI resource handles. Nothing else.

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

Removed from the public surface: `BufferView`, `BufferViewDesc`, `TextureView`, `TextureViewDesc`, `IBindableResource`, `TextureSlice`, `TextureAspect`, `TextureSubresourceLayers`, `BufferViewType`, `FrameBuffer`, `FrameBufferDesc`, `FrameBufferAttachment`, `RenderPassDesc`, `ClearValue`, `ClearValues`, `ClearFlags`.

The backend infers texture aspect from `desc.Format` whenever it needs to fill in a `VkImageAspectFlags` / DX12 plane index. The implicit operator from `Texture` to `TextureSubresourceRange` covers the whole resource.

`TextureType` (1D / 2D / 3D / Cube / CubeArray combined into one enum) stays as it is. Metal merges these the same way (`MTLTextureType` lists `Type1D / Type2D / TypeCube / Type3D / Type1DArray / Type2DArray / TypeCubeArray` in one enum), so the existing shape is canonical, not bespoke.

### Caching on the Parent

Each backend resource holds two private dictionaries:

```csharp
// Conceptual; backend-internal, not exposed.
private Dictionary<BufferRange, T_buffer_view> bufferViewCache;
private Dictionary<TextureSubresourceRange, T_texture_view> textureViewCache;
```

`T_*` is whatever the backend needs:

| Backend | Buffer cache value | Texture cache value |
|---|---|---|
| DX12 | `DXDescriptorToken` per CBV / SRV / UAV variant | `DXDescriptorToken` per SRV / UAV; RTV / DSV come from a separate `TextureSubresource`-keyed pool |
| Vulkan | `VkDescriptorBufferInfo` (no GPU object) | `VkImageView` |
| Metal | `nuint GpuAddress` | `MtlTexture` slice via `MakeTextureView` |

The cache is populated on first bind and reused for every subsequent bind with an equal range. The default range — produced by the implicit operator from `Texture` / `Buffer` — hits the same slot every time, so the typical "bind whole resource" call path materializes one backend object for the lifetime of the resource. The cache is owned by, and disposed with, the parent.

### Cache Invalidation

Resources are immutable in shape (you cannot re-`BufferDesc` an existing `Buffer`), so the cache never needs partial invalidation. `Resize` on a swapchain backbuffer disposes and recreates the underlying `Texture`; the new texture starts with an empty cache.

---

## Part 5 — Resource Table Binding

### Stage-Tagged Push Instead of Set Index

Modelling Vulkan's descriptor sets directly via `SetResourceTable(uint set, ResourceTable table)` maps cleanly to Vulkan but forces DX12 (root signature with per-stage visibility) and Metal (per-stage argument tables) into an artificial set-indexed translation layer. It also does not let the user say "this table is for the fragment stage only", which both DX12 and Metal can express natively.

The design uses a **stage-tagged push** modelled on Metal's `setVertexBuffer:atIndex:` / `setFragmentBuffer:atIndex:` family, generalised across stages via the existing `ShaderStageFlags` enum (`None / Vertex / Pixel / Compute / Amplification / Mesh`, `[Flags]`). The verb is `Push` rather than `Set` to signal that the call is binding-now / push-style, not a long-lived assignment.

### `ResourceTable.Write` — Typed Overloads

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

The implicit operators keep simple call sites a single line:

```csharp
table.Write(0, vertexBuffer);
table.Write(1, albedoTexture);
table.Write(2, sampler);
```

Sub-ranges use a struct literal:

```csharp
table.Write(3, new TextureSubresourceRange
{
    BaseMipLevel = 0,
    LevelCount = 1,
    BaseArrayLayer = 4,
    LayerCount = 1
});

table.Write(4, new BufferRange
{
    Buffer = bigBuffer,
    OffsetInBytes = 256,
    SizeInBytes = 1024,
    StrideInBytes = 32
});
```

Validation enforces shape compatibility against the resource-layout slot (UAV slot requires `BufferUsageFlags.UnorderedAccess` / `TextureUsageFlags.UnorderedAccess`, etc.). There is no marker interface like `IBindableResource`.

### Backend Mapping for `PushResourceTable`

| Backend | Push semantics |
|---|---|
| DX12 | look up the root parameter whose visibility includes any of `stages`; bind the table's descriptor handle there |
| Vulkan 1.3 | one descriptor set per call, layout pre-baked; for shader-stage-only visibility, use `VK_KHR_push_descriptor` or a stage-disjoint set layout |
| Metal 4 | call the matching per-stage encoder method (`setVertexBuffer` / `setFragmentBuffer` / `setComputeBuffer`) for each bit set in `stages` |

---

## Part 6 — Command Buffer Operations

All command-buffer operations take canonical types and consistent naming. Signatures align with Vulkan command names and parameter shapes; DX12 / Metal map directly. No `in` parameters; every multi-element parameter is `ReadOnlySpan<T>`; every byte-denominated parameter carries the `*InBytes` suffix.

### Buffer Operations

```csharp
public void SetVertexBuffer(uint slot, Buffer buffer, ulong offsetInBytes);

public void SetVertexBuffers(uint firstSlot, ReadOnlySpan<Buffer> buffers, ReadOnlySpan<ulong> offsetsInBytes);

public void SetIndexBuffer(Buffer buffer, ulong offsetInBytes, IndexFormat format);

public void CopyBuffer(Buffer source, ulong sourceOffsetInBytes,
                       Buffer destination, ulong destinationOffsetInBytes,
                       ulong sizeInBytes);

public void Upload<T>(Buffer destination, ulong offsetInBytes, ReadOnlySpan<T> data) where T : unmanaged;
```

### Texture Operations

```csharp
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
```

`Copy*` and `Upload<T>(Texture, ...)` paths require `range.LevelCount == 1`; the validation layer rejects multi-mip arguments. `ResolveTexture` always operates on a single subresource per side and therefore takes `TextureSubresource` directly.

### Resource Tables

```csharp
public void PushResourceTable(ShaderStageFlags stages, ResourceTable table);
```

There is no plural `PushResourceTables`; multiple stage targets are handled by setting the appropriate `ShaderStageFlags` bits in one call.

### Transitions

```csharp
public void Transition(Buffer buffer, TransitionState newState);

public void Transition(Texture texture, TransitionState newState);

public void MemoryBarrier();
```

### Viewports / Scissors

```csharp
public void SetViewports(ReadOnlySpan<Viewport> viewports);

public void SetScissors(ReadOnlySpan<Rect> scissors);
```

`SetViewports` / `SetScissors` are first-class command-buffer ops (not derived from any render-pass desc). Single-viewport / single-scissor calls pass a one-element span via `[stackalloc]` or a collection expression. The plural form matches DX12 `RSSetViewports` / `RSSetScissorRects` and Vulkan `vkCmdSetViewport` / `vkCmdSetScissor` natively; on Metal the backend asserts `viewports.Length == 1` and `scissors.Length == 1`.

```csharp
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
```

---

## Part 7 — Inline RenderPass (replaces `FrameBuffer`)

### Why `FrameBuffer` Is Removed

`FrameBuffer` predates dynamic rendering. It forces:

- A long-lived object whose only role is bundling attachment views.
- A separate `SwapChainFrameBuffer` per backend.
- An `Output` description duplicated on `FrameBuffer`, `SwapChain`, and pipelines.

All three target APIs natively accept inline render-pass descriptors. Removing `FrameBuffer` aligns the RHI with that model and makes attachments first-class participants of the resource transition system.

### Attachment Descriptions

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
```

Attachment descs reference `Texture` + `TextureSubresource` (a single mip × single layer — what an RTV / DSV always is). The backend resolves the RTV / DSV through the parent resource's `TextureSubresource`-keyed pool, so identical attachments across frames share one descriptor.

### `BeginRenderPass`

```csharp
public void BeginRenderPass(ReadOnlySpan<ColorAttachmentDesc> colorAttachments,
                            DepthStencilAttachmentDesc? depthStencilAttachment);

public void EndRenderPass();
```

The `depthStencilAttachment` parameter has **no default value**. Callers without a depth-stencil target must pass `null` explicitly. This forces the user to acknowledge the choice at the call site instead of silently dropping the depth attachment because of a missing argument.

The implementation:

1. Transitions every color attachment to render-target state and the depth attachment (if any) to depth-stencil state, using the backend's tracked-state machinery from Part 3. The user does **not** transition attachments before the pass.
2. Calls the backend `BeginRenderPassCore(...)`.

`EndRenderPass()` does **not** auto-revert attachments to a previous state; the texture remains in render-target state until the next user-issued transition (typically to `ShaderResource` for the next pass, or implicitly to present state via `SwapChain.Present`).

`SetViewports` / `SetScissors` are issued separately by the user inside the pass.

### Pipeline `Output`

`GraphicsPipelineDesc.Output` (color formats + depth format + sample count) is unchanged. A pipeline is compatible with any `BeginRenderPass` call whose attachment formats and sample count match its `Output`. This stays the user's responsibility, exactly as today; only the per-frame bundling changes.

### Layered / Multiview

Layered rendering is detected per-attachment from `Texture.Desc.ArrayLayers > 1` combined with the absence of an explicit single-layer `TextureSubresource`. A future extension can add an explicit `LayerCount` argument to `BeginRenderPass` if needed; the current proposal omits it to keep the entry point minimal.

### Backend Mapping

| Backend | Begin | End |
|---|---|---|
| DX12 | `OMSetRenderTargets` (or `BeginRenderPass` with `RENDER_PASS_RENDER_TARGET_DESC` for tile resources) | `EndRenderPass` if used, otherwise nothing |
| Vulkan 1.3 | `vkCmdBeginRendering` with `VkRenderingInfo` built from the attachment span (dynamic rendering is core in 1.3) | `vkCmdEndRendering` |
| Metal 4 | `MTL4CommandBuffer.RenderCommandEncoder(MTLRenderPassDescriptor)` built from the attachment span | encoder `EndEncoding` |

---

## Part 8 — `SwapChain` Without `FrameBuffer`

### Interaction With Queue Synchronization

Every coupling point between swapchain and queues is expressed as a `CommandSubmission`, so no new primitive leaks out. The API surface has no `AcquireNextImage()` and no first-frame helper field. `Present(...)` returns a `CommandSubmission` representing the next backbuffer's readiness, and the very first frame's image-ready submission is the **caller's** responsibility — typically `default(CommandSubmission)`, which `CommandSubmission.Wait()` treats as "no wait needed".

Two edges exist in a typical frame:

1. **Submit → Present**: the present engine must not read the backbuffer until the GPU finishes drawing. `SwapChain.Present(params ReadOnlySpan<CommandSubmission> waits)` accepts the submissions that produced the backbuffer contents and turns them into the appropriate GPU-side wait before scheduling present.
2. **Present → next Submit**: a backbuffer is only safe to write after the present engine releases it. `Present(...)` returns a `CommandSubmission` describing that release point; the next frame's `cmd.Submit(...)` waits on it. The very first frame uses whatever the caller decides — `default(CommandSubmission)` for the simple case, an explicit hand-off submission for advanced flight-frame strategies.

The public API does **not** add a `cmd.Submit(SwapChain)` overload or a `SwapChain.SubmitAndPresent(...)` shortcut. Coupling submit to the swapchain on the API surface would:

- Force every multi-pass frame (e.g. shadow + main + post) to either wrap the whole frame in one submit or pull the swapchain through every layer.
- Conflict with cross-queue scheduling, where the final write to the backbuffer may come from compute, not graphics.
- Be redundant: the existing `CommandSubmission` value already carries the necessary edge through `Present(waits)`.

Keeping submit and present orthogonal — and bridging them through `CommandSubmission` — is what makes this RHI directly reusable by an RDG layer that schedules many passes per frame.

### Public Surface

```csharp
public abstract class SwapChain(GraphicsContext context, SwapChainDesc desc) : GraphicsResource(context)
{
    private SwapChainDesc desc = desc;

    public ref readonly SwapChainDesc Desc => ref desc;

    public abstract uint Width { get; }

    public abstract uint Height { get; }

    /// <summary>Current backbuffer index. Internal — backend code only; not part of the public surface.</summary>
    internal abstract uint CurrentImageIndex { get; }

    public abstract Texture CurrentColorTarget { get; }

    public abstract Texture? CurrentDepthStencilTarget { get; }

    /// <summary>
    /// Submits a present operation that waits on <paramref name="waits"/> on the GPU.
    /// Returns a CommandSubmission that signals when the next frame's backbuffer is safe to write.
    /// The first frame's image-ready value must be supplied by the caller (typically default(CommandSubmission)).
    /// </summary>
    public abstract CommandSubmission Present(params ReadOnlySpan<CommandSubmission> waits);

    public void Resize(uint width, uint height) { /* unchanged */ }

    public void Refresh(Surface surface) { /* unchanged */ }
}
```

The backbuffer is exposed as a plain `Texture` — no view object. Users build attachment descs from `(swapChain.CurrentColorTarget, new TextureSubresource())`, which the backend resolves through the texture's `TextureSubresource`-keyed RTV pool. `CurrentImageIndex` is `internal` because no public API needs it; backend code that does need it (e.g. building per-frame command structures) reaches it through the same-assembly access.

### Frame Loop

```csharp
// First-frame seed: caller decides. default(CommandSubmission) is the simple case.
CommandSubmission imageReady = default;

while (running)
{
    CommandBuffer cmd = ctx.Graphics.CommandBuffer();

    ReadOnlySpan<ColorAttachmentDesc> colors =
    [
        new()
        {
            Texture = swapChain.CurrentColorTarget,
            Subresource = new(),
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

Every step in the loop has the same shape: takes a set of `CommandSubmission` waits, returns a `CommandSubmission` for the next consumer. `Submit` and `Present` are symmetric on the API surface.

### Backend Mapping

| Backend | `Present` | Returned `CommandSubmission` |
|---|---|---|
| DX12 | `IDXGISwapChain::Present`; `waits` are translated into queue `Wait` calls before present | a value derived from the frame-latency waitable object signalling when the next backbuffer is releasable |
| Vulkan 1.3 | `vkQueuePresentKHR` waits on a per-present binary semaphore that the last `CommandSubmission`'s queue signals as part of its submit | adapter that wraps the next-frame `vkAcquireNextImageKHR` binary semaphore so it appears as a wait edge during the next `Submit` |
| Metal 4 | `drawable.Present()` after waiting on the `waits` timeline values | the next drawable's availability event |

Vulkan is the only backend where binary semaphores must coexist with timelines. Internally the Vulkan swapchain holds:

- One binary `VkSemaphore` per in-flight frame (`imageAvailable[N]`) — signaled by `vkAcquireNextImageKHR`, exposed through the value returned by `Present`.
- One binary `VkSemaphore` per in-flight frame (`renderFinished[N]`) — waited by `vkQueuePresentKHR`.
- These binaries are wrapped in a private `CommandSubmission`-compatible adapter so the public API stays uniform across backends.

This isolates the binary-vs-timeline asymmetry inside the Vulkan swapchain, where it belongs.

---

## Migration Notes

The redesign is breaking. Affected APIs:

- `CommandQueue.WaitIdle()` → `CommandQueue.WaitForIdle()`.
- All `*Impl` overrides project-wide → `*Core` (`SubmitImpl→SubmitCore`, `WaitIdleImpl→WaitCore`, `SetImpl→SetCore`, `PreprocessImpl→PreprocessCore`, `GetResultsImpl→GetResultsCore`, `ResizeImpl→ResizeCore`, `RefreshImpl→RefreshCore`, `CopyBufferImpl→CopyBufferCore`, `BeginRenderPassImpl→BeginRenderPassCore`, ...).
- `CommandQueue` exposes no public per-value `Wait(ulong)`. Use `submission.Wait()`. The hooks `CompletedValueCore` (now a property) and `WaitCore(ulong)` are `protected internal` so `CommandSubmission` can call them directly.
- All expression-bodied `=>` members on RHI types are rewritten with `{ ... }` bodies, **except** `ref` / `ref readonly` returning properties (`public ref readonly TDesc Desc => ref desc;`), which keep the `=>` form.
- `CommandBuffer.Submit(bool waitForCompletion)` is removed. Use `Submit()` / `Submit().Wait()`.
- `CommandBuffer.BeginRenderPass(FrameBuffer, ClearValue, params IEnumerable<ResourceTable>)` is removed. Use `BeginRenderPass(ReadOnlySpan<ColorAttachmentDesc>, DepthStencilAttachmentDesc?)`; the depth-stencil parameter has no default value (pass `null` explicitly).
- `FrameBuffer`, `FrameBufferDesc`, `FrameBufferAttachment`, `RenderPassDesc`, `ClearValue`, `ClearValues`, `ClearFlags` are removed.
- `BufferView`, `BufferViewDesc`, `TextureView`, `TextureViewDesc`, `IBindableResource` are removed.
- `TextureSlice`, `TextureAspect`, `TextureSubresourceLayers`, `BufferViewType` are removed.
- `CommandBuffer.Transition(Texture, TextureSubresourceRange, TransitionState)` is **not** part of the public API; the corresponding backend primitive (`TransitionRangeCore`) remains internal for an RDG layer.
- `GraphicsContext.CreateBufferView(...)` / `CreateTextureView(...)` / `CreateFrameBuffer(...)` are removed.
- `ResourceTable.Write(uint, IBindableResource)` and its array / params variants are removed. Use the typed overloads in Part 5.
- `ResourceTable.Preprocess(CommandBuffer)` and the `preprocessResourceTables` parameter on `BeginRenderPass` are removed.
- `CommandBuffer.SetResourceTable(uint set, ResourceTable)` and `SetResourceTables(uint firstSet, ReadOnlySpan<ResourceTable>)` are removed. Use `PushResourceTable(ShaderStageFlags stages, ResourceTable table)` with the existing `ShaderStageFlags` enum.
- `Copy*` and `Upload<T>(Texture, ...)` signatures change to take `TextureSubresourceRange` + `Offset3D` + `Extent3D`. Copy / upload paths require `LevelCount == 1`.
- All byte-denominated parameters now carry the `*InBytes` suffix (`offsetInBytes`, `sizeInBytes`, `sourceOffsetInBytes`, `destinationOffsetInBytes`, `sourceRowPitchInBytes`, `destinationRowPitchInBytes`). `BufferRange` fields rename to `OffsetInBytes`, `SizeInBytes`, `StrideInBytes`.
- `CommandBuffer.SetViewport(Viewport)` and `SetScissor(Rect)` → `SetViewports(ReadOnlySpan<Viewport>)` and `SetScissors(ReadOnlySpan<Rect>)`.
- Render-pass attachment descs replace any `TextureView View` field with `Texture Texture` + `TextureSubresource Subresource`.
- The DX12 `DXBuffer.View` / `DXTexture.View` fields are removed; descriptor allocation moves into the per-range cache on the parent resource.
- The Vulkan auto-created `VKTexture.View` is removed; the cache lazily creates one `VkImageView` per unique `TextureSubresourceRange`.
- `SwapChain.FrameBuffer`, `SwapChain.AcquireNextImage()`, `SwapChain.FirstFrameImageReady`, and the public `SwapChain.CurrentImageIndex` are removed; replaced by `CurrentColorTarget` / `CurrentDepthStencilTarget` / `CommandSubmission Present(...)`. `CurrentImageIndex` is now `internal`.
- Cube faces are no longer addressed by a separate `Face` field. Replace `new TextureSlice { ArrayLayer = i, Face = f }` with `ArrayLayer = i * 6 + f`.
- All `in` parameters on the public RHI are removed.
- Every public method that took a managed array (`T[]`, `params T[]`, `IEnumerable<T>`) now takes `ReadOnlySpan<T>` (or `params ReadOnlySpan<T>` for trailing parameters).

The internal Vulkan `VKFence` (binary fence) is retained for swapchain use only.

---

## RDG and Third-Party Compatibility

This RHI is sufficient for a future render dependency graph to layer on top, and friendly to third-party adapters:

- **RDG.** Each pass is a function that records a section of a `CommandBuffer`. Resource access is declared at pass build time as `Buffer` / `Texture` plus `BufferRange` / `TextureSubresourceRange`. The RDG can compute exact transitions and emit `Transition(...)` / `MemoryBarrier()` calls between passes; the backend coalesces them. For sub-range or per-aspect transitions the RDG calls into the backend's internal primitive (`TransitionRangeCore`) directly; the public surface stays minimal. All range types are `record struct` and therefore equatable, so the RDG can hash them directly when planning aliases. Cross-queue scheduling reuses `CommandSubmission` waits; no new RHI primitive is needed. Identical bindings across passes share one cached backend object on the parent for free. Inline render passes mean the RDG can build attachment arrays per pass without allocating long-lived `FrameBuffer` objects. Aliased / transient resources can be added later by introducing a transient memory allocator beneath `Texture` / `Buffer` without changing the public RHI.
- **Third-party adapters.** Libraries that already speak Vulkan / DX12 conventions (ImGui backends, glTF loaders, profilers, render-graph layers) can adopt the RHI without translation: the subresource shapes have the same names and the same axes as `VkImageSubresource*`. There is no bespoke `Face` axis or default-view object to special-case.
