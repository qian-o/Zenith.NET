struct ComposeConstants
{
    float AOStrength;

    float BloomIntensity;
};

ConstantBuffer<ComposeConstants> constants;
Texture2D litColorTexture;
Texture2D ssaoTexture;
Texture2D bloomTexture;
RWTexture2D outputTexture;
SamplerState pointSampler;

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

    float3 color = litColorTexture.Sample(pointSampler, uv).rgb;

    color *= lerp(1.0, ssaoTexture.Sample(pointSampler, uv).r, constants.AOStrength);

    color += bloomTexture.Sample(pointSampler, uv).rgb * constants.BloomIntensity;

    outputTexture[dispatchThreadID.xy] = float4(color, 1.0);
}