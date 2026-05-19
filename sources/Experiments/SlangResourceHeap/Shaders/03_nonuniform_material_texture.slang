struct MaterialRecord
{
    DescriptorHandle<Texture2D<float4>> BaseColorTexture;
    float4 Tint;
};

struct MaterialConstants
{
    DescriptorHandle<StructuredBuffer<MaterialRecord>> Materials;
    DescriptorHandle<SamplerState> LinearSampler;
    uint MaterialCount;
    float UvScale;
};

struct FullscreenVertex
{
    float4 Position : SV_POSITION;
    float2 Uv : TEXCOORD0;
};

[[vk::binding(0, 0)]]
ConstantBuffer<MaterialConstants> gMaterials : register(b0);

[shader("vertex")]
FullscreenVertex vertexMain(uint vertexId : SV_VertexID)
{
    float2 position = float2(vertexId == 2 ? 3.0 : -1.0, vertexId == 1 ? 3.0 : -1.0);

    FullscreenVertex output;
    output.Position = float4(position, 0.0, 1.0);
    output.Uv = position * 0.5 + 0.5;

    return output;
}

[shader("fragment")]
float4 fragmentMain(FullscreenVertex input) : SV_TARGET
{
    uint count = max(gMaterials.MaterialCount, 1u);
    uint materialIndex = min(uint(input.Uv.x * float(count)), count - 1u);
    MaterialRecord material = gMaterials.Materials[materialIndex];
    SamplerState linearSampler = gMaterials.LinearSampler;

    return nonuniform(material.BaseColorTexture)->Sample(linearSampler, input.Uv * gMaterials.UvScale) * material.Tint;
}