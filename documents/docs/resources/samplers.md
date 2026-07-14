# Samplers

Samplers define filtering, address modes, comparison behavior, anisotropy, and level-of-detail selection independently from textures. Shaders access them through a bindless `ResourceHandle`.

## Common Samplers

`SamplerDesc` provides helpers for the most common combinations:

```csharp
Sampler linearWrap = context.CreateSampler(SamplerDesc.LinearWrap());
Sampler linearClamp = context.CreateSampler(SamplerDesc.LinearClamp());
Sampler pointWrap = context.CreateSampler(SamplerDesc.PointWrap());
Sampler pointClamp = context.CreateSampler(SamplerDesc.PointClamp());
Sampler anisotropic = context.CreateSampler(SamplerDesc.Anisotropic(16));
```

These helpers configure all descriptor fields, including comparison behavior, LOD range, and border color.

## Sampler Description

Create a custom sampler when the helper descriptions do not match the workload:

```csharp
Sampler sampler = context.CreateSampler(new()
{
    MinFilter = FilterMode.Linear,
    MagFilter = FilterMode.Linear,
    MipFilter = FilterMode.Point,
    AddressU = AddressMode.Wrap,
    AddressV = AddressMode.Wrap,
    AddressW = AddressMode.Clamp,
    CompareOp = CompareOp.Never,
    MaxAnisotropy = 1,
    LodBias = 0.0f,
    MinLod = 0.0f,
    MaxLod = float.MaxValue,
    BorderColor = BorderColor.TransparentBlack
});
```

| Field | Purpose |
|-------|---------|
| `MinFilter` | Filtering when a texture is minified |
| `MagFilter` | Filtering when a texture is magnified |
| `MipFilter` | Filtering between mip levels |
| `AddressU`, `AddressV`, `AddressW` | Addressing outside the normalized texture range |
| `CompareOp` | Comparison sampling operation; `Never` disables comparison sampling |
| `MaxAnisotropy` | Maximum anisotropy; values greater than one enable anisotropic filtering |
| `LodBias` | Bias added to the selected mip level |
| `MinLod`, `MaxLod` | Allowed mip-level range |
| `BorderColor` | Value returned by border addressing |

## Filter Modes

`FilterMode.Point` selects the nearest value. `FilterMode.Linear` interpolates adjacent values. Minification, magnification, and mip filtering are selected independently.

Anisotropic filtering uses linear filtering with `MaxAnisotropy` greater than one:

```csharp
Sampler sampler = context.CreateSampler(SamplerDesc.Anisotropic(8));
```

Choose a value supported by the target devices. Higher values improve oblique texture sampling at additional cost.

## Address Modes

| Mode | Behavior outside the texture range |
|------|------------------------------------|
| `Wrap` | Repeat the texture |
| `Mirror` | Repeat and mirror alternating intervals |
| `Clamp` | Clamp to the edge texel |
| `Border` | Return `BorderColor` |

Configure all three axes even for a 2D texture so the sampler description remains explicit and portable.

## Comparison Sampling

Set `CompareOp` to a comparison other than `Never` for shadow or depth comparisons:

```csharp
Sampler shadowSampler = context.CreateSampler(new()
{
    MinFilter = FilterMode.Linear,
    MagFilter = FilterMode.Linear,
    MipFilter = FilterMode.Linear,
    AddressU = AddressMode.Clamp,
    AddressV = AddressMode.Clamp,
    AddressW = AddressMode.Clamp,
    CompareOp = CompareOp.LessEqual,
    MaxAnisotropy = 1,
    LodBias = 0.0f,
    MinLod = 0.0f,
    MaxLod = float.MaxValue,
    BorderColor = BorderColor.OpaqueWhite
});
```

Use a matching Slang `SamplerComparisonState` declaration and comparison sampling operation.

## Bindless Shader Access

Store the sampler handle beside the sampled texture handle:

```csharp
[StructLayout(LayoutKind.Explicit, Size = 16)]
file struct TextureConstants
{
    [FieldOffset(0)]
    public ResourceHandle Texture;

    [FieldOffset(8)]
    public ResourceHandle Sampler;
}
```

The matching Slang structure is:

```slang
struct TextureConstants
{
    DescriptorHandle<Texture2D> Texture;

    DescriptorHandle<SamplerState> Sampler;
};

uniform TextureConstants constants;
```

Dereference both handles when sampling:

```slang
float4 color = (*constants.Texture).Sample(*constants.Sampler, uv);
```

See [Bindless Resources](../concepts/resource-binding.md) for constant-buffer layout and lifetime rules.

## Lifetime

A sampler handle does not own the sampler. Keep the `Sampler` alive until every submission that can dereference its handle has completed, then dispose it deterministically.
