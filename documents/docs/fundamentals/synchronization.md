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
commandBuffer.SetPipeline(producerPipeline);
commandBuffer.Dispatch(producerGroupCount, 1, 1);

commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

commandBuffer.SetPipeline(consumerPipeline);
commandBuffer.Dispatch(consumerGroupCount, 1, 1);
```

Common cases include storage-buffer producer/consumer chains and compute-generated indirect arguments. Select the stages that perform the producing and consuming work. Combine flags when several stages consume the result:

```csharp
commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.VertexShading | BarrierStages.FragmentShading);
```

Use `BarrierStages.All` only when a narrower dependency cannot describe the work.

## Transition a Texture

Zenith.NET does not track texture layouts. Supply the current and next layout whenever a texture changes how it is used:

```csharp
commandBuffer.Transition(output, default, TextureLayout.Undefined, TextureLayout.Storage);

commandBuffer.SetPipeline(computePipeline);
commandBuffer.SetConstantBuffer(constantBuffer, 0);
commandBuffer.Dispatch(groupCountX, groupCountY, 1);

commandBuffer.Transition(output, default, TextureLayout.Storage, TextureLayout.Sampled);
```

`default` selects mip level zero and array layer zero. Select another subresource explicitly when needed:

```csharp
commandBuffer.Transition(texture, new()
{
	MipLevel = mipLevel,
	ArrayLayer = arrayLayer
}, currentLayout, TextureLayout.CopyDst);
```

Use `Undefined` as the source only when previous contents can be discarded. Copy, resolve, and command-buffer upload or download operations do not insert transitions. The `Texture.Upload` and `Texture.Download` convenience methods perform their declared current-to-final layout transitions.

The principal layouts are:

| Layout | Access role |
|--------|-------------|
| `Sampled` | Sampled texture reads |
| `Storage` | Storage texture reads and writes |
| `ColorAttachment` | Color attachment access |
| `DepthStencilAttachment` | Writable depth/stencil attachment access |
| `DepthStencilReadOnly` | Read-only depth/stencil access |
| `CopySrc` / `CopyDst` | Copy source or destination |
| `ResolveSrc` / `ResolveDst` | Resolve source or destination |
| `Present` | Presentation |
| `Common` | General access and shared-texture presentation paths |

## Order Work Across Queues

Pass a producer's `TimelineValue` to the dependent submission:

```csharp
CommandBuffer transferCommands = context.TransferQueue.CommandBuffer();
transferCommands.Upload(buffer, 0, data);
TimelineValue uploadCompletion = transferCommands.Submit();

CommandBuffer computeCommands = context.ComputeQueue.CommandBuffer();
computeCommands.SetPipeline(computePipeline);
computeCommands.SetConstantBuffer(constantBuffer, 0);
computeCommands.Dispatch(groupCountX, groupCountY, 1);

computeCommands.Submit(uploadCompletion).Wait();
```

The queue dependency is resolved by the GPU. The CPU can continue until it explicitly calls `Wait()`.
