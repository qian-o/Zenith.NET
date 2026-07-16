# Bindless Resources

Zenith.NET uses bindless resource handles instead of per-pipeline resource tables. Buffers, textures, views, samplers, and top-level acceleration structures expose compact `ResourceHandle` values that can be stored in constant data and resolved by Slang shaders.

The binding path is:

1. Create a resource with the required usage.
2. Select the handle that represents the intended shader access.
3. Store that handle in an unmanaged constant structure.
4. Upload the structure to a constant buffer.
5. Bind that constant buffer with `SetConstantBuffer`.
6. Declare the matching Slang `DescriptorHandle<T>`.

## Resource Handles

Choose a handle that matches the shader declaration:

| Zenith.NET resource | Handle | Slang target |
|---------------------|--------|--------------|
| `Buffer` / `BufferView` | `ConstantHandle` | `ConstantBuffer<T>` |
| `Buffer` / `BufferView` | `StorageReadOnlyHandle` | `StructuredBuffer<T>` |
| `Buffer` / `BufferView` | `StorageReadWriteHandle` | `RWStructuredBuffer<T>` |
| `Texture` / `TextureView` | `SampledHandle` | `Texture1D`, `Texture2D`, `Texture3D`, or cube texture types |
| `Texture` / `TextureView` | `StorageHandle` | `RWTexture1D`, `RWTexture2D`, or `RWTexture3D` |
| `Sampler` | `Handle` | `SamplerState` or `SamplerComparisonState` |
| `TopLevelAccelerationStructure` | `Handle` | `RaytracingAccelerationStructure` |

Keep the source resource or view alive for every submission that uses its handle.

## Constant Data

Use explicit unmanaged layout when C# data must exactly match a shader structure:

```csharp
using System.Numerics;
using System.Runtime.InteropServices;
using Buffer = Zenith.NET.Buffer;

[StructLayout(LayoutKind.Explicit, Size = 80)]
file struct ComputeConstants
{
    [FieldOffset(0)]
    public Matrix4x4 Transform;

    [FieldOffset(64)]
    public ResourceHandle Input;

    [FieldOffset(72)]
    public ResourceHandle Output;
}
```

Populate the handles from resources or views:

```csharp
ComputeConstants constants = new()
{
    Transform = transform,
    Input = input.StorageReadOnlyHandle,
    Output = output.StorageHandle
};
```

Create a CPU-writable constant buffer and upload the structure:

```csharp
Buffer constantBuffer = context.CreateBuffer(new()
{
    SizeInBytes = (uint)sizeof(ComputeConstants),
    Usages = BufferUsages.Constant,
    Residency = MemoryResidency.CpuWriteOnly
});

unsafe
{
    constantBuffer.Upload(0, new()
    {
        Pointer = (nint)(&constants),
        SizeInBytes = (uint)sizeof(ComputeConstants)
    });
}
```

The structure must be unmanaged. Verify every field offset against the Slang layout rather than relying on accidental CLR padding.

## Slang Declarations

Declare the matching shader structure with typed descriptor handles:

```slang
struct ComputeConstants
{
    float4x4 Transform;

    DescriptorHandle<StructuredBuffer<float4>> Input;

    DescriptorHandle<RWTexture2D<float4>> Output;
};

uniform ComputeConstants constants;
```

Slang implicitly converts `DescriptorHandle<T>` to `T` when the resource is used:

```slang
float4 value = constants.Input[index];
constants.Output[pixel] = value;
```

The generic argument is part of the ABI. It must match the handle's access type and the resource data layout.

## Binding the Constant Buffer

Set the pipeline before binding constants, then provide the byte offset of the selected constant record:

```csharp
commandBuffer.SetPipeline(computePipeline);
commandBuffer.SetConstantBuffer(constantBuffer, 0);
commandBuffer.Dispatch(groupCountX, groupCountY, 1);
```

`SetConstantBuffer` binds constant data at the supplied byte offset for the current pipeline.

## Views

Resources expose handles for their full range. Create a view for a buffer subrange or a selected texture range and format.

```csharp
BufferView materialView = context.CreateBufferView(BufferViewDesc.StorageReadOnly(materialBuffer, offsetInBytes, sizeInBytes, (uint)sizeof(Material)));

ResourceHandle materials = materialView.StorageReadOnlyHandle;
```

Texture views select mip levels and array layers:

```csharp
TextureView mipView = context.CreateTextureView(TextureViewDesc.Texture2D(texture, texture.Desc.Format, mipLevel, 1));

ResourceHandle sampledMip = mipView.SampledHandle;
```

Explicit views have their own lifetime and remain dependent on the source resource.

## Resource Usage and Layout

A handle does not transition a texture or insert a memory dependency. The resource description and command stream must permit the requested access:

- `SampledHandle` requires `TextureUsages.Sampled` and `TextureLayout.Sampled` while accessed.
- `StorageHandle` requires `TextureUsages.Storage` and `TextureLayout.Storage` while accessed.
- Storage buffer handles require the corresponding `BufferUsages.StorageReadOnly` or `StorageReadWrite` usage.
- A producer/consumer dependency without a texture layout change requires `Barrier`.

```csharp
commandBuffer.Transition(output, default, TextureLayout.Undefined, TextureLayout.Storage);
commandBuffer.SetPipeline(computePipeline);
commandBuffer.SetConstantBuffer(constantBuffer, 0);
commandBuffer.Dispatch(groupCountX, groupCountY, 1);
commandBuffer.Transition(output, default, TextureLayout.Storage, TextureLayout.Sampled);
```

See [Synchronization](synchronization.md) for choosing between a barrier, a texture transition, and a timeline wait.

## Handle Lifetime

`ResourceHandle` does not own its resource or view. Keep that owner alive through the final submission that uses the handle, and refresh constant data when replacing the owner.
