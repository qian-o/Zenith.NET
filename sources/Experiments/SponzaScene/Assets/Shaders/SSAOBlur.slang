import "Common/Fullscreen";

struct BlurConstants
{
    float2 TexelSize;
    int BlurSize;
    float _Padding;
};

ConstantBuffer<BlurConstants> constants;
Texture2D ssaoTexture;
SamplerState pointSampler;

float4 PSMain(PSInput input) : SV_Target0
{
    float2 uv = input.TexCoord;

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

    return float4(result, result, result, 1.0);
}