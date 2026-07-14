# Bindless Resources

Zenith.NET uses bindless resource handles instead of per-pipeline resource tables. Buffers, textures, views, samplers, and top-level acceleration structures expose compact `ResourceHandle` values that can be stored in constant data and resolved by Slang shaders.

The binding path is:

1. Create a resource with the required usage.
2. Select the handle that represents the intended shader access.
3. Store that handle in an unmanaged constant structure.
4. Upload the structure to a constant buffer.
5. Bind that constant buffer with `SetConstantBuffer`.
6. Resolve the handle through Slang `DescriptorHandle<T>`.

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

Requesting a handle creates or resolves the corresponding graphics API descriptor as needed. Keep the source resource or view alive for every submission that can use the handle.

## Constant Data

Use explicit unmanaged layout when C# data must exactly match a shader structure:

```csharp
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

constantBuffer.Upload(0, new()
{
    Pointer = (nint)(&constants),
    SizeInBytes = (uint)sizeof(ComputeConstants)
});
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

Dereference a handle before indexing or invoking resource operations:

```slang
float4 value = (*constants.Input)[index];
(*constants.Output)[pixel] = value;
```

The generic argument is part of the ABI. It must match the handle's access type and the resource data layout.

## Binding the Constant Buffer

Set the pipeline before binding constants, then provide the byte offset of the selected constant record:

```csharp
commandBuffer.SetPipeline(computePipeline);
commandBuffer.SetConstantBuffer(constantBuffer, 0);
commandBuffer.Dispatch(groupCountX, groupCountY, 1);
```

`SetConstantBuffer` binds one constant-buffer record for the current pipeline. Store several aligned records in one buffer and vary `offsetInBytes` when issuing many draws or dispatches.

## Views

Resources expose handles for their default full-resource views. Create an explicit view when a shader should see only a subrange or when a texture needs another view type or format.

```csharp
BufferView materialView = context.CreateBufferView(BufferViewDesc.StorageReadOnly(materialBuffer, offsetInBytes, sizeInBytes, (uint)sizeof(Material)));

ResourceHandle materials = materialView.StorageReadOnlyHandle;
```

Texture views select mip levels and array layers:

```csharp
TextureView mipView = context.CreateTextureView(TextureViewDesc.Texture2D(texture, texture.Desc.Format, mipLevel, 1));

ResourceHandle sampledMip = mipView.SampledHandle;
```

Dispose explicit views after the last submission that uses their handles. Disposing a resource also disposes its internal default view, but it does not dispose separately created views.

## Resource Usage and Layout

A handle does not transition a texture or insert a memory dependency. The resource description and command stream must permit the requested access:

- `SampledHandle` requires `TextureUsages.Sampled` and `TextureLayout.Sampled` while accessed.
- `StorageHandle` requires `TextureUsages.Storage` and `TextureLayout.Storage` while accessed.
- Storage buffer handles require the corresponding `BufferUsages.StorageReadOnly` or `StorageReadWrite` usage.
- A producer/consumer dependency without a texture layout change requires `Barrier`.

```csharp
commandBuffer.Transition(output, default, TextureLayout.Storage);
commandBuffer.SetPipeline(computePipeline);
commandBuffer.SetConstantBuffer(constantBuffer, 0);
commandBuffer.Dispatch(groupCountX, groupCountY, 1);
commandBuffer.Transition(output, default, TextureLayout.Sampled);
```

See [Synchronization and Barriers](synchronization.md) for choosing between a barrier, a texture transition, and a timeline wait.

## Handle Lifetime

Treat a `ResourceHandle` as a non-owning reference:

- Keep the resource or view alive while GPU work can dereference the handle.
- Do not cache a handle after disposing its owner.
- Rebuild constant data when replacing a resized texture or view.
- Wait for the final dependent submission before disposing an owner.

Handles are portable values inside Zenith.NET's shader ABI. Their internal representation is graphics API-specific and should not be decoded by application code.
