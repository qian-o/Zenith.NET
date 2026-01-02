static const float EPSILON = 0.0001;
static const float PI = 3.14159265359;

struct SSGIBlurConstants
{
    float2 TexelSize;
    int BlurSize;
    float NormalSigma;
    float DepthSigma;
};

ConstantBuffer<SSGIBlurConstants> constants;
Texture2D ssgiTexture;
Texture2D positionTexture;
Texture2D normalTexture;
RWTexture2D<float4> outputTexture;
SamplerState pointSampler;

float3 SafeNormalize(float3 v)
{
    float len = dot(v, v);
    return len > EPSILON ? v * rsqrt(len) : float3(0.0, 1.0, 0.0);
}

// Extended Poisson disk with 32 samples for better coverage
static const float2 poissonDisk[32] = {
    float2(-0.94201624, -0.39906216),
    float2(0.94558609, -0.76890725),
    float2(-0.094184101, -0.92938870),
    float2(0.34495938, 0.29387760),
    float2(-0.91588581, 0.45771432),
    float2(-0.81544232, -0.87912464),
    float2(-0.38277543, 0.27676845),
    float2(0.97484398, 0.75648379),
    float2(0.44323325, -0.97511554),
    float2(0.53742981, -0.47373420),
    float2(-0.26496911, -0.41893023),
    float2(0.79197514, 0.19090188),
    float2(-0.24188840, 0.99706507),
    float2(-0.81409955, 0.91437590),
    float2(0.19984126, 0.78641367),
    float2(0.14383161, -0.14100790),
    float2(-0.55555555, 0.66666666),
    float2(0.66666666, 0.55555555),
    float2(-0.33333333, -0.77777777),
    float2(0.77777777, -0.33333333),
    float2(-0.88888888, -0.11111111),
    float2(0.11111111, 0.88888888),
    float2(-0.44444444, 0.44444444),
    float2(0.44444444, -0.88888888),
    float2(-0.72222222, 0.16666666),
    float2(0.16666666, -0.61111111),
    float2(-0.05555555, 0.55555555),
    float2(0.55555555, 0.05555555),
    float2(-0.61111111, -0.55555555),
    float2(0.88888888, 0.44444444),
    float2(-0.16666666, 0.22222222),
    float2(0.22222222, 0.72222222)
};

// Gaussian weight function
float GaussianWeight(float x, float sigma)
{
    return exp(-x * x / (2.0 * sigma * sigma));
}

// Edge-preserving bilateral blur with extended Poisson disk sampling
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

    float3 centerPosition = positionTexture.SampleLevel(pointSampler, uv, 0).xyz;
    float3 centerNormal = normalTexture.SampleLevel(pointSampler, uv, 0).xyz;

    if (dot(centerPosition, centerPosition) < EPSILON || dot(centerNormal, centerNormal) < EPSILON)
    {
        outputTexture[dispatchThreadID.xy] = float4(0.0, 0.0, 0.0, 0.0);
        return;
    }

    centerNormal = SafeNormalize(centerNormal);
    float centerDepth = length(centerPosition);
    float4 centerSSGI = ssgiTexture.SampleLevel(pointSampler, uv, 0);

    // Start with center sample (higher weight for center)
    float3 accumulatedColor = centerSSGI.rgb * 2.0;
    float totalWeight = 2.0;

    float normalThreshold = 1.0 - constants.NormalSigma;
    float depthThreshold = constants.DepthSigma * max(centerDepth * 0.01, 0.02);

    // Adaptive blur radius based on depth
    float blurRadius = float(constants.BlurSize);
    float depthScale = saturate(centerDepth / 30.0); // Larger blur for distant objects
    blurRadius *= lerp(0.7, 1.3, depthScale);

    // Per-pixel rotation to break up patterns
    float rotAngle = float((dispatchThreadID.x * 1471343u + dispatchThreadID.y * 7919u) % 628u) * 0.01;
    float cosRot = cos(rotAngle);
    float sinRot = sin(rotAngle);

    // Multi-scale sampling: use all 32 samples with varying scales
    for (int i = 0; i < 32; i++)
    {
        // Rotate the sample offset
        float2 diskOffset = poissonDisk[i];
        float2 rotatedOffset = float2(
            diskOffset.x * cosRot - diskOffset.y * sinRot,
            diskOffset.x * sinRot + diskOffset.y * cosRot
        );
        
        // Apply scale variation for multi-scale blur
        float scale = (i < 16) ? 1.0 : 0.5;
        float2 offset = rotatedOffset * blurRadius * scale * constants.TexelSize;
        float2 sampleUV = uv + offset;

        if (sampleUV.x < 0.0 || sampleUV.x > 1.0 || sampleUV.y < 0.0 || sampleUV.y > 1.0)
            continue;

        float3 samplePosition = positionTexture.SampleLevel(pointSampler, sampleUV, 0).xyz;
        float3 sampleNormal = normalTexture.SampleLevel(pointSampler, sampleUV, 0).xyz;

        if (dot(samplePosition, samplePosition) < EPSILON)
            continue;

        sampleNormal = SafeNormalize(sampleNormal);
        float sampleDepth = length(samplePosition);

        // Normal similarity check with softer threshold
        float normalDot = dot(centerNormal, sampleNormal);
        if (normalDot < normalThreshold * 0.9)
            continue;

        // Depth similarity check with adaptive threshold
        float depthDiff = abs(centerDepth - sampleDepth);
        float adaptiveDepthThreshold = depthThreshold * (1.0 + depthScale * 2.0);
        if (depthDiff > adaptiveDepthThreshold * 4.0)
            continue;

        // Spatial weight - Gaussian falloff
        float dist = length(diskOffset);
        float spatialSigma = 0.5;
        float spatialWeight = GaussianWeight(dist, spatialSigma);

        // Normal weight - exponential falloff
        float normalWeight = pow(max(normalDot, 0.0), 8.0);

        // Depth weight - exponential falloff
        float depthWeight = exp(-depthDiff / max(adaptiveDepthThreshold, 0.001));

        // Plane consistency - reject samples far from tangent plane
        float3 posDiff = samplePosition - centerPosition;
        float planeDist = abs(dot(posDiff, centerNormal));
        float planeWeight = exp(-planeDist / (adaptiveDepthThreshold * 2.0));

        // Luminance similarity weight to preserve edges
        float4 sampleColor = ssgiTexture.SampleLevel(pointSampler, sampleUV, 0);
        float centerLum = dot(centerSSGI.rgb, float3(0.299, 0.587, 0.114));
        float sampleLum = dot(sampleColor.rgb, float3(0.299, 0.587, 0.114));
        float lumDiff = abs(centerLum - sampleLum);
        float lumWeight = exp(-lumDiff * lumDiff * 10.0);

        float weight = spatialWeight * normalWeight * depthWeight * planeWeight * lumWeight;
        
        // Scale weight for outer samples
        weight *= (i < 16) ? 1.0 : 0.6;

        accumulatedColor += sampleColor.rgb * weight;
        totalWeight += weight;
    }

    float3 result = accumulatedColor / max(totalWeight, EPSILON);
    
    // Optional: slight sharpening to counteract blur
    float3 sharpened = result + (result - centerSSGI.rgb) * 0.1;
    result = lerp(result, sharpened, 0.2);

    outputTexture[dispatchThreadID.xy] = float4(max(result, 0.0), 1.0);
}
