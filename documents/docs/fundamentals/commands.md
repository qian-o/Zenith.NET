# Commands

GPU work is recorded in a `CommandBuffer` borrowed from its `CommandQueue`. Recording preserves command order, but execution begins only after submission.

## Record and Submit Work

Borrow a command buffer from the queue that should execute the work:

```csharp
CommandBuffer commandBuffer = context.ComputeQueue.CommandBuffer();

commandBuffer.Transition(texture, default, TextureLayout.Undefined, TextureLayout.Storage);

commandBuffer.SetPipeline(pipeline);
commandBuffer.SetConstantBuffer(buffer, 0);
commandBuffer.Dispatch(groupCountX, groupCountY, 1);

commandBuffer.Transition(texture, default, TextureLayout.Storage, TextureLayout.Sampled);

commandBuffer.Submit();
```

A command buffer is queue-owned and valid only for the current recording. Set the matching pipeline before binding state or issuing work, submit it immediately, and never store, reuse, or dispose it.

## Track Completion

`Submit()` returns a `TimelineValue`. Use `IsCompleted` only for a non-blocking status query. Call `Wait()` when CPU code must observe GPU results, including command-buffer downloads. To order queues without blocking the CPU, pass the producer value to the consumer submission. See [Synchronization](synchronization.md).

## Record a Render Pass

Transition attachments before beginning a render pass:

```csharp
commandBuffer.Transition(texture, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
commandBuffer.BeginRenderPass([ColorAttachment.Clear(texture, default)], null);

commandBuffer.SetPipeline(pipeline);
commandBuffer.SetVertexBuffer(buffer, 0, 0);
commandBuffer.Draw(vertexCount, 1, 0, 0);

commandBuffer.EndRenderPass();
```

`BeginRenderPass` initializes viewports and scissors from the attachment size. Set them afterward when rendering to a smaller region.

## Transfer Data

Command buffers can upload, download, copy, and resolve resources. These operations can be batched with later GPU work.

Texture transfer commands do not change texture layouts. Record the required `CopySrc`, `CopyDst`, `ResolveSrc`, or `ResolveDst` transitions around them.

For simple one-off transfers, `Buffer.Upload`, `Buffer.Download`, `Texture.Upload`, and `Texture.Download` complete the transfer before returning.

## Label GPU Work

Use `BeginDebugEvent`, `EndDebugEvent`, and `InsertDebugMarker` to identify recorded GPU work in diagnostics.

Query and acceleration-structure commands follow the same record-then-submit model. See [Ray Tracing](../workloads/ray-tracing.md) for acceleration-structure builds and the [API Reference](../../api/index.md) for query operations.
