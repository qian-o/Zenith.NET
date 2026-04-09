# Samplers

Samplers define how textures are read in shaders — controlling filtering, address wrapping, and level of detail.

## Creating a Sampler

```csharp
Sampler sampler = context.CreateSampler(new SamplerDesc
{
    U = AddressMode.Wrap,
    V = AddressMode.Wrap,
    W = AddressMode.Wrap,
    Filter = Filter.MinLinearMagLinearMipLinear,
    MaxLod = uint.MaxValue
});
```

### SamplerDesc

| Field | Type | Description |
|-------|------|-------------|
| `U` | `AddressMode` | Horizontal address mode |
| `V` | `AddressMode` | Vertical address mode |
| `W` | `AddressMode` | Depth address mode (3D textures) |
| `Filter` | `Filter` | Minification, magnification, and mipmap filtering |
| `ComparisonFunc` | `ComparisonFunc` | Comparison function for shadow sampling |
| `MaxAnisotropy` | `uint` | Maximum anisotropic filtering level |
| `MinLod` | `float` | Minimum mip LOD clamp |
| `MaxLod` | `float` | Maximum mip LOD clamp |
| `LodBias` | `float` | LOD bias offset |
| `BorderColor` | `BorderColor` | Border color when using `AddressMode.Border` |

## Address Modes

Address modes control what happens when UV coordinates fall outside `[0, 1]`:

| Mode | Behavior |
|------|----------|
| `Wrap` | Tile the texture by repeating |
| `Mirror` | Tile with mirrored repetition |
| `Clamp` | Clamp to the edge texel |
| `Border` | Return the border color |

## Filter Modes

Filter modes combine minification, magnification, and mipmap filters:

| Filter | Min | Mag | Mip |
|--------|-----|-----|-----|
| `MinPointMagPointMipPoint` | Point | Point | Point |
| `MinLinearMagLinearMipLinear` | Linear | Linear | Linear |
| `MinLinearMagLinearMipPoint` | Linear | Linear | Point |
| `Anisotropic` | Anisotropic | Anisotropic | Linear |

All 9 combinations of Point/Linear for each stage are available. `Anisotropic` provides the highest quality and should be paired with `MaxAnisotropy`.

### Anisotropic Filtering

```csharp
Sampler sampler = context.CreateSampler(new()
{
    U = AddressMode.Wrap,
    V = AddressMode.Wrap,
    W = AddressMode.Wrap,
    Filter = Filter.Anisotropic,
    MaxAnisotropy = 16,
    MaxLod = uint.MaxValue
});
```

## Level of Detail

| Field | Purpose |
|-------|---------|
| `MinLod` | Clamp the minimum mip level (0 = highest resolution) |
| `MaxLod` | Clamp the maximum mip level (`uint.MaxValue` = allow all levels) |
| `LodBias` | Shift the computed LOD (positive = blurrier, negative = sharper) |

## Shader Usage

In Slang/HLSL shaders, declare a `SamplerState` and sample textures:

```hlsl
Texture2D albedo;
SamplerState linearSampler;

float4 color = albedo.Sample(linearSampler, uv);
```

Bind the sampler to a resource table at the corresponding binding index:

```csharp
resourceTable.Write(1, sampler);
```
