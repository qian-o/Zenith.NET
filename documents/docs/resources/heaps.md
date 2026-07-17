# Heaps

A `Heap` is an explicit memory allocation. Create placed buffers and textures from it with `Heap.CreateBuffer` and `Heap.CreateTexture`, supplying each resource's byte offset. Use a heap when several compatible resources should share an allocation or when the application needs to control resource placement.

## Query Requirements

Query every resource description before sizing the heap:

```csharp
BufferDesc vertexDesc = BufferDesc.Vertex(vertexSizeInBytes);
BufferDesc indexDesc = BufferDesc.Index(indexSizeInBytes);

SizeAndAlignment vertexRequirements = context.GetSizeAndAlignment(vertexDesc);
SizeAndAlignment indexRequirements = context.GetSizeAndAlignment(indexDesc);
```

`SizeInBytes` is the allocation space required by the resource. `AlignmentInBytes` is the required alignment of its offset in the heap.

## Place Buffers

Place the first buffer at offset zero. Align each later offset to that resource's requirement, then size the heap through the final resource:

```csharp
ulong indexOffset = ZenithHelper.Align(vertexRequirements.SizeInBytes,
                                       indexRequirements.AlignmentInBytes);
ulong heapSize = indexOffset + indexRequirements.SizeInBytes;

using Heap heap = context.CreateHeap(HeapDesc.GpuOnly(heapSize));
using Zenith.NET.Buffer vertexBuffer = heap.CreateBuffer(0, vertexDesc);
using Zenith.NET.Buffer indexBuffer = heap.CreateBuffer(indexOffset, indexDesc);
```

Use the `SizeInBytes` and `AlignmentInBytes` returned by `GetSizeAndAlignment` when calculating offsets and heap size. Do not substitute `BufferDesc.SizeInBytes` for the returned `SizeInBytes`.

## Place Textures

Texture placement follows the same requirement query. Offset zero satisfies the texture's alignment requirement:

```csharp
TextureDesc colorDesc = TextureDesc.ColorAttachment(PixelFormat.B8G8R8A8UNorm,
                                                    width,
                                                    height,
                                                    1,
                                                    SampleCount.Count1);
SizeAndAlignment colorRequirements = context.GetSizeAndAlignment(colorDesc);

using Heap heap = context.CreateHeap(HeapDesc.GpuOnly(colorRequirements.SizeInBytes));
using Texture color = heap.CreateTexture(0, colorDesc);
```

For multiple textures, align each offset to that texture's `AlignmentInBytes`. Set `HeapDesc.SizeInBytes` to at least the maximum `offset + SizeInBytes` of all placed resources.

## Match Residency

For a placed buffer, use the same residency in its `BufferDesc` and the containing `HeapDesc`:

| Heap helper | Resource residency |
|-------------|--------------------|
| `HeapDesc.GpuOnly` | `MemoryResidency.GpuOnly` |
| `HeapDesc.CpuWriteOnly` | `MemoryResidency.CpuWriteOnly` |
| `HeapDesc.CpuReadOnly` | `MemoryResidency.CpuReadOnly` |

Create texture heaps with `HeapDesc.GpuOnly`. Query each exact resource description on the context that creates the heap, use its returned size and alignment, and keep every resource range within `HeapDesc.SizeInBytes`.

A heap does not add usages to a resource or replace required texture transitions and memory barriers.

## Create Standalone Resources

When explicit placement is unnecessary, create a standalone resource directly from the context:

```csharp
using Zenith.NET.Buffer vertexBuffer = context.CreateBuffer(vertexDesc);
using Texture color = context.CreateTexture(colorDesc);
```

Standalone resources use the same descriptions, usages, layouts, and synchronization rules, but do not require a `Heap`. Continue with [Buffers](buffers.md) and [Textures](textures.md) for resource-specific workflows.

## Manage Lifetime

Placed resources use memory owned by the heap. Keep the heap alive until every placed buffer and texture is disposed and all submissions that use them have completed:

```csharp
completion.Wait();

indexBuffer.Dispose();
vertexBuffer.Dispose();
heap.Dispose();
```

`ResourceHandle` does not own its resource. Keep each placed resource, and any explicit view used to obtain a stored handle, alive through the final submission that uses the handle. Dispose explicit views before their source resources, and update constant data when replacing a stored handle.
