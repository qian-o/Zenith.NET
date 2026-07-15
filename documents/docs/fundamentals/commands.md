# Queues and Commands

Zenith.NET exposes GPU work through explicit command queues and command buffers. Recording commands does not execute them immediately. Work begins when the command buffer is submitted to its queue.

Request a command buffer from the queue that will execute the work:

```csharp
CommandBuffer commandBuffer = context.ComputeQueue.CommandBuffer();
```

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

Set the matching pipeline before binding pipeline state or issuing draws and dispatches.

## Timeline Submission

`Submit()` queues the recorded work and returns a timeline value:

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

See [Synchronization](synchronization.md) for texture transitions, memory barriers, and cross-queue dependencies.

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

`Buffer.Upload()` and `Texture.Upload()` are synchronous convenience methods. Record transfers directly when several operations should be batched or chained with later GPU work.

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
