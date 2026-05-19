struct TextureArrayConstants
{
    DescriptorHandle<StructuredBuffer<DescriptorHandle<Texture2D<float4>>>> Textures;
    DescriptorHandle<SamplerState> LinearSampler;
    uint TextureCount;
    float UvScale;
};

struct FullscreenVertex
{
    float4 Position : SV_POSITION;
    float2 Uv : TEXCOORD0;
};

[[vk::binding(0, 0)]]
ConstantBuffer<TextureArrayConstants> gTextures : register(b0);

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
    uint count = max(gTextures.TextureCount, 1u);
    uint textureIndex = min(uint(input.Uv.x * float(count)), count - 1u);
    DescriptorHandle<Texture2D<float4>> textureHandle = gTextures.Textures[textureIndex];
    SamplerState linearSampler = gTextures.LinearSampler;

    return nonuniform(textureHandle)->Sample(linearSampler, input.Uv * gTextures.UvScale);
}