static const float EPSILON = 0.0001;

struct BlurConstants
{
    float2 TexelSize;
};

ConstantBuffer<BlurConstants> constants;
Texture2D<float> inputTexture;
Texture2D positionTexture;
RWTexture2D<float> outputTexture;
SamplerState linearSampler;

static const int KERNEL_SIZE = 9;
static const float KERNEL_WEIGHTS[9] = { 0.0093, 0.028, 0.0659, 0.1226, 0.1802, 0.1226, 0.0659, 0.028, 0.0093 };
static const float DEPTH_THRESHOLD = 2.0;

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

    float3 centerPos = positionTexture.SampleLevel(linearSampler, uv, 0).xyz;
    float centerDepth = length(centerPos);

    float result = 0.0;
    float totalWeight = 0.0;

    int halfSize = KERNEL_SIZE / 2;

    for (int i = -halfSize; i <= halfSize; i++)
    {
        float2 offset = constants.TexelSize * float(i);
        float2 sampleUV = uv + offset;

        float3 samplePos = positionTexture.SampleLevel(linearSampler, sampleUV, 0).xyz;
        float sampleDepth = length(samplePos);

        float depthDiff = abs(centerDepth - sampleDepth);
        float depthWeight = exp(-depthDiff * depthDiff / (DEPTH_THRESHOLD * DEPTH_THRESHOLD));

        float kernelWeight = KERNEL_WEIGHTS[i + halfSize];
        float weight = kernelWeight * depthWeight;

        result += inputTexture.SampleLevel(linearSampler, sampleUV, 0) * weight;
        totalWeight += weight;
    }

    outputTexture[dispatchThreadID.xy] = totalWeight > EPSILON ? result / totalWeight : 0.0;
}