static const float EPSILON = 0.0001;
static const float MIN_VARIANCE = 0.0001;

struct TemporalConstants
{
    float4x4 PrevViewProjection;

    float2 ViewportSize;

    float ColorBoxSigma;

    float NormalThreshold;

    float DepthThreshold;

    int MaxHistoryLength;
};

ConstantBuffer<TemporalConstants> constants;
Texture2D currentColorTexture;
Texture2D positionTexture;
Texture2D normalTexture;
Texture2D historyPositionTexture;
Texture2D historyNormalTexture;
RWTexture2D<float4> accumulatedTexture;
RWTexture2D<float4> momentsTexture;
RWTexture2D<float4> outputTexture;
SamplerState linearSampler;

float3 SafeNormalize(float3 v)
{
    float len = dot(v, v);

    return len > EPSILON ? v * rsqrt(len) : float3(0.0, 0.0, 1.0);
}

float Luminance(float3 color)
{
    return dot(color, float3(0.2126, 0.7152, 0.0722));
}

bool ReprojectAndValidate(float3 worldPos, float3 worldNormal, out float2 historyUV)
{
    float4 clipPos = mul(float4(worldPos, 1.0), constants.PrevViewProjection);

    if (clipPos.w <= 0.0)
    {
        historyUV = float2(-1.0);

        return false;
    }

    clipPos.xyz /= clipPos.w;
    historyUV = float2(clipPos.x * 0.5 + 0.5, -clipPos.y * 0.5 + 0.5);

    if (any(historyUV < 0.0) || any(historyUV > 1.0) || clipPos.z <= 0.0 || clipPos.z >= 1.0)
    {
        return false;
    }

    float3 historyPos = historyPositionTexture.SampleLevel(linearSampler, historyUV, 0).xyz;
    float3 historyNormal = historyNormalTexture.SampleLevel(linearSampler, historyUV, 0).xyz;

    if (dot(historyPos, historyPos) < EPSILON)
    {
        return false;
    }

    float3 normal = SafeNormalize(worldNormal);
    float planeDist = abs(dot(worldPos - historyPos, normal));

    if (planeDist > length(worldPos) * constants.DepthThreshold)
    {
        return false;
    }

    if (dot(normal, SafeNormalize(historyNormal)) < constants.NormalThreshold)
    {
        return false;
    }

    return true;
}

void ComputeColorBounds(uint2 pixelCoord, out float3 minColor, out float3 maxColor, out float spatialVariance)
{
    float3 m1 = float3(0.0);
    float3 m2 = float3(0.0);
    int count = 0;

    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            int2 coord = int2(pixelCoord) + int2(x, y);

            if (all(coord >= 0) && all(coord < int2(constants.ViewportSize)))
            {
                float3 c = currentColorTexture.Load(int3(coord, 0)).rgb;

                m1 += c;
                m2 += c * c;
                count++;
            }
        }
    }

    float invCount = 1.0 / float(count);
    float3 mean = m1 * invCount;
    float3 variance = max(m2 * invCount - mean * mean, 0.0);
    float3 stdDev = sqrt(variance) * constants.ColorBoxSigma;

    minColor = mean - stdDev;
    maxColor = mean + stdDev;
    spatialVariance = Luminance(variance);
}

[numthreads(16, 16, 1)]
void CSMain(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint2 pixelCoord = dispatchThreadID.xy;

    if (any(pixelCoord >= uint2(constants.ViewportSize)))
    {
        return;
    }

    float3 worldPos = positionTexture.Load(int3(pixelCoord, 0)).xyz;

    if (dot(worldPos, worldPos) < EPSILON)
    {
        accumulatedTexture[pixelCoord] = float4(0.0);
        momentsTexture[pixelCoord] = float4(0.0);
        outputTexture[pixelCoord] = float4(0.0);

        return;
    }

    float3 worldNormal = normalTexture.Load(int3(pixelCoord, 0)).xyz;
    float3 currentColor = currentColorTexture.Load(int3(pixelCoord, 0)).rgb;

    float3 minColor, maxColor;
    float spatialVariance;
    ComputeColorBounds(pixelCoord, minColor, maxColor, spatialVariance);

    float2 historyUV;
    bool valid = ReprojectAndValidate(worldPos, worldNormal, historyUV);

    float3 accumulatedColor;
    float2 moments;
    float historyLength;

    if (valid)
    {
        int2 historyCoord = int2(historyUV * constants.ViewportSize);
        float3 historyColor = clamp(accumulatedTexture[historyCoord].rgb, minColor, maxColor);
        float4 historyMoments = momentsTexture[historyCoord];

        historyLength = min(historyMoments.w + 1.0, float(constants.MaxHistoryLength));

        float alpha = 1.0 / historyLength;
        accumulatedColor = lerp(historyColor, currentColor, alpha);

        float lum = Luminance(currentColor);
        moments.x = lerp(historyMoments.x, lum, alpha);
        moments.y = lerp(historyMoments.y, lum * lum, alpha);
    }
    else
    {
        accumulatedColor = currentColor;
        historyLength = 1.0;

        float lum = Luminance(currentColor);
        moments = float2(lum, lum * lum);
    }

    float temporalVariance = max(moments.y - moments.x * moments.x, 0.0);
    float t = saturate(historyLength / 4.0);
    float variance = max(lerp(spatialVariance, temporalVariance, t), MIN_VARIANCE);

    accumulatedTexture[pixelCoord] = float4(accumulatedColor, 1.0);
    momentsTexture[pixelCoord] = float4(moments, 0.0, historyLength);
    outputTexture[pixelCoord] = float4(accumulatedColor, variance);
}
