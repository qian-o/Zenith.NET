# RHI Redesign

> Working document for the redesign discussion. Not for commit. The Chinese mirror lives in `rhi-redesign.zh.draft.md`.

## 1. Goals and Non-Goals

**Goals**

- Small, uniform public API surface that maps 1:1 to DirectX 12 / Vulkan 1.4 / Metal 4.
- Short frame-loop code with no managed allocations on the hot path.
- Cross-queue synchronization (Graphics / Compute / Copy) as a first-class primitive.
- Explicit resource state transitions as a stable foundation for a future RDG.
- Inline render passes; no long-lived `FrameBuffer`.
- Subresource / sub-range information expressed at the **call site** as a value type, not as a long-lived `View` object.

**Non-Goals**

- Full hazard tracking inside the RHI (only "current state" is cached, used to compute the source side of explicit transitions).
- One synchronization object per submission.
- Using `WaitForIdle` as the primary synchronization mechanism.
- Implementing the RDG, bindless, or descriptor-buffer paths in this iteration.
- Reworking `ResourceTable` / `ResourceLayout` / `Sampler` / `Pipeline` / shader IO outside of what is described here.
- Exposing per-aspect (color/depth/stencil) addressing, sub-range transitions, or "first-frame image ready" waiting on the public surface.

## 2. API-Surface Conventions

- All public value types: `record struct` with public mutable fields. No `ref struct`. No `in` parameters.
- Multi-element parameters: `ReadOnlySpan<T>` / `params ReadOnlySpan<T>`. No `T[]`, no `IEnumerable<T>`.
- All method bodies use `{ ... }`. **Exception:** `ref` / `ref readonly` returning properties keep `=> ref _field;`.
- Every byte-denominated parameter or field carries an `*InBytes` suffix.
- Backend-hook naming rule: the `*Core` suffix exists **only** to disambiguate from a **same-named** non-`Core` wrapper.
    - `Wait(ulong)` wrapper → `WaitCore(ulong)`
    - `Submit(...)` wrapper → `SubmitCore(...)`
    - `CreateCommandBuffer()` has no same-named wrapper (the public factory is `CommandBuffer()`) → no `Core` suffix.
    - `GetCompletedValue()` has no wrapper → no `Core` suffix.
- Every abstract member is plain `protected abstract`; same-assembly callers go through an `internal` wrapper.
- The only public synchronization result type is `CommandSubmission`.

## 3. Public Type Catalog

```csharp
// === Subresources (rule: views use range, copy/upload uses layers, single-point uses single) ===

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

public readonly struct CommandSubmission(CommandQueue queue, ulong value)
{
    public void Wait()
    {
        queue.Wait(value);
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

**View caches** (per-backend, on each `Texture` / `Buffer`):

- `Dictionary<TextureSubresourceRange, T_view>` — SRV / UAV
- `Dictionary<TextureSubresource, T_rtv_dsv>` — RTV / DSV
- `Dictionary<BufferRange, T_view>` — typed buffer view

Lazy-allocated by value-type key, lifetime tied to the parent. The same `(resource, range)` pair across all call sites shares one backend object.

**State tracking:** every `Texture` / `Buffer` keeps its current `TransitionState` internally; the initial state is set by the backend default. The first explicit `Transition` synthesizes the from→to barrier from that cached value.

**Removed from the framework (these all exist today):** `BufferView` / `BufferViewDesc` / `TextureView` / `TextureViewDesc` / `BufferViewType` / `IBindableResource` / `TextureSlice` (incl. `Face`) / `TextureOffset` / `TextureExtent` / `FrameBuffer` / `FrameBufferDesc` / `FrameBufferAttachment` / `ClearValue` / `ClearValues` (static factory) / `ClearFlags` / `GraphicsContext.CreateBufferView` / `CreateTextureView` / `CreateFrameBuffer`. The `Output` type itself stays — it remains `GraphicsPipelineDesc.Output`; only the `FrameBuffer.Output` use site disappears.

## 5. CommandQueue / CommandBuffer

Each `CommandQueue` owns one monotonic completion timeline. Each `Submit` advances that timeline by one and exposes the new value as `CommandSubmission(queue, value)`. Cross-queue dependencies are expressed by feeding upstream `CommandSubmission`s into a downstream `Submit(waits)`.

```csharp
public abstract class CommandQueue(GraphicsContext context, CommandQueueType type) : GraphicsResource(context)
{
    private readonly Lock @lock = new();
    private readonly Queue<CommandBuffer> available = [];
    private readonly Queue<InFlightCommandBuffer> execution = [];

    private ulong nextValue = 1;
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

    /// <summary>Idempotent. Returns immediately when the queue is already drained.</summary>
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

        nextValue++;

        SubmitCore(commandBuffer, waits, nextValue);

        lastSignaledValue = nextValue;

        execution.Enqueue(new(commandBuffer, nextValue));

        return new(this, nextValue);
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

Key points:

- `internal void Wait(ulong)` is the **single** wait entry point: lock → short-circuit on `GetCompletedValue() >= value` → `WaitCore(value)` → `CollectCompleted()`. `CommandSubmission.Wait()` is just a thin forward.
- `WaitForIdle()` is one line: `Wait(lastSignaledValue)`. The first call has `lastSignaledValue == 0`; `GetCompletedValue() >= 0` short-circuits naturally.
- `GetCompletedValue()` is a **method**, not a property: DX12 `fence.GetCompletedValue()` / VK `vkGetSemaphoreCounterValue` / Metal `event.SignaledValue` are all call-shaped on the backends.
- In `Submit`, `nextValue++` runs first, so the same post-incremented value is submitted, signalled, enqueued, and returned. Reads top-to-bottom as "advance → submit → record".
- Command buffer pooling lives in the queue: `CommandBuffer()` pops from `available` (or calls `CreateCommandBuffer()`); `CollectCompleted()` runs at every `Submit` / `Wait` and recycles buffers whose timeline value has retired.

### Backend Timeline Mapping

| Backend | Timeline object | API |
|---|---|---|
| DX12 | one `ID3D12Fence` per queue | `queue.Signal(fence, value)` / `fence.GetCompletedValue()` / `fence.SetEventOnCompletion` |
| Vulkan 1.4 | one timeline `VkSemaphore` per queue (core feature) | `vkQueueSubmit2` `pSignalSemaphoreInfos[].value` / `vkGetSemaphoreCounterValue` / `vkWaitSemaphores` |
| Metal 4 | one `MTLSharedEvent` per queue | `commandBuffer.EncodeSignalEvent(event, value)` / `event.SignaledValue` / `event.NotifyListener` |

The internal binary `VkFence` is retained for swapchain image acquire only.

### Interaction with SwapChain Present

Both DX12 and Vulkan implementations execute `Present` on the GraphicsQueue today, and the new design keeps this behavior. `SwapChain.Present(waits)` submits the present from the GraphicsQueue; cross-queue waits are honored on the GraphicsQueue side via "wait other queue's timeline" instructions (see Section 7 mapping table). The SwapChain implementation pulls the GraphicsQueue from `GraphicsContext`; the queue reference is never exposed on `SwapChain`'s public surface.

To keep barriers simple, all resources are created with concurrent / shared semantics on Vulkan (`VK_SHARING_MODE_CONCURRENT` across the three queues). Cross-queue synchronization is therefore expressed entirely through `CommandSubmission` waits; user code never writes Vulkan ownership-transfer barriers.

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

public void PushResourceTable(ShaderStageFlags stages, ResourceTable table);

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

- All byte-denominated parameters carry the `*InBytes` suffix; multi-element parameters are `ReadOnlySpan<T>`; no `in` parameters.
- Copy / upload uses `TextureSubresourceLayers` (one mip + contiguous layer range). The type itself excludes multi-mip, so no runtime validation is needed.
- `ResolveTexture` is single-point → single-point and uses `TextureSubresource`.
- `BeginRenderPass` accepts an attachment span; pass `null` explicitly for no depth. The implementation auto-`Transition`s the attachments and auto-fills `SetViewports` / `SetScissors` from attachment dimensions; the caller may issue `SetViewports` / `SetScissors` afterward to override.
- `PushResourceTable` takes a `ShaderStageFlags stages` argument: DX12 root-parameter visibility and Metal argument tables are stage-scoped. On Vulkan the descriptor set's stage mask is fixed at layout creation time, so `stages` is used to validate the call against the layout.
- `Transition` is only called explicitly between `ShaderResource` and `UnorderedAccess`. Render-target / depth-stencil / copy / vertex / index / CBV / indirect / present states are transitioned implicitly by the corresponding operation.
- `MemoryBarrier()` is a global cross-resource / cross-stage memory barrier. The command buffer also tracks the set of UAVs written by the previous dispatch and inserts a barrier automatically as a fallback when the next dispatch reads the same resource.

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
    /// Submits a present operation on the GraphicsQueue, waiting on <paramref name="waits"/>.
    /// Returns a CommandSubmission that signals when the next backbuffer is safe to write.
    /// First-frame seed is the caller's responsibility — pass an empty span, or seed an `imageReady`
    /// local with `default(CommandSubmission)` and let the backend filter it out of the next `Submit`'s waits.
    /// Note: <see cref="CommandSubmission.Wait"/> itself does not defend against `default`; only call it
    /// on submissions returned by an actual `Submit` / `Present`.
    /// </summary>
    public abstract CommandSubmission Present(params ReadOnlySpan<CommandSubmission> waits);

    public void Resize(uint width, uint height);

    public void Refresh(Surface surface);
}
```

Design points:

- Backbuffers are exposed as plain `Texture`s (color + optional depth-stencil). No `FrameBuffer`, no image index.
- `Present(...)` returns a `CommandSubmission` describing when the next backbuffer is writable; it accepts waits from any queue.
- The current backbuffer index lives **only** inside the backend (today: `VKSwapChain.ImageIndex`, `DXSwapChain.BufferIndex`). Public callers never need it.
- The first-frame image-ready submission is supplied by the caller (frames-in-flight strategy is the caller's choice).

Backend mapping:

| Backend | `Present(waits)` implementation | "Next-frame ready" signal |
|---|---|---|
| DX12 | for each `wait`, call `graphicsQueue.Wait(otherFence, otherValue)`, then `IDXGISwapChain3::Present` | frame-latency event wrapped as a `CommandSubmission` whose value is taken from the graphics queue timeline |
| Vulkan 1.4 | one bridging `vkQueueSubmit2(graphicsQueue, waits=timeline values, signal=renderFinished[N])` → `vkQueuePresentKHR(graphicsQueue, wait=renderFinished[N])` | the next frame's `vkAcquireNextImageKHR` returns binary `imageAvailable[N]`; another graphics-queue "wait binary, signal timeline" bridging submit wraps it as a `CommandSubmission` |
| Metal 4 | translate each wait into `commandBuffer.EncodeWaitForEvent(...)` → `commandBuffer.Present(drawable)` → `commandBuffer.Commit()` | the next drawable's availability event wrapped as a `CommandSubmission` |

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

## 9. Three-Backend RenderPass / Barrier Mapping

| Concept | DX12 | Vulkan 1.4 | Metal 4 |
|---|---|---|---|
| Begin / End RenderPass | `OMSetRenderTargets`, or `BeginRenderPass(RENDER_PASS_RENDER_TARGET_DESC)` / `EndRenderPass` | `vkCmdBeginRendering(VkRenderingInfo)` / `vkCmdEndRendering` | `commandBuffer.RenderCommandEncoder(MTLRenderPassDescriptor)` / `endEncoding` |
| LoadAction | `BeginningAccessType` (Discard / Preserve / Clear) | `loadOp` (DONT_CARE / LOAD / CLEAR) | `MTLLoadAction` |
| StoreAction | `EndingAccessType` (Discard / Preserve / Resolve) | `storeOp` (DONT_CARE / STORE / NONE) + `resolveMode` | `MTLStoreAction` |
| Resolve | `EndingAccessResolveSubresourceParameters` | `pResolveAttachments` + `resolveMode` | `MTLRenderPassDescriptor.resolveTexture` |
| Explicit Transition | `ResourceBarrier(Transition / UAV)` | `vkCmdPipelineBarrier2(VkImageMemoryBarrier2 / VkBufferMemoryBarrier2)` | `commandEncoder.MemoryBarrier(scope, after, before)` |
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
| Cross-pass binding sharing | ✅ | `ResourceTable` cache key = (range, layout) |

Conclusion: an RDG implementation can be built without any further changes to the RHI public surface.

## 11. ZenithHelper Members Affected

The following functions in [ZenithHelper](sources/Zenith.NET/ZenithHelper.cs) depend on the now-removed `TextureSlice` (with `Face`) / `TextureViewDesc`, or on the "cube faces multiply implicitly into subresource counts" model. Under the new design they no longer apply and must be removed or rewritten against the new types:

- `FaceCount(TextureDesc)` — cubes / cube arrays now express their faces directly as 6-layer arrays. `ArrayLayers` already counts faces; there is no separate face axis.
- `FaceIndex(TextureDesc, TextureSlice)` — depends on `TextureSlice.Face`.
- `FlattenArrayLayerCount(TextureDesc)` — the "fold face into array layer" step is no longer needed; callers use `desc.ArrayLayers` directly.
- `FlattenArrayLayerIndex(TextureDesc, TextureSlice)` — same.
- `FlattenArrayLayerRange(TextureViewDesc)` — `TextureViewDesc` is removed.
- `SubresourceCount(TextureDesc)` — new model = `MipLevels * ArrayLayers` (DX12 plane is handled implicitly by format). Inline at the call site.
- `SubresourceIndex(TextureDesc, TextureSlice)` — replace with inline `MipLevel + ArrayLayer * MipLevels` over `TextureSubresource`.
- `SubresourceSizeInBytes(TextureDesc, TextureSlice)` — rewrite to take a `TextureSubresource` (only `MipLevel` is used; the rest reuses the existing `MipDimensions` + `SizeInBytes` helpers).

Unaffected: pure format / geometry helpers (`MipDimensions`, `SizeInBytes(PixelFormat, ...)`, `ElementFormat` byte-size table, etc.) keep working as-is.
