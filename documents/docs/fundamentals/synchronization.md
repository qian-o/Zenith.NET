# Synchronization

Synchronization makes data produced by earlier GPU work available to later work. Choose the mechanism that matches the dependency:

| Mechanism | Use it when |
|-----------|-------------|
| `Barrier(before, after)` | Commands in one command buffer share data without changing a texture layout |
| `Transition(texture, subresource, before, after)` | A texture changes its access role |
| `Submit(waitValues...)` | A submission depends on work submitted to another queue |

## Add a Memory Barrier

Use `Barrier` when a later command consumes memory written by an earlier command and the resource layout does not change:

```csharp
commandBuffer.SetPipeline(pipeline);
commandBuffer.Dispatch(groupCount, 1, 1);

commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

commandBuffer.SetPipeline(nextPipeline);
commandBuffer.Dispatch(nextGroupCount, 1, 1);
```

Common cases include storage-buffer producer/consumer chains and compute-generated indirect arguments. Select the stages that perform the producing and consuming work, and combine flags when several stages consume the result.

Use `BarrierStages.All` only when a narrower dependency cannot describe the work.

## Transition a Texture

Supply the current and next layout whenever a texture changes how it is used:

```csharp
commandBuffer.Transition(texture, default, TextureLayout.Undefined, TextureLayout.Storage);

commandBuffer.SetPipeline(pipeline);
commandBuffer.SetConstantBuffer(buffer, 0);
commandBuffer.Dispatch(groupCountX, groupCountY, 1);

commandBuffer.Transition(texture, default, TextureLayout.Storage, TextureLayout.Sampled);
```

`default` selects mip level zero and array layer zero. Pass a `TextureSubresource` with the required `MipLevel` and `ArrayLayer` when another subresource is needed.

Use `Undefined` as the source only when previous contents can be discarded. Copy, resolve, and command-buffer upload or download operations do not insert transitions. The `Texture.Upload` and `Texture.Download` convenience methods perform their declared current-to-final layout transitions.

The principal layouts are listed in declaration order:

| Layout | Access role |
|--------|-------------|
| `Undefined` | Previous contents are discarded |
| `Common` | General access and shared-texture presentation paths |
| `Sampled` | Sampled texture reads |
| `Storage` | Storage texture reads and writes |
| `ColorAttachment` | Color attachment access |
| `DepthStencilAttachment` | Writable depth/stencil attachment access |
| `DepthStencilReadOnly` | Read-only depth/stencil access |
| `CopySrc` / `CopyDst` | Copy source or destination |
| `ResolveSrc` / `ResolveDst` | Resolve source or destination |
| `Present` | Presentation |

## Order Work Across Queues

Pass a producer's `TimelineValue` to the dependent submission:

```csharp
CommandBuffer commandBuffer = context.TransferQueue.CommandBuffer();
commandBuffer.Upload(buffer, 0, data);
TimelineValue timelineValue = commandBuffer.Submit();

commandBuffer = context.ComputeQueue.CommandBuffer();
commandBuffer.SetPipeline(pipeline);
commandBuffer.SetConstantBuffer(buffer, 0);
commandBuffer.Dispatch(groupCountX, groupCountY, 1);

commandBuffer.Submit(timelineValue);
```

Passing a producer `TimelineValue` to `Submit` orders the consumer submission after that value without blocking the CPU. Call `Wait()` only when CPU code must observe GPU results.
