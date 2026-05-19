struct PassConstants
{
    DescriptorHandle<Texture2D<float4>> AlbedoTexture;
    DescriptorHandle<SamplerState> LinearSampler;
    float4 Tint;
};

struct FullscreenVertex
{
    float4 Position : SV_POSITION;
    float2 Uv : TEXCOORD0;
};

[[vk::binding(0, 0)]]
ConstantBuffer<PassConstants> gPass;

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
    return (*gPass.AlbedoTexture).Sample(*gPass.LinearSampler, input.Uv) * gPass.Tint;
}