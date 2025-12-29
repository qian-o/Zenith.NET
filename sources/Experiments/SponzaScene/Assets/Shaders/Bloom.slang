struct BloomConstants
{
    float2 TexelSize;
};

ConstantBuffer<BloomConstants> constants;
Texture2D<float4> sourceTexture;
RWTexture2D<float4> outputTexture;
SamplerState linearSampler;

static const float weights[5] = { 0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216 };

[numthreads(16, 16, 1)]
void CSMain(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint width, height;
    outputTexture.GetDimensions(width, height);

    if (dispatchThreadID.x >= width || dispatchThreadID.y >= height)
    {
        return;
    }

    float2 uv = (float2(dispatchThreadID.xy) + 0.5) / float2(width, height);

    float3 result = sourceTexture.SampleLevel(linearSampler, uv, 0).rgb * weights[0];

    for (int i = 1; i < 5; i++)
    {
        result += sourceTexture.SampleLevel(linearSampler, uv + constants.TexelSize * i, 0).rgb * weights[i];
        result += sourceTexture.SampleLevel(linearSampler, uv - constants.TexelSize * i, 0).rgb * weights[i];
    }

    outputTexture[dispatchThreadID.xy] = float4(result, 1.0);
}
