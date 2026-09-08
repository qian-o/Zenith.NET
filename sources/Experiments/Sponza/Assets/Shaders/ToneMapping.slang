struct Constants
{
    DescriptorHandle<Texture2D<float4>> HdrColor;

    DescriptorHandle<SamplerState> Sampler;

    float Exposure;

    float Padding0;

    float Padding1;

    float Padding2;
};

struct VertexOutput
{
    float4 Position : SV_POSITION;

    float2 UV : TEXCOORD0;
};

ConstantBuffer<Constants> constants;

[shader("vertex")]
VertexOutput VSMain(uint vertexId : SV_VertexID)
{
    VertexOutput output;
    output.UV = float2((vertexId << 1) & 2, vertexId & 2);
    output.Position = float4(output.UV * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    return output;
}

[shader("fragment")]
float4 FSMain(VertexOutput input) : SV_TARGET
{
    float3 hdr = max(constants.HdrColor.SampleLevel(constants.Sampler, input.UV, 0.0).rgb * constants.Exposure, 0.0);
    float3 mapped = saturate((hdr * (2.51 * hdr + 0.03)) / (hdr * (2.43 * hdr + 0.59) + 0.14));
    float3 srgb = select(mapped <= 0.0031308, mapped * 12.92, 1.055 * pow(mapped, 1.0 / 2.4) - 0.055);
    return float4(srgb, 1.0);
}
