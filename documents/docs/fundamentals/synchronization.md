# Synchronization

GPU commands can execute in parallel unless a dependency orders them. Zenith.NET exposes three synchronization mechanisms:

| Mechanism | Scope | Use it when |
|-----------|-------|-------------|
| `Barrier(before, after)` | One command buffer | Later work depends on earlier memory writes while resource layouts remain unchanged |
| `Transition(texture, subresource, before, after)` | One texture subresource | A texture changes how it will be accessed |
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

Zenith.NET does not track texture layouts. The application provides the known current layout and the required next layout for each transition:

```csharp
commandBuffer.Transition(output, default, TextureLayout.Undefined, TextureLayout.Storage);

commandBuffer.SetPipeline(computePipeline);
commandBuffer.SetConstantBuffer(constantBuffer, 0);
commandBuffer.Dispatch(groupCountX, groupCountY, 1);

commandBuffer.Transition(output, default, TextureLayout.Storage, TextureLayout.Sampled);
```

Use `Undefined` as the source layout only when previous contents may be discarded.

`default` selects mip level zero and array layer zero. Select another subresource explicitly when required:

```csharp
TextureSubresource subresource = new() { MipLevel = mipLevel, ArrayLayer = arrayLayer };

commandBuffer.Transition(texture, subresource, currentLayout, TextureLayout.CopyDst);
```

`Transition` includes the dependency for that layout change. Copy, upload, download, and resolve commands do not insert transitions.

### Texture Layouts

| Layout | Intended access |
|--------|-----------------|
| `Common` | General access and native shared-texture interop |
| `Sampled` | Sampled texture reads |
| `Storage` | Storage texture reads and writes |
| `ColorAttachment` | Color attachment access |
| `DepthStencilAttachment` | Writable depth/stencil attachment access |
| `DepthStencilReadOnly` | Read-only depth/stencil access |
| `CopySrc` / `CopyDst` | Copy source or destination |
| `ResolveSrc` / `ResolveDst` | Resolve source or destination |
| `Present` | Swap-chain presentation |

`Undefined` represents discarded previous contents and is only valid as a source layout.

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

The CPU can continue after submitting both command buffers. Call `Wait()` when host code must observe completion.
