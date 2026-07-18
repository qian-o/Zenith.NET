# Buffers

Buffers store linear data such as vertices, indices, constants, structured records, and indirect arguments. A `BufferDesc` defines the byte size, structured-element stride, permitted uses, and residency.

## Create a Standalone Buffer

When explicit heap placement is unnecessary, define the buffer properties and create it directly from the context:

```csharp
BufferDesc desc = new()
{
    SizeInBytes = sizeof(Element) * count,
    StrideInBytes = 0,
    Usages = BufferUsages.Vertex | BufferUsages.TransferDst,
    Residency = MemoryResidency.GpuOnly
};

Buffer buffer = context.CreateBuffer(desc);
```

Set `Usages` to every operation the buffer must support and select its CPU access with `Residency`.

`StrideInBytes` describes structured elements. Use zero for unstructured data.

## Choose Memory Residency

| Residency | CPU access | Typical use |
|-----------|------------|-------------|
| `GpuOnly` | Not mappable | Persistent GPU data |
| `CpuReadOnly` | Read through `Map()` | Readback data |
| `CpuWriteOnly` | Write through `Map()` | Frequently updated constants and staging data |

Prefer `GpuOnly` unless CPU access is part of the buffer's regular use.

## Upload and Download

`Buffer.Upload` copies data into a buffer and completes before returning:

```csharp
buffer.Upload(0, new() { Pointer = data, SizeInBytes = sizeof(Element) * count });
```

`Buffer.Download` follows the same pattern and writes into caller-provided memory before returning.

Use command-buffer transfers to batch several operations:

```csharp
CommandBuffer commandBuffer = context.TransferQueue.CommandBuffer();
commandBuffer.Upload(buffer, 0, data);

TimelineValue timelineValue = commandBuffer.Submit();
```

Record additional transfers before `Submit()` to include them in the same submission. Pass `timelineValue` to a submission that consumes the buffers. For `CommandBuffer.Download`, keep the destination memory valid until the submission's `TimelineValue` has been waited on.

## Map CPU-Visible Memory

Map only buffers created with `CpuReadOnly` or `CpuWriteOnly` residency. Pair each `Map()` with `Unmap()`, and use mapping for repeated CPU access. Use `Upload` or `Download` for individual transfers.

## Create a View

A `BufferView` selects a byte range and structured element stride. Use `sizeof(T)` for the stride and derive the selected byte range from the element count.

Use `BufferViewDesc.Constant`, `StorageReadOnly`, or `StorageReadWrite` to match the intended shader access. See [Bindless Resources](../fundamentals/bindless-resources.md) for shader declarations.

See [Heaps](heaps.md) for allocation requirements and explicit buffer placement.

Buffer views depend on their source buffer. Keep buffers and views alive until all submissions that use them have completed. Use a [memory barrier](../fundamentals/synchronization.md#add-a-memory-barrier) when later GPU work consumes buffer data written earlier in the same command stream.
