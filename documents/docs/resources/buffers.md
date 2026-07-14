# Buffers and Memory

Buffers store linear GPU data such as vertices, indices, constants, structured records, and indirect arguments. A `BufferDesc` defines the size, element stride, allowed usages, and memory residency.

## Buffer Description

```csharp
BufferDesc desc = new()
{
    SizeInBytes = sizeInBytes,
    StrideInBytes = (uint)sizeof(Vertex),
    Usages = BufferUsages.Vertex | BufferUsages.TransferDst,
    Residency = MemoryResidency.GpuOnly
};

Buffer vertexBuffer = context.CreateBuffer(desc);
```

| Field | Description |
|-------|-------------|
| `SizeInBytes` | Total allocation size visible through the buffer |
| `StrideInBytes` | Structured element stride; use zero when no structured view is needed |
| `Usages` | Operations and shader access permitted for the buffer |
| `Residency` | CPU/GPU memory access strategy |

## Convenience Descriptions

`BufferDesc` provides common GPU-only descriptions:

```csharp
Buffer vertexBuffer = context.CreateBuffer(BufferDesc.Vertex(vertexSizeInBytes));
Buffer indexBuffer = context.CreateBuffer(BufferDesc.Index(indexSizeInBytes));
Buffer indirectBuffer = context.CreateBuffer(BufferDesc.Indirect(indirectSizeInBytes));
Buffer constantBuffer = context.CreateBuffer(BufferDesc.Constant(constantSizeInBytes));

Buffer materialBuffer = context.CreateBuffer(BufferDesc.StorageReadOnly(materialSizeInBytes, (uint)sizeof(Material)));

Buffer outputBuffer = context.CreateBuffer(BufferDesc.StorageReadWrite(outputSizeInBytes, (uint)sizeof(Output)));
```

These descriptions include `TransferDst` so data can be uploaded through the transfer queue. `BufferDesc.Staging` creates a CPU-write-only transfer source.

## Buffer Usages

| Usage | Purpose |
|-------|---------|
| `Vertex` | Bind through `SetVertexBuffer` |
| `Index` | Bind through `SetIndexBuffer` |
| `Indirect` | Use as indirect draw or dispatch arguments |
| `Constant` | Bind through `SetConstantBuffer` or use `ConstantHandle` |
| `StorageReadOnly` | Resolve through `StorageReadOnlyHandle` |
| `StorageReadWrite` | Resolve through `StorageReadWriteHandle` |
| `TransferSrc` | Copy or upload source |
| `TransferDst` | Copy or upload destination |

Combine only the usages required by the application. The resource must be created with a usage before commands or handles can use that role.

## Memory Residency

| Residency | CPU access | Typical use |
|-----------|------------|-------------|
| `GpuOnly` | No direct mapping | Persistent vertex, index, storage, indirect, and attachment data |
| `CpuReadOnly` | Read through `Map()` | Readback buffers |
| `CpuWriteOnly` | Write through `Map()` | Frequently updated constants and staging data |

Prefer `GpuOnly` for data used repeatedly by the GPU. Use `CpuWriteOnly` for constants updated by the CPU and `CpuReadOnly` when the CPU must inspect GPU results.

## Uploading Data

`Buffer.Upload` maps CPU-write-only buffers directly. Other residencies use a transfer command buffer and wait for completion:

```csharp
unsafe
{
    fixed (Vertex* pointer = vertices)
    {
        vertexBuffer.Upload(0, new()
        {
            Pointer = (nint)pointer,
            SizeInBytes = (uint)(sizeof(Vertex) * vertices.Length)
        });
    }
}
```

Batch uploads by recording them on one command buffer when the data should be consumed asynchronously:

```csharp
CommandBuffer transferCommands = context.TransferQueue.CommandBuffer();
transferCommands.Upload(vertexBuffer, 0, vertexData);
transferCommands.Upload(indexBuffer, 0, indexData);
TimelineValue uploaded = transferCommands.Submit();
```

Pass `uploaded` to a dependent queue submission rather than waiting on the CPU.

## Downloading Data

`Buffer.Download` reads mapped CPU-read-only memory directly or uses the transfer queue for other residencies:

```csharp
unsafe
{
    fixed (Result* pointer = results)
    {
        outputBuffer.Download(0, new()
        {
            Pointer = (nint)pointer,
            SizeInBytes = (uint)(sizeof(Result) * results.Length)
        });
    }
}
```

The convenience method is synchronous. Record `commandBuffer.Download` directly when readback should be grouped with other commands.

## Mapping

Call `Map()` only for CPU-visible memory:

```csharp
nint pointer = constantBuffer.Map();

unsafe
{
    *(Constants*)pointer = constants;
}

constantBuffer.Unmap();
```

`Upload` and `Download` are usually preferable because they choose the correct path for the buffer residency.

## Buffer Views

A `BufferView` exposes a subrange with its own size and stride:

```csharp
BufferView materialView = context.CreateBufferView(BufferViewDesc.StorageReadOnly(materialBuffer, offsetInBytes, sizeInBytes, (uint)sizeof(Material)));
```

Available helper descriptions are `Constant`, `StorageReadOnly`, and `StorageReadWrite`. A view exposes the same typed handle categories as a full buffer:

```csharp
ResourceHandle materials = materialView.StorageReadOnlyHandle;
```

See [Bindless Resources](../concepts/resource-binding.md) for the matching Slang `DescriptorHandle<T>` declarations.

## Explicit Heaps

Use a `Heap` when several resources should share one explicitly managed allocation:

```csharp
SizeAndAlignment requirements = context.GetSizeAndAlignment(desc);
Heap heap = context.CreateHeap(HeapDesc.GpuOnly(requirements.SizeInBytes));
Buffer buffer = heap.CreateBuffer(0, desc);
```

Align every placed offset to the resource's `AlignmentInBytes`. Keep the heap alive longer than all resources placed in it.

## Synchronization

A buffer does not have a tracked layout. Use a memory barrier when later commands depend on earlier buffer writes in the same command stream:

```csharp
commandBuffer.Dispatch(writeGroupCount, 1, 1);
commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
commandBuffer.Dispatch(readGroupCount, 1, 1);
```

Use a timeline dependency when the producer and consumer are submitted to different queues. See [Synchronization and Barriers](../concepts/synchronization.md).

## Lifetime

Dispose explicit views before their underlying buffer. When a buffer is placed in a heap, dispose the buffer before the heap. Wait for the final submission that uses a buffer or any of its handles before disposing it.
