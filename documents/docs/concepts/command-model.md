# Command Model

Zenith.NET uses an explicit command recording model. GPU work is recorded into command buffers and then submitted to command queues for execution.

## Command Queues

`GraphicsContext` provides three command queues:

| Queue | Type | Capabilities |
|-------|------|-------------|
| `Graphics` | `CommandQueueType.Graphics` | Draw, dispatch, mesh dispatch, copy, acceleration structure builds |
| `Compute` | `CommandQueueType.Compute` | Compute dispatch, copy, acceleration structure builds |
| `Copy` | `CommandQueueType.Copy` | Data transfer only |

Each queue has two methods:

| Method | Description |
|--------|-------------|
| `CommandBuffer()` | Obtain a command buffer from the queue's internal pool |
| `WaitIdle()` | Block until all submitted work on this queue completes |

## Command Buffer Workflow

The typical workflow for each frame:

```csharp
// 1. Obtain a command buffer
CommandBuffer commandBuffer = context.Graphics.CommandBuffer();

// 2. Record commands
commandBuffer.BeginRenderPass(frameBuffer, clearValue, resourceTable);
commandBuffer.SetPipeline(pipeline);
commandBuffer.SetResourceTable(resourceTable);
commandBuffer.SetVertexBuffer(vertexBuffer, 0, 0);
commandBuffer.Draw(3, 1, 0, 0);
commandBuffer.EndRenderPass();

// 3. Submit to the GPU
commandBuffer.Submit(waitForCompletion: true);
```

## Render Pass Commands

A render pass scopes all draw operations to a frame buffer:

```csharp
commandBuffer.BeginRenderPass(frameBuffer, clearValue, resourceTables...);
// ... draw commands ...
commandBuffer.EndRenderPass();
```

`BeginRenderPass` accepts optional `ResourceTable` parameters for resource preprocessing (transition barriers).

## Draw Commands

| Method | Description |
|--------|-------------|
| `Draw(vertexCount, instanceCount, firstVertex, firstInstance)` | Direct non-indexed draw |
| `DrawIndexed(indexCount, instanceCount, firstIndex, vertexOffset, firstInstance)` | Indexed draw |
| `DrawIndirect(indirectBuffer, offsetInBytes, drawCount)` | GPU-driven non-indexed draw |
| `DrawIndexedIndirect(indirectBuffer, offsetInBytes, drawCount)` | GPU-driven indexed draw |

## Compute Commands

Compute dispatches do not require a render pass:

```csharp
commandBuffer.SetPipeline(computePipeline);
commandBuffer.SetResourceTable(resourceTable);
commandBuffer.Dispatch(groupCountX, groupCountY, groupCountZ);
```

| Method | Description |
|--------|-------------|
| `Dispatch(groupCountX, groupCountY, groupCountZ)` | Direct compute dispatch |
| `DispatchIndirect(indirectBuffer, offsetInBytes)` | GPU-driven compute dispatch |

## Mesh Shading Commands

| Method | Description |
|--------|-------------|
| `DispatchMesh(groupCountX, groupCountY, groupCountZ)` | Dispatch mesh shader groups |
| `DispatchMeshIndirect(indirectBuffer, offsetInBytes, dispatchCount)` | GPU-driven mesh dispatch |

## Copy Commands

Copy operations transfer data between buffers and textures:

| Method | Description |
|--------|-------------|
| `CopyBuffer` | Buffer-to-buffer copy |
| `CopyTexture` | Texture-to-texture copy |
| `CopyBufferToTexture` | Buffer-to-texture copy |
| `CopyTextureToBuffer` | Texture-to-buffer copy (readback) |
| `ResolveTexture` | Resolve a multisampled texture to a single-sampled target |

## Upload Commands

Upload data directly within a command buffer:

```csharp
commandBuffer.Upload(buffer, offsetInBytes, dataSpan);
commandBuffer.Upload(texture, slice, offset, extent, dataSpan);
```

## Acceleration Structure Commands

| Method | Description |
|--------|-------------|
| `BuildAccelerationStructure(BottomLevelAccelerationStructureDesc)` | Build a BLAS from triangle or AABB geometry |
| `BuildAccelerationStructure(TopLevelAccelerationStructureDesc)` | Build a TLAS from BLAS instances |
| `UpdateAccelerationStructure(tlas, newDesc)` | Update an existing TLAS in-place |

## Query Commands

| Method | Description |
|--------|-------------|
| `BeginQuery(queryHeap, index)` | Begin an occlusion query |
| `EndQuery(queryHeap, index)` | End an occlusion query |
| `WriteTimestamp(queryHeap, index)` | Write a GPU timestamp |

## Debug Commands

| Method | Description |
|--------|-------------|
| `BeginDebugEvent(label)` | Begin a named debug region (visible in GPU debuggers) |
| `EndDebugEvent()` | End the current debug region |
| `InsertDebugMarker(label)` | Insert a point-in-time marker |

## Submission

```csharp
// Submit and return immediately
commandBuffer.Submit();

// Submit and block until the GPU finishes
commandBuffer.Submit(waitForCompletion: true);
```

## Synchronization

Use `WaitIdle()` when you need to ensure the GPU has finished all prior work on a queue:

```csharp
context.Graphics.WaitIdle();
```

> [!NOTE]
> Command buffers are pooled and recycled by the queue. You do not need to manually manage their lifecycle — just call `CommandBuffer()` to get the next available one.
