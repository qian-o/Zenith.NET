static const float PI = 3.14159265359;
static const float EPSILON = 0.0001;

static const float MAX_VIEW_DISTANCE = 500.0;
static const float MIN_VIEW_DISTANCE = 0.1;
static const float MIN_SAMPLE_DISTANCE = 0.001;
static const float MIN_SCREEN_RADIUS = 2.0;
static const float MAX_SCREEN_RADIUS = 100.0;

static const float GOLDEN_RATIO = 0.381966;

static const float CURVATURE_NORMAL_THRESHOLD = 0.4;
static const float CURVATURE_NDOTS_MIN = 0.2;
static const float CURVATURE_NDOTS_MAX = 0.7;
static const float CURVATURE_ATTENUATION = 0.8;

struct GTAOConstants
{
    float4x4 View;

    float4x4 Projection;

    float ViewportWidth;

    float ViewportHeight;

    float EffectRadius;

    float EffectFalloffRange;

    float RadiusMultiplier;

    float FinalValuePower;

    float SampleDistributionPower;

    int SliceCount;

    int StepsPerSlice;

    int _pad0;

    int _pad1;

    int _pad2;
};

ConstantBuffer<GTAOConstants> constants;
Texture2D positionTexture;
Texture2D normalTexture;
RWTexture2D outputTexture;
SamplerState pointSampler;

// ============================================
// Utility
// ============================================

float3 SafeNormalize(float3 v)
{
    float len = dot(v, v);

    return len > EPSILON ? v * rsqrt(len) : float3(0.0, 0.0, -1.0);
}

float InterleavedGradientNoise(float2 pos)
{
    const float3 magic = float3(0.06711056, 0.00583715, 52.9829189);

    return frac(magic.z * frac(dot(pos, magic.xy)));
}

// ============================================
// Sampling
// ============================================

float3 GetViewPosition(float2 uv)
{
    float4 worldPos = positionTexture.SampleLevel(pointSampler, uv, 0);

    if (worldPos.w < EPSILON)
    {
        return float3(0.0, 0.0, 1e10);
    }

    return mul(float4(worldPos.xyz, 1.0), constants.View).xyz;
}

float3 GetViewNormal(float2 uv)
{
    float3 worldNormal = normalTexture.SampleLevel(pointSampler, uv, 0).xyz;

    if (dot(worldNormal, worldNormal) < 0.5)
    {
        return float3(0.0, 0.0, -1.0);
    }

    return SafeNormalize(mul(float4(SafeNormalize(worldNormal), 0.0), constants.View).xyz);
}

void SampleViewData(float2 uv, out float3 position, out float3 normal)
{
    float4 worldPos = positionTexture.SampleLevel(pointSampler, uv, 0);

    if (worldPos.w < EPSILON)
    {
        position = float3(0.0, 0.0, 1e10);
        normal = float3(0.0, 0.0, -1.0);

        return;
    }

    position = mul(float4(worldPos.xyz, 1.0), constants.View).xyz;

    float3 worldNormal = normalTexture.SampleLevel(pointSampler, uv, 0).xyz;
    normal = SafeNormalize(mul(float4(SafeNormalize(worldNormal), 0.0), constants.View).xyz);
}

// ============================================
// Occlusion Calculation
// ============================================

float ApplyCurvatureCompensation(float NdotS, float normalDot)
{
    if (normalDot <= CURVATURE_NORMAL_THRESHOLD)
    {
        return NdotS;
    }

    float normalFactor = (normalDot - CURVATURE_NORMAL_THRESHOLD) / (1.0 - CURVATURE_NORMAL_THRESHOLD);
    normalFactor = normalFactor * normalFactor;

    float threshold = lerp(CURVATURE_NDOTS_MIN, CURVATURE_NDOTS_MAX, normalFactor);

    if (NdotS < threshold)
    {
        return 0.0;
    }

    float remapped = (NdotS - threshold) / (1.0 - threshold);

    return remapped * (1.0 - normalFactor * CURVATURE_ATTENUATION);
}

float CalculateSampleOcclusion(float3 centerPos, float3 centerNormal, float2 sampleUV, float worldRadius, float falloffPower)
{
    float3 samplePos, sampleNormal;
    SampleViewData(sampleUV, samplePos, sampleNormal);

    if (abs(samplePos.z) >= MAX_VIEW_DISTANCE)
    {
        return 0.0;
    }

    float3 toSample = samplePos - centerPos;
    float dist = length(toSample);

    if (dist < MIN_SAMPLE_DISTANCE || dist >= worldRadius)
    {
        return 0.0;
    }

    float3 sampleDir = toSample / dist;
    float NdotS = dot(centerNormal, sampleDir);

    if (NdotS <= 0.0)
    {
        return 0.0;
    }

    float normalDot = dot(centerNormal, sampleNormal);
    NdotS = ApplyCurvatureCompensation(NdotS, normalDot);

    float distRatio = dist / worldRadius;
    float falloff = saturate(1.0 - distRatio * distRatio);
    float distanceFalloff = pow(falloff, falloffPower);

    return NdotS * distanceFalloff;
}

[numthreads(16, 16, 1)]
void CSMain(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint2 pixelCoord = dispatchThreadID.xy;

    uint width, height;
    outputTexture.GetDimensions(width, height);

    if (pixelCoord.x >= width || pixelCoord.y >= height)
    {
        return;
    }

    float2 uv = (float2(pixelCoord) + 0.5) / float2(width, height);

    float3 viewPos = GetViewPosition(uv);
    float viewDepth = abs(viewPos.z);

    if (viewDepth > MAX_VIEW_DISTANCE || viewDepth < MIN_VIEW_DISTANCE)
    {
        outputTexture[pixelCoord] = 1.0;

        return;
    }

    float3 viewNormal = GetViewNormal(uv);

    float noise1 = InterleavedGradientNoise(float2(pixelCoord));
    float noise2 = InterleavedGradientNoise(float2(pixelCoord) + float2(5.588238, 5.588238));

    float worldRadius = constants.EffectRadius * constants.RadiusMultiplier;
    float projScale = abs(constants.Projection[1][1]);
    float screenRadius = (worldRadius * projScale * constants.ViewportHeight * 0.5) / viewDepth;
    screenRadius = clamp(screenRadius, MIN_SCREEN_RADIUS, MAX_SCREEN_RADIUS);

    int sliceCount = constants.SliceCount;
    int stepsPerSlice = constants.StepsPerSlice;
    float invStepsPerSlice = 1.0 / float(stepsPerSlice);

    float totalOcclusion = 0.0;
    float sampleCount = 0.0;

    float angleStep = PI / float(sliceCount);
    float startAngle = noise1 * PI;

    float2 invViewportSize = 1.0 / float2(constants.ViewportWidth, constants.ViewportHeight);

    for (int slice = 0; slice < sliceCount; slice++)
    {
        float angle = startAngle + angleStep * (float(slice) + 0.5);
        float2 direction;
        sincos(angle, direction.y, direction.x);

        float2 scaledDirection = direction * invViewportSize;

        for (int step = 1; step <= stepsPerSlice; step++)
        {
            float jitter = frac(noise2 + float(step) * GOLDEN_RATIO);
            float t = (float(step) - 0.5 + jitter * 0.5) * invStepsPerSlice;

            float sampleRadius = max(pow(t, constants.SampleDistributionPower) * screenRadius, 1.0);
            float2 offset = scaledDirection * sampleRadius;

            float2 forwardUV = uv + offset;
            float2 backwardUV = uv - offset;

            if (all(forwardUV > 0.0) && all(forwardUV < 1.0))
            {
                totalOcclusion += CalculateSampleOcclusion(viewPos, viewNormal, forwardUV, worldRadius, constants.EffectFalloffRange);
                sampleCount += 1.0;
            }

            if (all(backwardUV > 0.0) && all(backwardUV < 1.0))
            {
                totalOcclusion += CalculateSampleOcclusion(viewPos, viewNormal, backwardUV, worldRadius, constants.EffectFalloffRange);
                sampleCount += 1.0;
            }
        }
    }

    float ao = 1.0;

    if (sampleCount > 0.0)
    {
        ao = 1.0 - saturate(totalOcclusion / sampleCount);
    }

    ao = pow(saturate(ao), constants.FinalValuePower);

    outputTexture[pixelCoord] = ao;
}
