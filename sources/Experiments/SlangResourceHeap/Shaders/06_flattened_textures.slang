struct FlattenedTextureConstants
{
    DescriptorHandle<Texture2D<float4>> Texture0;
    DescriptorHandle<Texture2D<float4>> Texture1;
    DescriptorHandle<Texture2D<float4>> Texture2;
    DescriptorHandle<Texture2D<float4>> Texture3;
    DescriptorHandle<SamplerState> LinearSampler;
    uint TextureIndex;
    float UvScale;
};

struct FullscreenVertex
{
    float4 Position : SV_POSITION;
    float2 Uv : TEXCOORD0;
};

[[vk::binding(0, 0)]]
ConstantBuffer<FlattenedTextureConstants> gTextures : register(b0);

[shader("vertex")]
FullscreenVertex vertexMain(uint vertexId : SV_VertexID)
{
    float2 position = float2(vertexId == 2 ? 3.0 : -1.0, vertexId == 1 ? 3.0 : -1.0);

    FullscreenVertex output;
    output.Position = float4(position, 0.0, 1.0);
    output.Uv = position * 0.5 + 0.5;

    return output;
}

DescriptorHandle<Texture2D<float4>> selectTexture(uint textureIndex)
{
    switch (min(textureIndex, 3u))
    {
    case 0u:
        return gTextures.Texture0;
    case 1u:
        return gTextures.Texture1;
    case 2u:
        return gTextures.Texture2;
    default:
        return gTextures.Texture3;
    }
}

[shader("fragment")]
float4 fragmentMain(FullscreenVertex input) : SV_TARGET
{
    DescriptorHandle<Texture2D<float4>> textureHandle = selectTexture(gTextures.TextureIndex);

    return nonuniform(textureHandle)->Sample(gTextures.LinearSampler, input.Uv * gTextures.UvScale);
}