# Synchronization and Barriers

GPU commands can execute in parallel unless a dependency orders them. Zenith.NET exposes three synchronization mechanisms so applications can describe those dependencies without using graphics API-specific barrier structures.

| Mechanism | Scope | Use it when |
|-----------|-------|-------------|
| `Barrier(before, after)` | One command buffer | Later work depends on earlier memory writes while resource layouts remain unchanged |
| `Transition(texture, subresource, layout)` | One texture subresource | A texture changes how it will be accessed |
| `Submit(waitValues...)` | Queue submissions | Work on one queue depends on a submission from another queue |

## Memory Barriers

`Barrier` creates a global execution and memory dependency between two stage sets:

```csharp
commandBuffer.SetPipeline(producerPipeline);
commandBuffer.Dispatch(producerGroupCount, 1, 1);

commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

commandBuffer.SetPipeline(consumerPipeline);
commandBuffer.Dispatch(consumerGroupCount, 1, 1);
```

The first dispatch completes its relevant memory accesses before the second dispatch can consume them. The resource stays in its current layout.

Typical barriers include:

- Compute writes a storage buffer, then another compute dispatch reads it.
- Compute generates indirect arguments, then graphics consumes them.
- Copy work fills a buffer, then a shader reads it in the same command stream.
- One shader stage writes storage that a later shader stage reads or writes.

### Barrier Stages

`BarrierStages` is a flags enum:

| Stage | Covered work |
|-------|--------------|
| `VertexShading` | Vertex input, vertex shading, mesh shading, and graphics indirect arguments |
| `FragmentShading` | Fragment shading, color attachments, and depth/stencil work |
| `ComputeShading` | Compute shading and compute indirect arguments |
| `Copy` | Buffer and texture copy operations |
| `Resolve` | Multisample resolve operations |
| `All` | All commands and memory accesses |

Combine destination stages when several consumers depend on the same producer:

```csharp
commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.VertexShading | BarrierStages.FragmentShading);
```

Prefer the narrowest stages that describe the dependency. `All` is valid for conservative synchronization but can serialize unrelated work.

## Texture Transitions

Zenith.NET tracks a layout for every texture mip level and array layer. Call `Transition` before a subresource is used in a different role:

```csharp
commandBuffer.Transition(output, default, TextureLayout.Storage);

commandBuffer.SetPipeline(computePipeline);
commandBuffer.SetConstantBuffer(constantBuffer, 0);
commandBuffer.Dispatch(groupCountX, groupCountY, 1);

commandBuffer.Transition(output, default, TextureLayout.Sampled);
```

`default` selects mip level zero and array layer zero. Select another subresource explicitly when required:

```csharp
TextureSubresource subresource = new() { MipLevel = mipLevel, ArrayLayer = arrayLayer };

commandBuffer.Transition(texture, subresource, TextureLayout.CopyDst);
```

A transition includes the texture-specific execution, memory, and layout dependency. Do not add an equivalent global barrier beside every transition.

### Texture Layouts

| Layout | Intended access |
|--------|-----------------|
| `General` | General access when a more specific layout is unsuitable |
| `Sampled` | Sampled texture reads |
| `Storage` | Storage texture reads and writes |
| `ColorAttachment` | Color attachment access |
| `DepthStencilAttachment` | Writable depth/stencil attachment access |
| `DepthStencilReadOnly` | Read-only depth/stencil access |
| `CopySrc` / `CopyDst` | Copy source or destination |
| `ResolveSrc` / `ResolveDst` | Resolve source or destination |
| `Present` | Swap-chain presentation |

`Undefined` represents unknown or discarded previous contents. It cannot be requested as a destination layout.

## Cross-Queue Dependencies

Every submission returns a `TimelineValue`. Pass producer values to a consumer submission to wait on the GPU:

```csharp
CommandBuffer transferCommands = context.TransferQueue.CommandBuffer();
transferCommands.Upload(buffer, 0, data);
TimelineValue uploaded = transferCommands.Submit();

CommandBuffer computeCommands = context.ComputeQueue.CommandBuffer();
computeCommands.SetPipeline(computePipeline);
computeCommands.SetConstantBuffer(constantBuffer, 0);
computeCommands.Dispatch(groupCountX, groupCountY, 1);
TimelineValue computed = computeCommands.Submit(uploaded);

computed.Wait();
```

The CPU can continue after submitting both command buffers. Call `Wait()` only when the CPU must observe completion or before releasing resources still used by the submission.

## Common Hazards

### Write Then Read

A producer writes storage and a consumer reads the same memory. Use a barrier when the layout does not change:

```csharp
commandBuffer.Dispatch(writeGroupCount, 1, 1);
commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
commandBuffer.Dispatch(readGroupCount, 1, 1);
```

### Compute Then Indirect Draw

Graphics indirect arguments are covered by `VertexShading`:

```csharp
commandBuffer.SetPipeline(cullingPipeline);
commandBuffer.Dispatch(groupCountX, 1, 1);

commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.VertexShading);

commandBuffer.SetPipeline(graphicsPipeline);
commandBuffer.DrawIndexedIndirect(indirectBuffer, 0, drawCount);
```

### Storage Then Sampled Texture

The texture changes role, so use a transition instead of a global barrier:

```csharp
commandBuffer.Transition(texture, default, TextureLayout.Storage);
commandBuffer.Dispatch(groupCountX, groupCountY, 1);
commandBuffer.Transition(texture, default, TextureLayout.Sampled);
```

## Decision Guide

1. If a texture changes role, call `Transition`.
2. If later commands depend on earlier writes without a layout change, call `Barrier`.
3. If the dependency crosses queue submissions, pass `TimelineValue` instances to `Submit`.
4. If the CPU must observe completion, call `Wait()` on the final submission.

Avoid adding synchronization without a dependency. Unnecessary barriers and waits reduce the parallelism that explicit GPU APIs are designed to preserve.
