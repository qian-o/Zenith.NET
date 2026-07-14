# Textures and Views

Textures store multidimensional formatted data for sampling, storage access, render attachments, copies, resolves, and presentation. A `TextureDesc` defines the shape and permitted usages; each subresource also has a tracked `TextureLayout`.

## Texture Description

```csharp
Texture texture = context.CreateTexture(new()
{
    Type = TextureType.Texture2D,
    Format = PixelFormat.R8G8B8A8UNorm,
    Width = 1024,
    Height = 1024,
    Depth = 1,
    MipLevels = 1,
    ArrayLayers = 1,
    SampleCount = SampleCount.Count1,
    Usages = TextureUsages.Sampled | TextureUsages.TransferDst
});
```

| Field | Description |
|-------|-------------|
| `Type` | Dimensionality, array behavior, and cube interpretation |
| `Format` | Texel or depth/stencil format |
| `Width`, `Height`, `Depth` | Base mip dimensions |
| `MipLevels` | Number of mip levels |
| `ArrayLayers` | Array layers; cube textures use six layers per cube |
| `SampleCount` | Multisample count |
| `Usages` | Permitted texture access roles |

## Texture Types

Zenith.NET supports `Texture1D`, `Texture1DArray`, `Texture2D`, `Texture2DArray`, `Texture3D`, `TextureCube`, and `TextureCubeArray`.

Use the helper descriptions for common shapes:

```csharp
TextureDesc albedoDesc = TextureDesc.Texture2D(PixelFormat.R8G8B8A8UNorm, width, height, mipLevels, SampleCount.Count1);

TextureDesc cubeDesc = TextureDesc.TextureCube(PixelFormat.R16G16B16A16Float, size, mipLevels);
```

Texture helpers create sampled transfer destinations by default. Extend `Usages` when the same texture also needs storage or attachment access.

## Texture Usages

| Usage | Purpose |
|-------|---------|
| `Sampled` | Read through `SampledHandle` |
| `Storage` | Read or write through `StorageHandle` |
| `ColorAttachment` | Use as a color attachment |
| `DepthStencilAttachment` | Use as a depth/stencil attachment |
| `TransferSrc` | Copy or download source |
| `TransferDst` | Copy or upload destination |

Usage declares what a texture may do. Layout declares what one subresource is doing at a particular point in the command stream.

## Attachments

Create color and depth/stencil attachments with the provided helpers:

```csharp
Texture color = context.CreateTexture(TextureDesc.ColorAttachment(PixelFormat.B8G8R8A8UNorm, width, height, 1, SampleCount.Count1));

Texture depthStencil = context.CreateTexture(TextureDesc.DepthStencilAttachment(PixelFormat.D32FloatS8UInt, width, height, SampleCount.Count1));
```

Transition attachments before beginning a render pass:

```csharp
commandBuffer.Transition(color, default, TextureLayout.ColorAttachment);
commandBuffer.Transition(depthStencil, default, TextureLayout.DepthStencilAttachment);
```

## Uploading Data

Provide row and slice strides explicitly:

```csharp
unsafe
{
    fixed (Rgba32* pointer = pixels)
    {
        texture.Upload(default, default, new()
        {
            Width = width,
            Height = height,
            Depth = 1
        }, new()
        {
            Pointer = (nint)pointer,
            SizeInBytes = (uint)(sizeof(Rgba32) * pixels.Length),
            RowStrideInBytes = rowStrideInBytes,
            SliceStrideInBytes = sliceStrideInBytes
        });
    }
}
```

`Texture.Upload` records on the transfer queue and waits. Use `CommandBuffer.Upload` to batch several subresources before one submission.

## ImageSharp Loading

`Zenith.NET.Extensions.ImageSharp` loads RGBA images and optionally creates a full mip chain:

```csharp
using Zenith.NET.Extensions.ImageSharp;

Texture albedo = context.LoadTextureFromFile("Assets/Textures/Albedo.png", generateMipMaps: true);
```

The extension resizes mip images on the CPU, records every mip upload on one transfer command buffer, then submits and waits once.

## Subresources and Layouts

A `TextureSubresource` selects one mip level and array layer:

```csharp
TextureSubresource subresource = new() { MipLevel = mipLevel, ArrayLayer = arrayLayer };

commandBuffer.Transition(texture, subresource, TextureLayout.Sampled);
```

Use `default` for mip level zero and array layer zero. Layouts are tracked independently for all subresources.

Texture transitions include the texture-specific synchronization dependency. See [Synchronization and Barriers](../concepts/synchronization.md) for layout definitions and barrier rules.

## Texture Views

Full textures expose handles through an internal default view. Create an explicit `TextureView` to select another type, format, mip range, or array range:

```csharp
TextureView mipView = context.CreateTextureView(TextureViewDesc.Texture2D(texture, texture.Desc.Format, mipLevel, 1));

ResourceHandle sampledMip = mipView.SampledHandle;
```

Helper descriptions cover all supported texture types. Cube helpers accept cube indices and convert them to six-layer ranges.

Explicit views expose `SampledHandle` and `StorageHandle`. Their selected range must be compatible with the underlying texture description and usage.

## Multisampling and Resolve

Create a multisampled color attachment and a single-sampled resolve target with matching formats:

```csharp
commandBuffer.ResolveTexture(msaaColor, default, resolvedColor, default);
```

`ResolveTexture` temporarily transitions the source and destination to their resolve layouts, performs the resolve, and restores their tracked layouts.

## Copies and Downloads

Command buffers support texture-to-texture, buffer-to-texture, and texture-to-buffer copies. The copy helpers transition affected texture subresources to copy layouts and restore them afterward.

`Texture.Download` is synchronous and writes to a `TextureData` destination. Record `CommandBuffer.Download` directly when readback should remain part of a larger command sequence.

## Wrapped Native Textures

`CreateTexture(desc, nativeTextureType, nativeTexture)` wraps a supported native texture handle. The description must match the underlying native resource.

Use `OverrideLayout` when importing a texture whose current layout is known outside Zenith.NET:

```csharp
texture.OverrideLayout(default, TextureLayout.Sampled);
```

`OverrideLayout` only changes Zenith.NET's tracked state. It does not record a GPU transition.

## Lifetime and Resizing

Dispose explicit views before the source texture. Recreate size-dependent textures when the drawable dimensions change, then update every constant structure that stores their handles.

Do not dispose `SwapChain.Drawable`; the swap chain owns it.
