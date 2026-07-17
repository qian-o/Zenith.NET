# Commands

GPU work is recorded in a `CommandBuffer` and submitted to its `CommandQueue`. Recording preserves command order, but execution begins only after submission.

## Record and Submit Work

Request a command buffer from the queue that should execute the work:

```csharp
CommandBuffer commandBuffer = context.ComputeQueue.CommandBuffer();

commandBuffer.Transition(output, default, TextureLayout.Undefined, TextureLayout.Storage);

commandBuffer.SetPipeline(computePipeline);
commandBuffer.SetConstantBuffer(constantBuffer, 0);
commandBuffer.Dispatch(groupCountX, groupCountY, 1);

commandBuffer.Transition(output, default, TextureLayout.Storage, TextureLayout.Sampled);

commandBuffer.Submit().Wait();
```

A command buffer is ready for recording when returned by `CommandBuffer()`. Set the matching pipeline before binding pipeline state or issuing draw and dispatch commands, then submit the command buffer once.

Submit each command buffer once. Do not dispose it, retain it for later recording, or record commands after submission.

## Track Completion

`Submit()` returns a `TimelineValue` for that submission:

```csharp
TimelineValue completion = commandBuffer.Submit();

if (!completion.IsCompleted)
{
    completion.Wait();
}
```

Use `IsCompleted` for a non-blocking status check. Call `Wait()` only when CPU code must observe completion. To order work between queues without blocking the CPU, pass the producer value to the consumer submission. See [Synchronization](synchronization.md).

## Record a Render Pass

Transition attachments before beginning a render pass:

```csharp
commandBuffer.Transition(color, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
commandBuffer.BeginRenderPass([ColorAttachment.Clear(color, new(0.05f, 0.05f, 0.08f, 1.0f))], null);

commandBuffer.SetPipeline(graphicsPipeline);
commandBuffer.SetVertexBuffer(vertexBuffer, 0, 0);
commandBuffer.Draw(vertexCount, 1, 0, 0);

commandBuffer.EndRenderPass();
```

`BeginRenderPass` initializes viewports and scissors from the attachment size. Set them afterward when rendering to a smaller region.

## Transfer Data

Command buffers can upload, download, copy, and resolve resources. These operations can be batched with later GPU work.

Texture transfer commands do not change texture layouts. Record the required `CopySrc`, `CopyDst`, `ResolveSrc`, or `ResolveDst` transitions around them.

For simple one-off transfers, `Buffer.Upload`, `Buffer.Download`, `Texture.Upload`, and `Texture.Download` complete the transfer before returning.

## Label GPU Work

Use debug events and markers to identify a group of commands:

```csharp
commandBuffer.BeginDebugEvent("Post processing");
commandBuffer.Dispatch(groupCountX, groupCountY, 1);
commandBuffer.EndDebugEvent();
```

Query and acceleration-structure commands follow the same record-then-submit model. See [Ray Tracing](../workloads/ray-tracing.md) for acceleration-structure builds and the [API Reference](../../api/index.md) for query operations.
