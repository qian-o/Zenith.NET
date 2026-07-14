# Best Practices

This guide reflects the current explicit Zenith.NET model across DirectX 12, Metal, and Vulkan.

## Ownership and Deterministic Disposal

- Dispose GPU objects deterministically.
- Dispose dependents before owners, and dispose `GraphicsContext` last.
- Keep long-lived resources (pipelines, heaps, textures, buffers, samplers) outside the per-frame path.
- Use short-lived `using` scopes for temporary setup objects where practical.

Typical shutdown order:

1. Per-frame or transient resources.
2. Pipelines and binding-related resources.
3. Swap chain / view-owned presentation resources.
4. Command queues and context (via context disposal).

## Command Batching and Submission

- Record larger coherent command batches instead of many tiny submissions.
- Group draws and dispatches that share state to reduce rebinding.
- Submit once per logical phase when possible.
- Capture `TimelineValue` from `Submit(...)` when CPU reclamation or inter-queue ordering is required.

```csharp
CommandBuffer graphics = context.GraphicsQueue.CommandBuffer();
// Record multiple transitions, passes, and draws.
TimelineValue graphicsDone = graphics.Submit();
```

## Synchronization Model

Zenith.NET has three distinct synchronization layers:

- `CommandBuffer.Barrier(before, after)`: execution and memory ordering inside a command stream.
- `CommandBuffer.Transition(texture, subresource, layout)`: texture layout/access transitions.
- `Submit(waitValues...)`: cross-queue scheduling dependencies.

Use the narrowest primitive that solves the hazard.

## Keep BarrierStages Narrow

Prefer specific stages (`Copy`, `ComputeShading`, `FragmentShading`, etc.) over `BarrierStages.All`.

- Narrow barriers improve graphics API scheduling freedom.
- Over-broad barriers can serialize unrelated work.

## Transition Textures Explicitly

- Transition textures to the required layout before use (`ColorAttachment`, `Storage`, `Sampled`, `CopySrc`, `CopyDst`, `Present`).
- Avoid redundant patterns such as barrier + transition when the transition alone already captures the needed texture synchronization.
- Always transition presentation targets to `TextureLayout.Present` before presenting.

## Timeline Waits and Cross-Queue Dependencies

`Submit(waits...)` is the cross-queue dependency mechanism.

```csharp
CommandBuffer transfer = context.TransferQueue.CommandBuffer();
// Upload and copy commands.
TimelineValue uploadDone = transfer.Submit();

CommandBuffer graphics = context.GraphicsQueue.CommandBuffer();
// Consume uploaded resources.
TimelineValue frameDone = graphics.Submit(uploadDone);

frameDone.Wait();
```

Guidance:

- Wait on CPU only when reclamation or strict sequencing is required.
- Prefer queue-to-queue waits over CPU waits to keep work on the GPU timeline.

## Memory Residency and Upload/Download

- Plan heap usage with `GraphicsContext.GetSizeAndAlignment(...)`.
- Use explicit upload/download methods (`Upload(...)`, `Download(...)`) and queue placement intentionally.
- Schedule transfer work on `TransferQueue` when it reduces contention with graphics/compute workloads.
- Avoid readback in hot rendering paths unless the platform integration requires it.

## Explicit Layout Structs and Bindless Handles

- Use explicit CPU-side struct layout for GPU ABI-sensitive data.
- Keep field alignment and padding intentional.
- Track `ResourceHandle` lifetime carefully: handles are valid only while the underlying resource is alive.
- If a handle is cached in CPU or GPU-visible data, update or invalidate it when the resource is recreated.

## Resizing and Swap Chain Synchronization

- Resize presentation resources only when dimensions actually change.
- Recreate dependent size-based resources immediately after resize.
- Presentation is synchronous by design (`SwapChain.Present()` waits through queue timeline signaling).
- Design frame loops for no frames-in-flight assumptions.

## Capability Gating

The public capability surface is intentionally small:

- `DeviceName`
- `RayTracingSupported`
- `MeshShadingSupported`

Check capabilities before creating optional pipelines, acceleration structures, or feature paths.

## Validation and Debug Naming

- Enable validation during development (`useValidationLayer: true`).
- Subscribe to `GraphicsContext.ValidationMessage` and log `Severity`, `Message`, and `Timestamp`.
- Name resources and passes for easier analysis in tooling.

## Common Mistakes

| Mistake | Better Approach |
|---|---|
| Disposing context before child resources | Dispose children first, context last |
| Calling CPU waits every frame | Prefer queue dependency chaining and wait only when needed |
| Using `BarrierStages.All` by default | Use the narrowest relevant stages |
| Forgetting transitions to `Present` | Transition render target to `TextureLayout.Present` before present |
| Caching stale `ResourceHandle` values after resource recreation | Refresh handles whenever resources are replaced |
| Recreating pipelines/resources in per-frame callbacks | Create once and reuse |
