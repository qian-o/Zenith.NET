# Bindless Resources

Shaders access Zenith.NET resources through `ResourceHandle` values. Store the handles in constant data, upload that data to a constant buffer, and declare matching typed handles in Slang.

## Select a Handle

Choose the handle that matches the shader access:

| C# resource | Handle | Slang resource |
|--------------|--------|----------------|
| `Buffer` / `BufferView` | `ConstantHandle` | `ConstantBuffer<T>` |
| `Buffer` / `BufferView` | `StorageReadOnlyHandle` | `StructuredBuffer<T>` |
| `Buffer` / `BufferView` | `StorageReadWriteHandle` | `RWStructuredBuffer<T>` |
| `Texture` / `TextureView` | `SampledHandle` | `Texture1D`, `Texture2D`, `Texture3D`, or cube texture types |
| `Texture` / `TextureView` | `StorageHandle` | `RWTexture1D`, `RWTexture2D`, or `RWTexture3D` |
| `Sampler` | `Handle` | `SamplerState` or `SamplerComparisonState` |
| `TopLevelAccelerationStructure` | `Handle` | `RaytracingAccelerationStructure` |

The resource description must include the usage required by the selected handle.

## Define Constant Data

Use an unmanaged C# structure whose layout matches the shader structure:

```csharp
[StructLayout(LayoutKind.Explicit, Size = 8)]
file struct Constants
{
    [FieldOffset(0)]
    public ResourceHandle Resource;
}
```

Populate the structure with handles from the resources or views used by the command:

```csharp
Constants constants = new() { Resource = texture.SampledHandle };
```

Create a constant buffer and upload the structure:

```csharp
BufferDesc desc = new()
{
    SizeInBytes = sizeof(Constants),
    StrideInBytes = 0,
    Usages = BufferUsages.Constant | BufferUsages.TransferDst,
    Residency = MemoryResidency.GpuOnly
};

Buffer buffer = context.CreateBuffer(desc);
buffer.Upload(0, new()
{
    Pointer = &constants,
    SizeInBytes = sizeof(Constants)
});
```

Verify field offsets against the Slang layout, especially when a structure contains vectors, matrices, or nested records.

## Declare Shader Handles

Declare matching typed handles and expose the record as `ConstantBuffer<T>`:

```slang
struct Constants
{
    DescriptorHandle<Texture2D> Resource;
};

ConstantBuffer<Constants> constants;
```

## Bind the Constants

Bind the constant buffer after setting the pipeline:

```csharp
commandBuffer.SetConstantBuffer(buffer, 0);
```

The second argument is the byte offset into the buffer.

## Use Views

`Buffer` and `Texture` expose handles for the whole resource. Create a `BufferView` when a shader needs a byte subrange with a specific stride, and create a `TextureView` when it needs selected mip levels or array layers. Use `sizeof(T)` for typed buffer sizes and strides.

Views do not own their source resource. Keep both the view and its source alive while submitted work uses the handle.

## Match Usage and Synchronization

A handle does not change a texture layout or create a memory dependency:

- Sampled access requires `TextureUsages.Sampled` and `TextureLayout.Sampled`.
- Storage texture access requires `TextureUsages.Storage` and `TextureLayout.Storage`.
- Storage buffer access requires the matching storage usage.
- Producer/consumer access without a layout change requires a `Barrier`.

`ResourceHandle` does not own its resource. Keep the resource or view alive through the final submission that uses the handle, and update constant data when replacing it.
