static const float EPSILON = 0.0001;
static const float DEPTH_FALLOFF = 0.25;

static const int KERNEL_RADIUS = 4;
static const float KERNEL_WEIGHTS[9] = { 0.0093, 0.028, 0.0659, 0.1226, 0.1802, 0.1226, 0.0659, 0.028, 0.0093 };

struct BlurConstants
{
    float2 TexelSize;

    private float2 padding0;
};

ConstantBuffer<BlurConstants> constants;
Texture2D inputTexture;
Texture2D positionTexture;
RWTexture2D outputTexture;
SamplerState linearSampler;

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
    float centerDepth = dot(centerPos, centerPos);

    float centerValue = inputTexture.SampleLevel(linearSampler, uv, 0).r;
    float centerWeight = KERNEL_WEIGHTS[KERNEL_RADIUS];

    float result = centerValue * centerWeight;
    float totalWeight = centerWeight;

    [unroll]
    for (int i = 1; i <= KERNEL_RADIUS; i++)
    {
        float2 offset = constants.TexelSize * float(i);
        float kernelWeight = KERNEL_WEIGHTS[KERNEL_RADIUS + i];

        float2 uvPos = uv + offset;
        float2 uvNeg = uv - offset;

        float3 posPos = positionTexture.SampleLevel(linearSampler, uvPos, 0).xyz;
        float3 posNeg = positionTexture.SampleLevel(linearSampler, uvNeg, 0).xyz;

        float depthPos = dot(posPos, posPos);
        float depthNeg = dot(posNeg, posNeg);

        float diffPos = centerDepth - depthPos;
        float diffNeg = centerDepth - depthNeg;

        float weightPos = kernelWeight * exp(-diffPos * diffPos * DEPTH_FALLOFF);
        float weightNeg = kernelWeight * exp(-diffNeg * diffNeg * DEPTH_FALLOFF);

        result += inputTexture.SampleLevel(linearSampler, uvPos, 0).r * weightPos;
        result += inputTexture.SampleLevel(linearSampler, uvNeg, 0).r * weightNeg;

        totalWeight += weightPos + weightNeg;
    }

    outputTexture[dispatchThreadID.xy] = result / max(totalWeight, EPSILON);
}
