import "Common/Fullscreen";

struct BlurConstants
{
    float2 TexelSize;

    int BlurSize;
};

ConstantBuffer<BlurConstants> constants;
Texture2D ssaoTexture;
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

    float result = 0.0;
    int sampleCount = 0;

    for (int x = -constants.BlurSize; x < constants.BlurSize; ++x)
    {
        for (int y = -constants.BlurSize; y < constants.BlurSize; ++y)
        {
            float2 offset = float2(float(x), float(y)) * constants.TexelSize;
            result += ssaoTexture.Sample(pointSampler, uv + offset).r;

            sampleCount++;
        }
    }

    result /= float(sampleCount);

    outputTexture[dispatchThreadID.xy] = result;
}
