# Buffers

Buffers are linear GPU memory used for vertex data, index data, constant data, structured data, and indirect draw arguments.

## Creating a Buffer

```csharp
Buffer buffer = context.CreateBuffer(new BufferDesc
{
    SizeInBytes = 1024,
    StrideInBytes = 16,
    Flags = BufferUsageFlags.Vertex | BufferUsageFlags.MapWrite
});
```

### BufferDesc

| Field | Type | Description |
|-------|------|-------------|
| `SizeInBytes` | `uint` | Total buffer size in bytes |
| `StrideInBytes` | `uint` | Element stride (e.g., vertex size, struct size) |
| `Flags` | `BufferUsageFlags` | Usage flags that determine how the buffer can be used |

### Usage Flags

| Flag | Description |
|------|-------------|
| `Vertex` | Vertex buffer binding |
| `Index` | Index buffer binding |
| `Indirect` | Indirect draw/dispatch argument source |
| `AccelerationStructure` | Input for BLAS/TLAS builds |
| `Constant` | Constant buffer for shader uniforms |
| `ShaderResource` | Read-only structured buffer in shaders |
| `UnorderedAccess` | Read-write buffer in compute shaders |
| `MapRead` | CPU-readable via `Map()` |
| `MapWrite` | CPU-writable via `Map()` |

Flags can be combined: `BufferUsageFlags.Vertex | BufferUsageFlags.MapWrite`.

## Uploading Data

The simplest way to fill a buffer is `Upload()`:

```csharp
Vertex[] vertices = [ /* ... */ ];
buffer.Upload(vertices, offsetInBytes: 0);
```

`Upload()` works on any buffer. If `MapRead` or `MapWrite` is set, it uses direct CPU mapping; otherwise, it internally uploads through the Copy queue.

## Map / Unmap

For fine-grained control over CPU access, use `Map()` and `Unmap()`:

```csharp
MappedMemory mapped = buffer.Map();

// Write directly to GPU memory
Unsafe.CopyBlock((void*)mapped.Pointer, source, mapped.SizeInBytes);

buffer.Unmap();
```

`MappedMemory` provides a `Pointer` (native address) and `SizeInBytes`.

## Buffer Views

A `BufferView` references a sub-region of a buffer for shader binding:

```csharp
BufferView view = context.CreateBufferView(new BufferViewDesc
{
    Buffer = buffer,
    OffsetInBytes = 0,
    SizeInBytes = 512,
    StrideInBytes = 16
});
```

Buffer views implement `IBindableResource` and can be written to resource tables.

## Buffer Roles by Flag

| Role | Required Flags | Shader Type |
|------|---------------|-------------|
| Vertex buffer | `Vertex` | — (bound via `SetVertexBuffer`) |
| Index buffer | `Index` | — (bound via `SetIndexBuffer`) |
| Constant buffer | `Constant` | `ConstantBuffer<T>` |
| Structured buffer (read) | `ShaderResource` | `StructuredBuffer<T>` |
| Structured buffer (read-write) | `UnorderedAccess` | `RWStructuredBuffer<T>` |
| Indirect arguments | `Indirect` | — (passed to `Draw*Indirect` / `DispatchIndirect`) |
| Acceleration structure input | `AccelerationStructure` | — (used in BLAS builds) |

## Command Buffer Uploads

You can also upload data through a `CommandBuffer` for GPU-timeline uploads:

```csharp
commandBuffer.Upload(buffer, offsetInBytes, dataSpan);
```
