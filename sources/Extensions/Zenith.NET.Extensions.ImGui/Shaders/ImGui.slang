struct VSInput
{
    float2 Position : POSITION0;

    float2 UV : TEXCOORD0;

    float4 Color : COLOR0;
};

struct VSOutput
{
    float4 Position : SV_POSITION;

    float2 UV : TEXCOORD0;

    float4 Color : COLOR0;
};

struct Constants
{
    float4x4 Projection;

    DescriptorHandle<Texture2D> Texture;

    DescriptorHandle<SamplerState> Sampler;
};

ConstantBuffer<Constants> constants;

float3 SrgbToLinear(float3 srgb)
{
    return srgb * (srgb * (srgb * 0.305306011 + 0.682171111) + 0.012522878);
}

[shader("vertex")]
VSOutput VSMain(VSInput input)
{
    VSOutput output;

    output.Position = mul(float4(input.Position, 0.0, 1.0), constants.Projection);
    output.UV = input.UV;
    output.Color = input.Color;

#if 0
    output.Color.rgb = SrgbToLinear(output.Color.rgb);
#endif

    return output;
}

[shader("fragment")]
float4 FSMain(VSOutput input) : SV_TARGET
{
    return input.Color * constants.Texture.Sample(constants.Sampler, input.UV);
}
