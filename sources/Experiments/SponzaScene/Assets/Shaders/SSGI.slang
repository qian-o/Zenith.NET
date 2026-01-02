static const float PI = 3.14159265359;
static const float TWO_PI = 6.28318530718;
static const float EPSILON = 0.0001;

struct SSGIConstants
{
    float4x4 View;
    float4x4 Projection;
    float4x4 PrevViewProjection;
    float4 CameraPosition;
    float2 ViewportSize;
    float2 ViewportPixelSize;
    float MaxDistance;
    float Thickness;
    float Intensity;
    int MaxSteps;
    int BinarySearchSteps;
    float RoughnessThreshold;
    int SampleCount;
    float TemporalBlend;
    uint FrameIndex;
    float CameraMotion;
};

ConstantBuffer<SSGIConstants> constants;
Texture2D positionTexture;
Texture2D normalTexture;
Texture2D albedoTexture;
Texture2D metallicRoughnessTexture;
Texture2D litColorHistoryTexture;
Texture2D historyTexture;
RWTexture2D<float4> outputTexture;
SamplerState linearSampler;

float3 SafeNormalize(float3 v)
{
    float len = dot(v, v);
    return len > EPSILON ? v * rsqrt(len) : float3(0.0, 1.0, 0.0);
}

// Fast hash - no loops
float2 FastHash(uint2 p)
{
    uint h = p.x * 73856093u ^ p.y * 19349663u;
    h = (h ^ (h >> 16u)) * 0x45d9f3bu;
    h = (h ^ (h >> 16u)) * 0x45d9f3bu;
    return float2(float(h & 0xFFFFu) / 65535.0, float((h >> 16u) & 0xFFFFu) / 65535.0);
}

// Pre-computed Halton samples (base 2, 3) - avoid runtime loop
static const float2 haltonSamples[8] = {
    float2(0.5, 0.333333),
    float2(0.25, 0.666667),
    float2(0.75, 0.111111),
    float2(0.125, 0.444444),
    float2(0.625, 0.777778),
    float2(0.375, 0.222222),
    float2(0.875, 0.555556),
    float2(0.0625, 0.888889)
};

float3x3 CreateTBN(float3 N)
{
    float3 up = abs(N.y) < 0.999 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
    float3 T = normalize(cross(up, N));
    float3 B = cross(N, T);
    return float3x3(T, B, N);
}

// Optimized: compute ViewProjection matrix once and reuse
float3 WorldToScreen(float3 worldPos, float4x4 viewProj)
{
    float4 clipPos = mul(float4(worldPos, 1.0), viewProj);
    if (clipPos.w <= 0.0) return float3(-1.0, -1.0, -1.0);
    clipPos.xyz /= clipPos.w;
    return float3(clipPos.x * 0.5 + 0.5, -clipPos.y * 0.5 + 0.5, clipPos.z);
}

// Simplified ray marching - linear stepping only (no binary search refinement)
float4 TraceRayFast(float3 origin, float3 direction, float jitter, float4x4 viewProj)
{
    float3 startScreen = WorldToScreen(origin, viewProj);
    if (startScreen.x < 0.0) return float4(0.0, 0.0, 0.0, 0.0);

    float3 endWorld = origin + direction * constants.MaxDistance;
    float3 endScreen = WorldToScreen(endWorld, viewProj);

    // Simple clamp instead of binary search for valid end point
    if (endScreen.x < 0.0)
    {
        // Try a shorter distance
        endWorld = origin + direction * constants.MaxDistance * 0.5;
        endScreen = WorldToScreen(endWorld, viewProj);
        if (endScreen.x < 0.0) return float4(0.0, 0.0, 0.0, 0.0);
    }

    float3 rayDir = endScreen - startScreen;
    float rayLen = length(rayDir.xy);
    if (rayLen < 0.003) return float4(0.0, 0.0, 0.0, 0.0);
    
    rayDir /= rayLen;
    
    // Fixed step count for predictable performance
    int stepCount = constants.MaxSteps;
    float stepSize = rayLen / float(stepCount);
    
    // Start with jitter
    float3 pos = startScreen + rayDir * stepSize * (0.5 + jitter * 0.5);
    
    float thickness = constants.Thickness * 0.02;
    
    [loop]
    for (int i = 0; i < stepCount; i++)
    {
        // Early out for screen bounds
        if (pos.x < 0.005 || pos.x > 0.995 || pos.y < 0.005 || pos.y > 0.995)
            break;

        float3 samplePos = positionTexture.SampleLevel(linearSampler, pos.xy, 0).xyz;
        
        if (dot(samplePos, samplePos) > EPSILON)
        {
            float3 sampleScreen = WorldToScreen(samplePos, viewProj);
            
            if (sampleScreen.x >= 0.0)
            {
                float depthDiff = pos.z - sampleScreen.z;
                
                if (depthDiff > 0.0 && depthDiff < thickness)
                {
                    float2 hitUV = pos.xy;
                    
                    // Simple confidence
                    float2 edgeDist = min(hitUV, 1.0 - hitUV);
                    float confidence = saturate(min(edgeDist.x, edgeDist.y) * 20.0);
                    confidence *= 1.0 - float(i) / float(stepCount) * 0.3;
                    
                    // Sample color directly
                    float3 hitLit = litColorHistoryTexture.SampleLevel(linearSampler, hitUV, 0).rgb;
                    float hitLum = dot(hitLit, float3(0.299, 0.587, 0.114));
                    
                    if (hitLum < 0.02)
                    {
                        float3 hitAlbedo = albedoTexture.SampleLevel(linearSampler, hitUV, 0).rgb;
                        hitLit = hitAlbedo * 0.3;
                    }
                    
                    return float4(hitLit, confidence);
                }
            }
        }
        
        pos += rayDir * stepSize;
    }
    
    return float4(0.0, 0.0, 0.0, 0.0);
}

[numthreads(16, 16, 1)]
void CSMain(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint width, height;
    outputTexture.GetDimensions(width, height);
    if (dispatchThreadID.x >= width || dispatchThreadID.y >= height) return;

    float2 uv = (float2(dispatchThreadID.xy) + 0.5) / float2(width, height);
    float3 worldPos = positionTexture.SampleLevel(linearSampler, uv, 0).xyz;
    float3 normal = normalTexture.SampleLevel(linearSampler, uv, 0).xyz;

    if (dot(worldPos, worldPos) < EPSILON || dot(normal, normal) < EPSILON)
    {
        outputTexture[dispatchThreadID.xy] = float4(0.0, 0.0, 0.0, 0.0);
        return;
    }

    normal = SafeNormalize(normal);
    float2 metallicRoughness = metallicRoughnessTexture.SampleLevel(linearSampler, uv, 0).xy;
    float metallic = metallicRoughness.x;

    // Pre-compute ViewProjection matrix
    float4x4 viewProj = mul(constants.View, constants.Projection);

    // Get history for temporal accumulation
    float4 prevClip = mul(float4(worldPos, 1.0), constants.PrevViewProjection);
    float2 historyUV = float2(-1.0, -1.0);
    bool validHistory = false;
    
    if (prevClip.w > 0.0)
    {
        prevClip.xyz /= prevClip.w;
        historyUV = float2(prevClip.x * 0.5 + 0.5, -prevClip.y * 0.5 + 0.5);
        validHistory = historyUV.x > 0.02 && historyUV.x < 0.98 && 
                       historyUV.y > 0.02 && historyUV.y < 0.98;
    }
    
    float4 historyValue = validHistory ? historyTexture.SampleLevel(linearSampler, historyUV, 0) : float4(0.0, 0.0, 0.0, 0.0);

    float3x3 TBN = CreateTBN(normal);
    
    // Stable per-pixel hash
    float2 pixelHash = FastHash(dispatchThreadID.xy);
    float pixelJitter = pixelHash.y;
    
    float3 accumulatedGI = float3(0.0, 0.0, 0.0);
    float totalWeight = 0.0;
    int hitCount = 0;

    int sampleCount = min(constants.SampleCount, 8);
    
    [unroll]
    for (int s = 0; s < 8; s++)
    {
        if (s >= sampleCount) break;
        
        // Use pre-computed Halton samples
        float2 xi = frac(haltonSamples[s] + pixelHash);
        
        // Cosine-weighted hemisphere sampling
        float phi = TWO_PI * xi.x;
        float cosTheta = sqrt(1.0 - xi.y);
        float sinTheta = sqrt(xi.y);
        
        float3 localDir = float3(cos(phi) * sinTheta, sin(phi) * sinTheta, cosTheta);
        float3 sampleDir = normalize(mul(localDir, TBN));
        
        float NdotD = dot(sampleDir, normal);
        if (NdotD < 0.0)
        {
            sampleDir = reflect(sampleDir, normal);
            NdotD = -NdotD;
        }
        
        if (NdotD < 0.08) continue;

        float3 rayOrigin = worldPos + normal * 0.1;
        float4 traceResult = TraceRayFast(rayOrigin, sampleDir, pixelJitter, viewProj);
        
        if (traceResult.w > 0.03)
        {
            hitCount++;
            float weight = NdotD * traceResult.w;
            accumulatedGI += traceResult.rgb * weight;
            totalWeight += weight;
        }
    }

    float3 currentGI;
    if (totalWeight > EPSILON && hitCount > 0)
    {
        currentGI = accumulatedGI / totalWeight;
    }
    else
    {
        // Simple ambient fallback
        float3 albedo = albedoTexture.SampleLevel(linearSampler, uv, 0).rgb;
        float skyFactor = saturate(normal.y * 0.5 + 0.5);
        float3 skyAmbient = lerp(float3(0.08, 0.06, 0.05), float3(0.10, 0.12, 0.15), skyFactor);
        currentGI = albedo * skyAmbient;
    }

    // Apply intensity and material
    currentGI *= constants.Intensity;
    currentGI *= (1.0 - metallic * 0.5);

    // Temporal accumulation
    float temporalWeight = constants.TemporalBlend;
    temporalWeight *= (1.0 - constants.CameraMotion * 0.6);
    
    if (!validHistory)
    {
        temporalWeight = 0.0;
    }
    else
    {
        float2 edgeDist = min(historyUV, 1.0 - historyUV);
        temporalWeight *= saturate(min(edgeDist.x, edgeDist.y) * 20.0);
        
        // Luminance-based rejection
        float lumCurrent = dot(currentGI, float3(0.299, 0.587, 0.114));
        float lumHistory = dot(historyValue.rgb, float3(0.299, 0.587, 0.114));
        float lumRatio = abs(lumCurrent - lumHistory) / (max(lumCurrent, lumHistory) + 0.05);
        
        if (lumRatio > 0.5)
            temporalWeight *= 0.6;
    }
    
    temporalWeight *= saturate(float(constants.FrameIndex) / 12.0);

    float3 finalGI = lerp(currentGI, historyValue.rgb, temporalWeight);
    finalGI = clamp(finalGI, 0.0, 3.0);

    outputTexture[dispatchThreadID.xy] = float4(finalGI, 1.0);
}
