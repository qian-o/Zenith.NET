# Buffers

Buffers store linear data such as vertices, indices, constants, structured records, and indirect arguments. A `BufferDesc` defines the byte size, structured-element stride, permitted uses, and residency.

## Create a Standalone Buffer

When explicit heap placement is unnecessary, create a standalone buffer from the context. Use a helper for common buffer roles:

```csharp
using Buffer = Zenith.NET.Buffer;

using Buffer vertexBuffer = context.CreateBuffer(BufferDesc.Vertex(vertexSizeInBytes));
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
BufferViewDesc viewDesc = BufferViewDesc.StorageReadOnly(materialBuffer,
                                                         offsetInBytes,
                                                         sizeInBytes,
                                                         (uint)sizeof(Material));

using BufferView materialView = context.CreateBufferView(viewDesc);

ResourceHandle materials = materialView.StorageReadOnlyHandle;
```

Use `BufferViewDesc.Constant`, `StorageReadOnly`, or `StorageReadWrite` to match the intended shader access. See [Bindless Resources](../fundamentals/bindless-resources.md) for shader declarations.

See [Heaps](heaps.md) for allocation requirements and explicit buffer placement.

Buffer views depend on their source buffer. Keep buffers and views alive until all submissions that use them have completed. Use a [memory barrier](../fundamentals/synchronization.md#add-a-memory-barrier) when later GPU work consumes buffer data written earlier in the same command stream.
