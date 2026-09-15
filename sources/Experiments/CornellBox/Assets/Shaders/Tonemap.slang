struct Constants
{
    float4 OutputSizeFrame;

    DescriptorHandle<Texture2D<float4>> Input;

    DescriptorHandle<RWTexture2D<float4>> Output;

    DescriptorHandle<SamplerState> Sampler;

    uint2 Padding;
};

ConstantBuffer<Constants> constants;

uint Hash(uint value)
{
    uint state = value * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;

    return (word >> 22u) ^ word;
}

[shader("compute")]
[numthreads(8, 8, 1)]
void CSMain(uint3 dispatchThreadId: SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadId.xy;
    uint2 outputSize = uint2(constants.OutputSizeFrame.xy);

    if (any(pixel >= outputSize))
    {
        return;
    }

    float2 uv = (float2(pixel) + 0.5) / constants.OutputSizeFrame.xy;
    float3 color = max(constants.Input.SampleLevel(constants.Sampler, uv, 0.0).rgb, 0.0);
    float3 numerator = color * (color * 2.51 + 0.03);
    float3 denominator = color * (color * 2.43 + 0.59) + 0.14;
    color = pow(saturate(numerator / denominator), 1.0 / 2.2);

    uint seed = Hash(pixel.x + pixel.y * outputSize.x + asuint(constants.OutputSizeFrame.z) * 1664525u);
    float first = float(seed >> 8u) * (1.0 / 16777216.0);
    float second = float(Hash(seed) >> 8u) * (1.0 / 16777216.0);
    color += (first - second) / 255.0;

    constants.Output[pixel] = float4(saturate(color), 1.0);
}
