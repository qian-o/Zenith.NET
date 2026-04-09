# Textures

Textures are GPU image resources used for rendering, sampling, compute read/write, and render targets.

## Creating a Texture

```csharp
Texture texture = context.CreateTexture(new TextureDesc
{
    Type = TextureType.Texture2D,
    Format = PixelFormat.R8G8B8A8UNorm,
    Width = 512,
    Height = 512,
    Depth = 1,
    MipLevels = 1,
    ArrayLayers = 1,
    SampleCount = SampleCount.Count1,
    Flags = TextureUsageFlags.ShaderResource
});
```

### TextureDesc

| Field | Type | Description |
|-------|------|-------------|
| `Type` | `TextureType` | Dimensionality and array/cube configuration |
| `Format` | `PixelFormat` | Pixel format (color, depth, compressed) |
| `Width` | `uint` | Width in pixels |
| `Height` | `uint` | Height in pixels |
| `Depth` | `uint` | Depth (for 3D textures, otherwise `1`) |
| `MipLevels` | `uint` | Number of mipmap levels |
| `ArrayLayers` | `uint` | Number of array layers |
| `SampleCount` | `SampleCount` | Multisample count |
| `Flags` | `TextureUsageFlags` | Usage flags |

### Texture Types

| Type | Description |
|------|-------------|
| `Texture1D` | 1D texture |
| `Texture1DArray` | Array of 1D textures |
| `Texture2D` | Standard 2D texture |
| `Texture2DArray` | Array of 2D textures |
| `Texture3D` | Volume texture |
| `TextureCube` | Cube map (6 faces) |
| `TextureCubeArray` | Array of cube maps |

### Usage Flags

| Flag | Description |
|------|-------------|
| `RenderTarget` | Color render target attachment |
| `DepthStencil` | Depth/stencil render target attachment |
| `ShaderResource` | Read-only in shaders (`Texture2D`, etc.) |
| `UnorderedAccess` | Read-write in compute shaders (`RWTexture2D`, etc.) |

## Pixel Formats

Common formats:

| Category | Formats |
|----------|---------|
| Color (8-bit) | `R8G8B8A8UNorm`, `B8G8R8A8UNorm`, `R8G8B8A8SRgb` |
| Color (16-bit) | `R16G16B16A16Float` |
| Color (32-bit) | `R32G32B32A32Float` |
| Depth | `D16UNorm`, `D32Float` |
| Depth + Stencil | `D24UNormS8UInt`, `D32FloatS8UInt` |
| Compressed | `BC7UNorm`, `BC7SRgb`, `ETC2R8G8B8A8UNorm`, `ASTC4x4UNorm` |

## Uploading Data

Upload pixel data directly:

```csharp
byte[] pixelData = [ /* ... */ ];
texture.Upload(pixelData, slice: default, offset: default, extent: new()
{
    Width = texture.Desc.Width,
    Height = texture.Desc.Height,
    Depth = 1
});
```

### Loading from File

With the `Zenith.NET.Extensions.ImageSharp` package:

```csharp
Texture texture = context.LoadTextureFromFile("image.png", generateMipMaps: true);
```

## Texture Views

A `TextureView` references a subset of mip levels or array layers:

```csharp
TextureView view = context.CreateTextureView(new TextureViewDesc
{
    Texture = texture,
    FirstMipLevel = 0,
    MipLevelCount = 1,
    FirstArrayLayer = 0,
    ArrayLayerCount = 1
});
```

Texture views implement `IBindableResource` and can be written to resource tables.

## Mipmaps

Set `MipLevels` to the desired number of levels, or use the `generateMipMaps` parameter when loading from file. In shaders, configure the sampler's `MaxLod` to allow mipmap sampling:

```csharp
Sampler sampler = context.CreateSampler(new()
{
    Filter = Filter.MinLinearMagLinearMipLinear,
    MaxLod = uint.MaxValue
});
```

## Multisampling

Set `SampleCount` to `Count2`, `Count4`, `Count8`, `Count16`, or `Count32`. Multisampled textures can be resolved to single-sampled targets:

```csharp
commandBuffer.ResolveTexture(msaaTexture, slice, resolvedTexture, slice);
```
