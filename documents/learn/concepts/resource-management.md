# Resource Management

Choose a resource description from the operations that will use it. A texture rendered into, sampled by a shader and downloaded to the CPU needs all three uses declared, along with the synchronization between them. Memory placement and resource lifetime are additional decisions.

## Declare the required uses before creating a resource

[`BufferUsages`](xref:Zenith.NET.BufferUsages) and [`TextureUsages`](xref:Zenith.NET.TextureUsages) are flag enums that declare a resource's intended roles. They are assigned to `BufferDesc.Usages` and `TextureDesc.Usages`, respectively. A flag enum allows several members to be combined with `|` when a resource has more than one role.

### Buffer usages

| Usage | Role |
| --- | --- |
| `None` | No buffer usage flags are declared. |
| `Vertex` | Vertex attributes fetched for drawing. |
| `Index` | Indices used by indexed drawing. |
| `Indirect` | Arguments read by indirect draw or dispatch commands. |
| `Constant` | Shader parameters read through a constant-buffer binding. |
| `StorageReadOnly` | Buffer data accessed through a read-only shader binding. |
| `StorageReadWrite` | Buffer data accessed through a read/write shader binding. |
| `TransferSrc` | Source data for a GPU copy. |
| `TransferDst` | Destination storage for a GPU copy. |

### Texture usages

| Usage | Role |
| --- | --- |
| `None` | No texture usage flags are declared. |
| `Sampled` | Texture data read through a sampled-texture binding. |
| `Storage` | Texture data accessed through a storage-texture binding for shader reads or writes. |
| `ColorAttachment` | A color attachment in a render pass. |
| `DepthStencilAttachment` | A depth/stencil attachment in a render pass. |
| `TransferSrc` | Source image data for a GPU copy. |
| `TransferDst` | Destination image storage for a GPU copy. |

`TransferSrc` and `TransferDst` describe the direction of GPU copies. CPU access is a separate memory-placement choice, described below.

Combining usage flags declares several roles; it does not perform the operations or order their accesses. Textures still need the appropriate [layout and synchronization](synchronization.md#layouts), and their formats must support the requested uses on the selected device.

<a id="memory-placement"></a>
## Choose memory for the CPU access you need

A [`BufferDesc`](xref:Zenith.NET.BufferDesc) has a `Residency` field. Choose it according to how the CPU and GPU will access the data:

| Residency | Typical role | CPU access |
| --- | --- | --- |
| `GpuOnly` | Static geometry and GPU working data | Transfer bytes through an upload or readback buffer. |
| `CpuReadOnly` | Readback data produced by the GPU | Read mapped memory after the GPU copy finishes. |
| `CpuWriteOnly` | Data supplied or updated by the CPU | Write mapped memory after previous GPU readers finish. |

Residency describes the intended CPU access to the allocation. It does not imply that every device has separate physical CPU and GPU memory. The triangle uses `CpuWriteOnly` to initialize a small vertex buffer directly. A large static mesh may instead benefit from GPU-only storage with a one-time upload.

`TextureDesc` has no residency field. Ordinary context-created textures use GPU storage in the backends; the public texture API provides upload and download operations rather than a `Map()` method. Explicit heaps provide placement control, described below.

For a CPU-accessible buffer, `Map()` gives the CPU an address. It does not wait for the GPU to stop accessing the same bytes. `Unmap()` likewise does not submit work or wait for completion. Before overwriting CPU-writable data, finish the GPU reads that still need its old contents.

## Choose between an immediate transfer and recorded transfers

An upload copies CPU data toward a GPU resource; a download copies resource data back to CPU memory. The resource-level methods complete that transfer before returning:

| Method | How it transfers data |
| --- | --- |
| `Buffer.Upload` | Direct CPU copy for `CpuWriteOnly`; otherwise submits a transfer-queue upload and waits. |
| `Buffer.Download` | Direct CPU copy for `CpuReadOnly`; otherwise submits a transfer-queue download and waits. |
| `Texture.Upload` / `Texture.Download` | Use the graphics queue, transition the selected subresource for the copy, transition to the requested final layout, and wait. |

Those waits cover the transfer they submit. They do not discover dependencies on a different queue that was already reading or writing the resource. Establish those dependencies before calling the resource-level method. A direct mapped copy also relies on the caller to ensure GPU completion.

Use `CommandBuffer.Upload` or `CommandBuffer.Download` when copies should be recorded with other work. Their pointer lifetimes differ:

- An upload copies the CPU source into an internal upload buffer, also called staging memory, during the call. The source pointer only needs to remain valid for that call.
- A download records a GPU copy into internal readback storage. The library copies those bytes into the supplied CPU destination when it resets the completed command buffer for reuse. Keep the destination allocated, and keep managed memory pinned, until the submission value's `Wait()` returns.

For a texture, a row stride is the byte distance between successive rows of CPU data, and a slice stride is the byte distance between successive depth slices. They are not the texture's internal GPU memory layout. See [Synchronization](synchronization.md#cpu-and-gpu) for the completion and readback distinction.

## Place resources in a heap when allocation control is needed

A [`Heap`](xref:Zenith.NET.Heap) supplies memory in which you place resources at explicit byte offsets. The application chooses those offsets and tracks which regions are in use.

Use `context.GetSizeAndAlignment` with the exact buffer or texture description before placing it. Choose an offset that is a multiple of the returned alignment, reserve the returned size, and keep the resource inside the heap's total byte size. Keep live allocations in non-overlapping regions. Texture storage can include backend-specific padding and alignment, so pixel count multiplied by bytes per pixel is not an allocation-size calculation.

For placed buffers, keep the buffer's residency consistent with the heap's residency. Use GPU-only heaps for the ordinary GPU textures described here. Size and alignment are placement requirements, not a promise that any resource can be placed into any heap on every backend.

Start with context-created resources unless you need an allocator of your own. When using a heap, dispose its placed resources before the heap itself, after all GPU work that uses them finishes.

<a id="views-and-lifetime"></a>
## Keep views and their backing resources usable together

A [`BufferView`](xref:Zenith.NET.BufferView) describes a buffer region and its interpretation. A [`TextureView`](xref:Zenith.NET.TextureView) selects a compatible texture type, format and subresource range. A view shares the backing storage; it does not copy the data or add usages that were missing when the resource was created. Its range is also not a portable guarantee of shader bounds checking.

Use the resource's own handle for its default view, or create a view when a different region or compatible interpretation is needed. Keep C# references to the view and its backing resource while GPU work can use them; a resource handle alone does not keep either object alive. Dispose them only after their last GPU use has completed. After replacing either one, update constants that contain its old [resource handle](shader-data-and-binding.md#resource-handles).

The [spinning cube](../samples.md#spinning-cube) shows a depth texture replaced on resize. The [compute sample](../samples.md#compute-shader) shows an input texture kept separate from its writable output. Both make the resource's lifetime follow the work that actually uses it.
