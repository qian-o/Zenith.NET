#ifndef SPONZA_MATERIAL
#define SPONZA_MATERIAL

struct MaterialConstants
{
    float4 BaseColorFactor;

    float MetallicFactor;

    float RoughnessFactor;

    float NormalScale;

    float AlphaCutoff;

    DescriptorHandle<Texture2D<float4>> BaseColor;

    DescriptorHandle<Texture2D<float4>> MetallicRoughness;

    DescriptorHandle<Texture2D<float4>> Normal;

    uint Flags;

    uint Padding;
};

float3 DecodeSrgb(float3 color)
{
    return select(color <= 0.04045, color / 12.92, pow((color + 0.055) / 1.055, 2.4));
}

float4 SampleBaseColor(MaterialConstants material, float2 uv, SamplerState materialSampler)
{
    float4 color = float4(1.0);
    if ((material.Flags & 1u) != 0u)
    {
        color = material.BaseColor.Sample(materialSampler, uv);
        color.rgb = DecodeSrgb(color.rgb);
    }

    return color * material.BaseColorFactor;
}

void ClipMaterial(MaterialConstants material, float alpha)
{
    if ((material.Flags & 8u) != 0u && alpha < material.AlphaCutoff)
    {
        discard;
    }
}

#endif
