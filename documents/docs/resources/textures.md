# Textures

Textures store formatted one-, two-, or three-dimensional data. A `TextureDesc` defines the shape, format, sample count, and permitted uses. Each texture subresource has a layout supplied by the application when recording commands.

## Create a Texture

Use a helper for common shapes:

```csharp
using Texture albedo = context.CreateTexture(
    TextureDesc.Texture2D(PixelFormat.R8G8B8A8UNorm, width, height, mipLevels, SampleCount.Count1));
using Texture environment = context.CreateTexture(
    TextureDesc.TextureCube(PixelFormat.R16G16B16A16Float, size, mipLevels));
```

Dimensional helpers create sampled textures with `TransferDst` usage. Attachment helpers create sampled color or depth/stencil render targets.

Add usages before creation when a texture needs another role:

```csharp
TextureDesc outputDesc = TextureDesc.Texture2D(PixelFormat.R8G8B8A8UNorm, width, height, 1, SampleCount.Count1);

outputDesc.Usages |= TextureUsages.Storage;

using Texture output = context.CreateTexture(outputDesc);
```

Usage declares what the texture may do. Layout describes how one subresource is used by recorded commands.

## Create Attachments

Use the attachment helpers for render targets:

```csharp
using Texture color = context.CreateTexture(
    TextureDesc.ColorAttachment(PixelFormat.B8G8R8A8UNorm, width, height, 1, SampleCount.Count1));
using Texture depthStencil = context.CreateTexture(
    TextureDesc.DepthStencilAttachment(PixelFormat.D32FloatS8UInt, width, height, SampleCount.Count1));
```

Transition each attachment before beginning a render pass:

```csharp
commandBuffer.Transition(color, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
```

## Upload Texture Data

Provide the source pointer together with row and slice strides:

```csharp
fixed (Rgba32* source = pixels)
{
    texture.Upload(
        default,
        TextureLayout.Undefined,
        TextureLayout.Sampled,
        default,
        new()
        {
            Width = width,
            Height = height,
            Depth = 1
        },
        new()
        {
            Pointer = (nint)source,
            SizeInBytes = (uint)(sizeof(Rgba32) * pixels.Length),
            RowStrideInBytes = rowStrideInBytes,
            SliceStrideInBytes = sliceStrideInBytes
        });
}
```

`Texture.Upload` transitions through `CopyDst`, submits the transfer, and waits before returning. Supply the known current layout and the layout required after the upload.

Use `CommandBuffer.Upload` with explicit transitions when several transfers should share one submission.

## Load an Image

`Zenith.NET.Extensions.ImageSharp` loads an image into a sampled texture:

```csharp
using Zenith.NET.Extensions.ImageSharp;

using Texture albedo = context.LoadTextureFromFile("Assets/Textures/Albedo.png", generateMipMaps: true);
```

Enable `generateMipMaps` when the texture should contain a complete mip chain.

## Select a Subresource

A `TextureSubresource` selects one mip level and array layer:

```csharp
commandBuffer.Transition(texture, new()
{
    MipLevel = mipLevel,
    ArrayLayer = arrayLayer
}, currentLayout, TextureLayout.Sampled);
```

Use `default` for mip level zero and array layer zero. Track the current layout of every subresource used by the application. See [Synchronization](../fundamentals/synchronization.md#transition-a-texture) for layout rules.

## Create a Texture View

A `TextureView` selects a type, format, mip range, or array range:

```csharp
using TextureView mipView = context.CreateTextureView(
    TextureViewDesc.Texture2D(texture, texture.Desc.Format, mipLevel, 1));

ResourceHandle sampledMip = mipView.SampledHandle;
```

The selected range and format must be compatible with the source texture. Views do not own their source texture.

## Create a Sampler

Samplers define filtering and addressing independently from textures. Use a preset when it matches the required behavior:

```csharp
using Sampler sampler = context.CreateSampler(SamplerDesc.LinearWrap());
```

Other presets include `LinearClamp`, `PointWrap`, `PointClamp`, and `Anisotropic`. Create a custom `SamplerDesc` for comparison sampling, border colors, or a specific LOD range.

Pass `sampler.Handle` beside the sampled texture handle:

```slang
DescriptorHandle<Texture2D> Texture;
DescriptorHandle<SamplerState> Sampler;

float4 color = Texture.Sample(Sampler, uv);
```

## Resolve and Read Back

Resolve a multisampled texture into a compatible single-sampled texture:

```csharp
commandBuffer.Transition(msaaColor, default, TextureLayout.ColorAttachment, TextureLayout.ResolveSrc);
commandBuffer.Transition(resolvedColor, default, TextureLayout.Undefined, TextureLayout.ResolveDst);
commandBuffer.ResolveTexture(msaaColor, default, resolvedColor, default);
```

Create resolve and download sources with `TextureUsages.TransferSrc`. Resolve destinations require `TextureUsages.TransferDst`.

Texture copy, resolve, and command-buffer upload or download operations do not insert layout transitions. `Texture.Download` is the synchronous convenience path and performs its declared current-to-final transitions; use `CommandBuffer.Download` with explicit transitions when readback belongs to a larger submission.

Keep a texture alive while any view, handle, or submitted command refers to it. When replacing a size-dependent texture, recreate its views and update constant data that stores its handles.
