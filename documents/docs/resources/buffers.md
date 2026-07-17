# Buffers

Buffers store linear data such as vertices, indices, constants, structured records, and indirect arguments. A `BufferDesc` defines the allocation size, permitted uses, and CPU access.

## Create a Buffer

Use a helper for common buffer roles:

```csharp
using Buffer = Zenith.NET.Buffer;

using Buffer vertexBuffer = context.CreateBuffer(BufferDesc.Vertex(vertexSizeInBytes));
using Buffer materialBuffer = context.CreateBuffer(
    BufferDesc.StorageReadOnly(materialSizeInBytes, (uint)sizeof(Material)));
```

The helpers create GPU-only buffers that can receive uploaded data. Use an explicit description for additional roles or CPU access:

```csharp
using Buffer outputBuffer = context.CreateBuffer(new()
{
    SizeInBytes = outputSizeInBytes,
    StrideInBytes = (uint)sizeof(Output),
    Usages = BufferUsages.StorageReadWrite | BufferUsages.TransferSrc | BufferUsages.TransferDst,
    Residency = MemoryResidency.GpuOnly
});
```

`StrideInBytes` describes structured elements. Use zero for unstructured data.

## Choose Memory Residency

| Residency | CPU access | Typical use |
|-----------|------------|-------------|
| `GpuOnly` | Not mappable | Persistent GPU data |
| `CpuWriteOnly` | Write through `Map()` | Frequently updated constants and staging data |
| `CpuReadOnly` | Read through `Map()` | Readback data |

Prefer `GpuOnly` unless CPU access is part of the buffer's regular use.

## Upload and Download

`Buffer.Upload` copies data into a buffer and completes before returning:

```csharp
fixed (Vertex* source = vertices)
{
    vertexBuffer.Upload(0, new()
    {
        Pointer = (nint)source,
        SizeInBytes = (uint)(sizeof(Vertex) * vertices.Length)
    });
}
```

`Buffer.Download` follows the same pattern and writes into caller-provided memory before returning.

Use command-buffer transfers to batch several operations:

```csharp
CommandBuffer transferCommands = context.TransferQueue.CommandBuffer();
transferCommands.Upload(vertexBuffer, 0, vertexData);
transferCommands.Upload(indexBuffer, 0, indexData);

TimelineValue uploadCompletion = transferCommands.Submit();
```

Pass `uploadCompletion` to the submission that consumes the buffers. For `CommandBuffer.Download`, keep the destination memory valid until the submission's `TimelineValue` has been waited on.

## Map CPU-Visible Memory

Map only buffers created with `CpuWriteOnly` or `CpuReadOnly` residency:

```csharp
nint destination = constantBuffer.Map();
*(Constants*)destination = constants;
constantBuffer.Unmap();
```

Use mapping for repeated CPU access. Use `Upload` or `Download` for individual transfers.

## Create a View

A `BufferView` selects a byte range and structured element stride:

```csharp
using BufferView materialView = context.CreateBufferView(BufferViewDesc.StorageReadOnly(
    materialBuffer,
    offsetInBytes,
    sizeInBytes,
    (uint)sizeof(Material)));

ResourceHandle materials = materialView.StorageReadOnlyHandle;
```

Use `BufferViewDesc.Constant`, `StorageReadOnly`, or `StorageReadWrite` to match the intended shader access. See [Bindless Resources](../fundamentals/bindless-resources.md) for shader declarations.

## Place Buffers in a Heap

Use a `Heap` to place compatible buffers in one allocation:

```csharp
BufferDesc vertexDesc = BufferDesc.Vertex(vertexSizeInBytes);
BufferDesc indexDesc = BufferDesc.Index(indexSizeInBytes);

SizeAndAlignment vertexRequirements = context.GetSizeAndAlignment(vertexDesc);
SizeAndAlignment indexRequirements = context.GetSizeAndAlignment(indexDesc);
ulong indexOffset = ZenithHelper.Align(vertexRequirements.SizeInBytes, indexRequirements.AlignmentInBytes);

using Heap heap = context.CreateHeap(HeapDesc.GpuOnly(indexOffset + indexRequirements.SizeInBytes));
using Buffer vertexBuffer = heap.CreateBuffer(0, vertexDesc);
using Buffer indexBuffer = heap.CreateBuffer(indexOffset, indexDesc);
```

Query each description, align its offset, and size the heap through the final resource. The descriptions and heap must use the same residency. Dispose every placed resource before its heap.

Buffer views depend on their source buffer. Keep buffers, views, and heaps alive until all submissions that use them have completed. Use a [memory barrier](../fundamentals/synchronization.md#add-a-memory-barrier) when later GPU work consumes buffer data written earlier in the same command stream.
