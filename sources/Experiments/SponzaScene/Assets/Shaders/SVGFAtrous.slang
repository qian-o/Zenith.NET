static const float EPSILON = 0.0001;
static const float MIN_VARIANCE = 0.0001;

static const float KERNEL[3] = { 1.0, 2.0 / 3.0, 1.0 / 6.0 };

struct AtrousConstants
{
    float2 ViewportSize;

    int StepWidth;

    float PhiColor;

    float PhiNormal;

    float PhiDepth;
};

ConstantBuffer<AtrousConstants> constants;
Texture2D inputTexture;
Texture2D positionTexture;
Texture2D normalTexture;
RWTexture2D<float4> outputTexture;
SamplerState pointSampler;

float3 SafeNormalize(float3 v)
{
    float len = dot(v, v);

    return len > EPSILON ? v * rsqrt(len) : float3(0.0, 0.0, 1.0);
}

float Luminance(float3 color)
{
    return dot(color, float3(0.2126, 0.7152, 0.0722));
}

[numthreads(16, 16, 1)]
void CSMain(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint2 pixelCoord = dispatchThreadID.xy;

    if (any(pixelCoord >= uint2(constants.ViewportSize)))
    {
        return;
    }

    float4 centerData = inputTexture.Load(int3(pixelCoord, 0));
    float3 centerPos = positionTexture.Load(int3(pixelCoord, 0)).xyz;

    if (dot(centerPos, centerPos) < EPSILON)
    {
        outputTexture[pixelCoord] = float4(0.0);

        return;
    }

    float3 centerColor = centerData.rgb;
    float variance = max(centerData.a, MIN_VARIANCE);
    float3 centerNormal = SafeNormalize(normalTexture.Load(int3(pixelCoord, 0)).xyz);
    float centerLum = Luminance(centerColor);
    float centerDepth = length(centerPos);

    float phiColor = constants.PhiColor * sqrt(variance) + EPSILON;

    float3 colorSum = centerColor;
    float varianceSum = variance;
    float weightSum = 1.0;

    [unroll]
    for (int dy = -2; dy <= 2; dy++)
    {
        [unroll]
        for (int dx = -2; dx <= 2; dx++)
        {
            if (dx == 0 && dy == 0)
            {
                continue;
            }

            int2 sampleCoord = int2(pixelCoord) + int2(dx, dy) * constants.StepWidth;

            if (any(sampleCoord < 0) || any(sampleCoord >= int2(constants.ViewportSize)))
            {
                continue;
            }

            float4 sampleData = inputTexture.Load(int3(sampleCoord, 0));
            float3 samplePos = positionTexture.Load(int3(sampleCoord, 0)).xyz;

            if (dot(samplePos, samplePos) < EPSILON)
            {
                continue;
            }

            float3 sampleColor = sampleData.rgb;
            float sampleVariance = max(sampleData.a, MIN_VARIANCE);
            float3 sampleNormal = SafeNormalize(normalTexture.Load(int3(sampleCoord, 0)).xyz);

            float kernelWeight = KERNEL[abs(dx)] * KERNEL[abs(dy)];

            float lumDiff = abs(Luminance(sampleColor) - centerLum);
            float colorWeight = exp(-lumDiff / phiColor);

            float normalWeight = pow(max(dot(centerNormal, sampleNormal), 0.0), constants.PhiNormal);

            float planeDist = abs(dot(samplePos - centerPos, centerNormal));
            float depthWeight = exp(-planeDist / (centerDepth * constants.PhiDepth + EPSILON));

            float weight = kernelWeight * colorWeight * normalWeight * depthWeight;

            colorSum += sampleColor * weight;
            varianceSum += sampleVariance * weight * weight;
            weightSum += weight;
        }
    }

    float3 filteredColor = colorSum / weightSum;
    float filteredVariance = varianceSum / (weightSum * weightSum);

    outputTexture[pixelCoord] = float4(filteredColor, filteredVariance);
}
