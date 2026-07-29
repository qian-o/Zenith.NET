# Heaps

A `Heap` is an explicit memory allocation. Create placed buffers and textures from it with `Heap.CreateBuffer` and `Heap.CreateTexture`, supplying each resource's byte offset. Use a heap when several compatible resources should share an allocation or when the application needs to control resource placement.

## Query Requirements

Define every resource description explicitly and query it before sizing the heap. The returned `SizeInBytes` is the allocation space required by the resource, and `AlignmentInBytes` is the required alignment of its offset in the heap.

## Place Buffers

Define the buffer description, query its requirements, then create a compatible heap and place the buffer. Offset zero satisfies the first resource's alignment requirement:

```csharp
BufferDesc desc = new()
{
    SizeInBytes = sizeof(Element) * count,
    StrideInBytes = 0,
    Usages = BufferUsages.Vertex | BufferUsages.TransferDst,
    Residency = MemoryResidency.GpuOnly
};

SizeAndAlignment sizeAndAlignment = context.GetSizeAndAlignment(desc);

Heap heap = context.CreateHeap(HeapDesc.GpuOnly(sizeAndAlignment.SizeInBytes));
Buffer buffer = heap.CreateBuffer(0, desc);
```

Only add another description and requirement query when placing another resource. Align each later offset to that resource's `AlignmentInBytes`, and size the heap through the final resource.

Use the `SizeInBytes` and `AlignmentInBytes` returned by `GetSizeAndAlignment` when calculating offsets and heap size. Do not substitute `BufferDesc.SizeInBytes` for the returned `SizeInBytes`.

## Place Textures

Define the texture description, query its requirements, then create a compatible heap and place the texture. Offset zero satisfies the first resource's alignment requirement:

```csharp
TextureDesc desc = new()
{
    Type = TextureType.Texture2D,
    Format = PixelFormat.R8G8B8A8UNorm,
    Width = width,
    Height = height,
    Depth = 1,
    MipLevels = mipLevels,
    ArrayLayers = 1,
    SampleCount = SampleCount.Count1,
    Usages = TextureUsages.Sampled | TextureUsages.TransferDst
};

SizeAndAlignment sizeAndAlignment = context.GetSizeAndAlignment(desc);

Heap heap = context.CreateHeap(HeapDesc.GpuOnly(sizeAndAlignment.SizeInBytes));
Texture texture = heap.CreateTexture(0, desc);
```

For multiple textures, align each offset to that texture's `AlignmentInBytes`. Set `HeapDesc.SizeInBytes` to at least the maximum `offset + SizeInBytes` of all placed resources.

## Match Residency

For a placed buffer, use the same residency in its `BufferDesc` and the containing `HeapDesc`:

| Heap helper | Resource residency |
|-------------|--------------------|
| `HeapDesc.GpuOnly` | `MemoryResidency.GpuOnly` |
| `HeapDesc.CpuReadOnly` | `MemoryResidency.CpuReadOnly` |
| `HeapDesc.CpuWriteOnly` | `MemoryResidency.CpuWriteOnly` |

Create texture heaps with `HeapDesc.GpuOnly`. Query each exact resource description on the context that creates the heap, use its returned size and alignment, and keep every resource range within `HeapDesc.SizeInBytes`.

A heap does not add usages to a resource or replace required texture transitions and memory barriers.

## Create Standalone Resources

When explicit placement is unnecessary, create a standalone buffer or texture directly from the context.

Standalone resources use the same descriptions, usages, layouts, and synchronization rules, but do not require a `Heap`. Continue with [Buffers](buffers.md) and [Textures](textures.md) for resource-specific workflows.

## Manage Lifetime

Placed resources use memory owned by the heap. After rendering has stopped using them, dispose every placed buffer and texture before disposing the heap.

`ResourceHandle` does not own its resource. Keep each placed resource, and any explicit view used to obtain a stored handle, alive through the final submission that uses the handle. Dispose explicit views before their source resources, and update constant data when replacing a stored handle.
