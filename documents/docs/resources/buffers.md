# Buffers and Memory

Buffers store linear GPU data such as vertices, indices, constants, structured records, and indirect arguments. A `BufferDesc` defines the size, element stride, allowed usages, and memory residency.

## Buffer Description

```csharp
using Buffer = Zenith.NET.Buffer;

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

BufferDesc outputDesc = BufferDesc.StorageReadWrite(outputSizeInBytes, (uint)sizeof(Output));
outputDesc.Usages |= BufferUsages.TransferSrc;
Buffer outputBuffer = context.CreateBuffer(outputDesc);
```

`BufferDesc.Staging` creates a CPU-write-only staging buffer.

## Buffer Usages

| Usage | Purpose |
|-------|---------|
| `Vertex` | Bind through `SetVertexBuffer` |
| `Index` | Bind through `SetIndexBuffer` |
| `Indirect` | Use as indirect draw or dispatch arguments |
| `Constant` | Bind through `SetConstantBuffer` or use `ConstantHandle` |
| `StorageReadOnly` | Resolve through `StorageReadOnlyHandle` |
| `StorageReadWrite` | Resolve through `StorageReadWriteHandle` |
| `TransferSrc` | Source of copies and downloads |
| `TransferDst` | Destination of copies and uploads |

Combine only the usages required by the application. The resource must be created with a usage before commands or handles can use that role.

## Memory Residency

| Residency | CPU access | Typical use |
|-----------|------------|-------------|
| `GpuOnly` | No direct mapping | Persistent vertex, index, storage, indirect, and attachment data |
| `CpuReadOnly` | Read through `Map()` | Readback buffers |
| `CpuWriteOnly` | Write through `Map()` | Frequently updated constants and staging data |

Prefer `GpuOnly` for data used repeatedly by the GPU. Use `CpuWriteOnly` for constants updated by the CPU and `CpuReadOnly` when the CPU must inspect GPU results.

## Uploading Data

`Buffer.Upload` copies data into the buffer and completes before returning:

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

Use `CommandBuffer.Upload` to group transfers in one submission:

```csharp
CommandBuffer transferCommands = context.TransferQueue.CommandBuffer();
transferCommands.Upload(vertexBuffer, 0, vertexData);
transferCommands.Upload(indexBuffer, 0, indexData);
TimelineValue uploaded = transferCommands.Submit();
```

Pass `uploaded` to the submission that consumes the data.

## Downloading Data

`Buffer.Download` copies data into caller-provided memory and completes before returning:

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

Use `CommandBuffer.Download` when readback belongs to a larger submission.

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

Use `Map` for repeated direct CPU access and `Upload` or `Download` for individual transfers.

## Buffer Views

A `BufferView` exposes a subrange with its own size and stride:

```csharp
BufferView materialView = context.CreateBufferView(BufferViewDesc.StorageReadOnly(materialBuffer, offsetInBytes, sizeInBytes, (uint)sizeof(Material)));
```

Available helper descriptions are `Constant`, `StorageReadOnly`, and `StorageReadWrite`. A view exposes the same typed handle categories as a full buffer:

```csharp
ResourceHandle materials = materialView.StorageReadOnlyHandle;
```

See [Bindless Resources](../fundamentals/bindless-resources.md) for the matching Slang `DescriptorHandle<T>` declarations.

## Explicit Heaps

Use a `Heap` when several resources should share one explicitly managed allocation:

```csharp
SizeAndAlignment requirements = context.GetSizeAndAlignment(desc);
Heap heap = context.CreateHeap(HeapDesc.GpuOnly(requirements.SizeInBytes));
Buffer buffer = heap.CreateBuffer(0, desc);
```

Align every placed offset to the resource's `AlignmentInBytes`. Keep the heap alive longer than all resources placed in it.

## Synchronization

Buffers have no texture layout. Use [Synchronization](../fundamentals/synchronization.md) to order buffer producers and consumers.

## Lifetime

Views depend on their source buffer, and placed buffers depend on their heap. Keep each owner alive through the final submission that uses its dependent resources.
