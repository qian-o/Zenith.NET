struct OutputConstants
{
    float2 TexelSize;

    float Exposure;

    uint EncodeLuminance;

    DescriptorHandle<Texture2D> Input;

    DescriptorHandle<SamplerState> Sampler;
};

struct FullscreenOutput
{
    float4 Position : SV_POSITION;

    float2 UV : TEXCOORD0;
};

ConstantBuffer<OutputConstants> output;

[shader("vertex")]
FullscreenOutput FullscreenVS(uint vertexId : SV_VertexID)
{
    float2 uv = float2((vertexId << 1) & 2, vertexId & 2);

    FullscreenOutput result;
    result.Position = float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    result.UV = uv;

    return result;
}

float3 DisplayColor(float3 color)
{
    float3 hdr = max(color * output.Exposure, 0.0);
    float3 mapped = saturate(hdr * (2.51 * hdr + 0.03) / (hdr * (2.43 * hdr + 0.59) + 0.14));

    return select(mapped <= 0.0031308, mapped * 12.92, 1.055 * pow(mapped, 1.0 / 2.4) - 0.055);
}

float Luminance(float3 color)
{
    return dot(color, float3(0.299, 0.587, 0.114));
}

[shader("fragment")]
float4 ToneMappingFS(FullscreenOutput input) : SV_TARGET
{
    float3 color = DisplayColor(output.Input.SampleLevel(output.Sampler, input.UV, 0.0).rgb);

    return float4(color, output.EncodeLuminance != 0 ? Luminance(color) : 1.0);
}

[shader("fragment")]
float4 AntialiasingFS(FullscreenOutput input) : SV_TARGET
{
    float2 texel = output.TexelSize;
    float4 center = output.Input.SampleLevel(output.Sampler, input.UV, 0.0);
    float northwest = output.Input.SampleLevel(output.Sampler, input.UV + texel * float2(-1.0, -1.0), 0.0).a;
    float northeast = output.Input.SampleLevel(output.Sampler, input.UV + texel * float2(1.0, -1.0), 0.0).a;
    float southwest = output.Input.SampleLevel(output.Sampler, input.UV + texel * float2(-1.0, 1.0), 0.0).a;
    float southeast = output.Input.SampleLevel(output.Sampler, input.UV + texel * float2(1.0, 1.0), 0.0).a;
    float minimum = min(center.a, min(min(northwest, northeast), min(southwest, southeast)));
    float maximum = max(center.a, max(max(northwest, northeast), max(southwest, southeast)));

    if (maximum - minimum < max(0.0312, maximum * 0.125))
    {
        return float4(center.rgb, 1.0);
    }

    float2 direction = float2(-(northwest + northeast - southwest - southeast), northwest + southwest - northeast - southeast);
    float reduction = max((northwest + northeast + southwest + southeast) * 0.03125, 0.0078125);
    direction = clamp(direction / (min(abs(direction.x), abs(direction.y)) + reduction), -8.0, 8.0) * texel;

    float3 inner = (output.Input.SampleLevel(output.Sampler, input.UV - direction / 6.0, 0.0).rgb + output.Input.SampleLevel(output.Sampler, input.UV + direction / 6.0, 0.0).rgb) * 0.5;
    float3 outer = inner * 0.5 + (output.Input.SampleLevel(output.Sampler, input.UV - direction * 0.5, 0.0).rgb + output.Input.SampleLevel(output.Sampler, input.UV + direction * 0.5, 0.0).rgb) * 0.25;
    float luminance = Luminance(outer);

    return float4(luminance < minimum || luminance > maximum ? inner : outer, 1.0);
}
