# Textures

Textures store formatted one-, two-, or three-dimensional data. A `TextureDesc` defines the shape, format, sample count, and permitted uses. Each texture subresource has a layout supplied by the application when recording commands.

## Create a Standalone Texture

When explicit heap placement is unnecessary, create a standalone texture from the context:

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

Texture texture = context.CreateTexture(desc);
```

Other dimensional helpers include `Texture1D`, `Texture3D`, and `TextureCube`. They create sampled textures with `TransferDst` usage. Attachment helpers create sampled color or depth/stencil render targets.

Add usages to the description before creation when a texture needs another role. For example, add `TextureUsages.Storage` before creating a storage texture.

Usage declares what the texture may do. Layout describes how one subresource is used by recorded commands.

## Create Attachments

Use `TextureDesc.ColorAttachment` and `TextureDesc.DepthStencilAttachment` for render targets. Transition each attachment to its matching layout before beginning a render pass.

## Upload Texture Data

Provide the source pointer together with row and slice strides:

```csharp
texture.Upload(default, TextureLayout.Undefined, TextureLayout.Sampled, default, extent, data);
```

`Texture.Upload` completes before returning. Supply the known current layout and required final layout; on return, the uploaded subresource is in that final layout.

Use `CommandBuffer.Upload` with explicit transitions when several transfers should share one submission.

## Load an Image

`Zenith.NET.Extensions.ImageSharp` loads an image into a sampled texture with `LoadTextureFromFile` or `LoadTextureFromStream`.

Enable `generateMipMaps` when the texture should contain a complete mip chain.

## Select a Subresource

A `TextureSubresource` selects one mip level and array layer. Use `default` for mip level zero and array layer zero, or initialize both fields for another subresource.

Track the current layout of every subresource used by the application. See [Synchronization](../fundamentals/synchronization.md#transition-a-texture) for layout rules.

## Create a Texture View

A `TextureView` selects a type, format, mip range, or array range. Create it from the context with the matching `TextureViewDesc` helper.

The selected range and format must be compatible with the source texture. Views do not own their source texture.

## Create a Sampler

Samplers define filtering and addressing independently from textures. Use a `SamplerDesc` preset when it matches the required behavior.

Other presets include `LinearClamp`, `PointWrap`, `PointClamp`, and `Anisotropic`. Create a custom `SamplerDesc` for comparison sampling, border colors, or a specific LOD range.

Pass `sampler.Handle` beside the sampled texture handle and declare matching `DescriptorHandle<Texture2D>` and `DescriptorHandle<SamplerState>` fields in Slang.

## Resolve and Read Back

Resolve a multisampled texture into a compatible single-sampled texture:

```csharp
commandBuffer.Transition(texture, default, TextureLayout.ColorAttachment, TextureLayout.ResolveSrc);
commandBuffer.Transition(nextTexture, default, TextureLayout.Undefined, TextureLayout.ResolveDst);

commandBuffer.ResolveTexture(texture, default, nextTexture, default);
```

Create resolve and download sources with `TextureUsages.TransferSrc`. Resolve destinations require `TextureUsages.TransferDst`.

Texture copy, resolve, and command-buffer upload or download operations do not insert layout transitions. `Texture.Download` is the synchronous convenience path and performs its declared current-to-final transitions; use `CommandBuffer.Download` with explicit transitions when readback belongs to a larger submission.

See [Heaps](heaps.md) for allocation requirements and explicit texture placement.

Keep a texture alive while any view, handle, or submitted command refers to it. When replacing a size-dependent texture, recreate its views and update constant data that stores its handles.
