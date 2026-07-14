# Commands

Zenith.NET exposes GPU work through explicit command queues and command buffers. Recording commands does not execute them immediately. Work begins when the command buffer is submitted to its queue.

## Command Queues

Every `GraphicsContext` owns three queues:

| Queue | Typical work |
|-------|--------------|
| `GraphicsQueue` | Render passes, draw calls, compute dispatches, and presentation work |
| `ComputeQueue` | Compute dispatches and acceleration structure builds |
| `TransferQueue` | Buffer and texture uploads, downloads, and copies |

Request a command buffer from the queue that will execute the work:

```csharp
CommandBuffer commandBuffer = context.ComputeQueue.CommandBuffer();
```

Command buffers are pooled by their queue. Calling `Wait()` on a `TimelineValue` waits for that value, reclaims all completed command buffers on the same queue, and resets them before reuse.

## Recording Commands

A command buffer is ready for recording when returned by `CommandBuffer()`. Record commands in execution order, then submit it once:

```csharp
CommandBuffer commandBuffer = context.ComputeQueue.CommandBuffer();

commandBuffer.Transition(output, default, TextureLayout.Storage);
commandBuffer.SetPipeline(computePipeline);
commandBuffer.SetConstantBuffer(constantBuffer, 0);
commandBuffer.Dispatch(groupCountX, groupCountY, 1);
commandBuffer.Transition(output, default, TextureLayout.Sampled);

TimelineValue submission = commandBuffer.Submit();
submission.Wait();
```

Pipeline-dependent commands are ignored when no compatible pipeline is currently set. Set a graphics, compute, or mesh shading pipeline before binding its state or issuing work.

## Timeline Submission

`Submit()` closes the command buffer, submits it to its queue, and returns the value signaled on that queue's timeline:

```csharp
TimelineValue submission = commandBuffer.Submit();
```

The returned value can be inspected or waited on:

```csharp
if (!submission.IsCompleted)
{
    submission.Wait();
}
```

## Synchronization Boundary

Command recording meets synchronization at three explicit operations:

- `Transition` changes a texture subresource's access role.
- `Barrier` orders same-layout memory dependencies inside one command buffer.
- `Submit(waitValues...)` orders work across queue submissions.

See [Synchronization and Barriers](synchronization.md) for stage masks, texture layouts, cross-queue dependencies, and the decision guide.

## Render Passes

Render passes receive their attachments directly. Transition textures first, then begin the pass:

```csharp
commandBuffer.Transition(color, default, TextureLayout.ColorAttachment);
commandBuffer.Transition(depthStencil, default, TextureLayout.DepthStencilAttachment);

commandBuffer.BeginRenderPass([ColorAttachment.Clear(color, new(0.05f, 0.05f, 0.08f, 1.0f))], DepthStencilAttachment.Clear(depthStencil, 1.0f, 0));

commandBuffer.SetPipeline(graphicsPipeline);
commandBuffer.SetVertexBuffer(vertexBuffer, 0, 0);
commandBuffer.Draw(vertexCount, 1, 0, 0);

commandBuffer.EndRenderPass();
```

`BeginRenderPass` sets viewports and scissors from the attachment dimensions. Override them after beginning the pass when a smaller rendering region is required.

## Copies and Transfers

Command buffers support buffer copies, texture copies, buffer-to-texture copies, texture-to-buffer copies, resolves, uploads, and downloads. Copy helpers transition texture subresources to their required copy layouts and restore the tracked layouts afterward.

Convenience methods such as `Buffer.Upload()` and `Texture.Upload()` record work on the transfer queue, submit it, and wait for completion. Record transfer commands directly when several operations should be batched or consumed asynchronously by another queue.

## Acceleration Structures

`BuildAccelerationStructure` records BLAS or TLAS construction and returns the new acceleration structure. `UpdateAccelerationStructure` records an in-place update for an acceleration structure created with `AccelerationStructureBuildFlags.AllowUpdate`.

Build related acceleration structures in dependency order, then submit the command buffer before using their handles:

```csharp
BottomLevelAccelerationStructure blas = commandBuffer.BuildAccelerationStructure(blasDesc);
TopLevelAccelerationStructure tlas = commandBuffer.BuildAccelerationStructure(tlasDesc);

commandBuffer.Submit().Wait();
```

## Queries and Debug Markers

Query heaps support occlusion, binary occlusion, and timestamp queries. Record query operations with `BeginQuery`, `EndQuery`, or `WriteTimestamp`.

Use `BeginDebugEvent`, `EndDebugEvent`, and `InsertDebugMarker` to label GPU work for graphics debuggers:

```csharp
commandBuffer.BeginDebugEvent("Post processing");
commandBuffer.Dispatch(groupCountX, groupCountY, 1);
commandBuffer.EndDebugEvent();
```
