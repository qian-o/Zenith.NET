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

Populate the structure with handles from the resources or views used by the command:

```csharp
ComputeConstants constants = new()
{
    Transform = transform,
    Input = input.StorageReadOnlyHandle,
    Output = output.StorageHandle
};
```

Create a constant buffer and upload the structure:

```csharp
uint constantSize = (uint)Marshal.SizeOf<ComputeConstants>();

using Buffer constantBuffer = context.CreateBuffer(BufferDesc.Constant(constantSize));

constantBuffer.Upload(0, new()
{
    Pointer = (nint)(&constants),
    SizeInBytes = constantSize
});
```

Verify field offsets against the Slang layout, especially when a structure contains vectors, matrices, or nested records.

## Declare Shader Handles

Declare the same fields with typed `DescriptorHandle<T>` values:

```slang
struct ComputeConstants
{
    float4x4 Transform;

    DescriptorHandle<StructuredBuffer<float4>> Input;

    DescriptorHandle<RWTexture2D<float4>> Output;
};

uniform ComputeConstants constants;
```

Use the handles as their declared resource types:

```slang
float4 value = constants.Input[index];
constants.Output[pixel] = value;
```

The generic resource type is part of the C#/shader contract. Its access and element layout must match the handle stored by C#.

## Bind the Constants

Set the pipeline, bind the constant buffer, and issue the command:

```csharp
commandBuffer.SetPipeline(computePipeline);
commandBuffer.SetConstantBuffer(constantBuffer, 0);
commandBuffer.Dispatch(groupCountX, groupCountY, 1);
```

The offset selects the constant record used by the current pipeline.

## Use Views

A resource handle represents the resource's default view. Create an explicit view for a buffer subrange or selected texture range:

```csharp
using BufferView materialView = context.CreateBufferView(BufferViewDesc.StorageReadOnly(
    materialBuffer,
    offsetInBytes,
    sizeInBytes,
    (uint)Marshal.SizeOf<Material>()));

ResourceHandle materials = materialView.StorageReadOnlyHandle;
```

Texture views select mip levels and array layers:

```csharp
using TextureView mipView = context.CreateTextureView(
    TextureViewDesc.Texture2D(texture, texture.Desc.Format, mipLevel, 1));

ResourceHandle sampledMip = mipView.SampledHandle;
```

Views do not own their source resource. Keep both the view and its source alive while submitted work uses the handle.

## Match Usage and Synchronization

A handle does not change a texture layout or create a memory dependency:

- Sampled access requires `TextureUsages.Sampled` and `TextureLayout.Sampled`.
- Storage texture access requires `TextureUsages.Storage` and `TextureLayout.Storage`.
- Storage buffer access requires the matching storage usage.
- Producer/consumer access without a layout change requires a `Barrier`.

`ResourceHandle` does not own its resource. Keep the resource or view alive through the final submission that uses the handle, and update constant data when replacing it.
