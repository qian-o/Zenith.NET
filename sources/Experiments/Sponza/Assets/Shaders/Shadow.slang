#include "Material.slang"

struct ShadowConstants
{
    float4x4 World;

    float4x4 ViewProjection;

    DescriptorHandle<StructuredBuffer<MaterialConstants>> Materials;

    DescriptorHandle<SamplerState> MaterialSampler;

    uint MaterialIndex;

    uint Padding0;

    uint Padding1;

    uint Padding2;
};

struct ShadowVertexInput
{
    float3 Position : POSITION0;

    float3 Normal : NORMAL0;

    float4 Tangent : TANGENT0;

    float2 TexCoord : TEXCOORD0;
};

struct ShadowFragmentInput
{
    float4 Position : SV_POSITION;

    float2 TexCoord : TEXCOORD0;
};

ConstantBuffer<ShadowConstants> shadow;

[shader("vertex")]
ShadowFragmentInput VSMain(ShadowVertexInput input)
{
    ShadowFragmentInput output;
    output.Position = mul(mul(float4(input.Position, 1.0), shadow.World), shadow.ViewProjection);
    output.TexCoord = input.TexCoord;

    return output;
}

[shader("fragment")]
void OpaqueFS(ShadowFragmentInput input)
{
}

[shader("fragment")]
void MaskFS(ShadowFragmentInput input)
{
    MaterialConstants material = shadow.Materials[shadow.MaterialIndex];
    float4 baseColor = SampleBaseColor(material, input.TexCoord, shadow.MaterialSampler);
    ClipMaterial(material, baseColor.a);
}
