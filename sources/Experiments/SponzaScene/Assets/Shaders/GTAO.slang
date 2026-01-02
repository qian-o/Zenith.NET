///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// GTAO - Final optimized version
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

static const float PI = 3.14159265359;

struct GTAOConstants
{
    float4x4 View;
    float4x4 Projection;

    float2 ViewportSize;
    float2 ViewportPixelSize;

    float EffectRadius;
    float EffectFalloffRange;
    float RadiusMultiplier;
    float FinalValuePower;

    float SampleDistributionPower;
    float ThinOccluderCompensation;
    int SliceCount;
    int StepsPerSlice;
};

ConstantBuffer<GTAOConstants> constants;
Texture2D<float4> positionTexture;
Texture2D<float4> normalTexture;
RWTexture2D<float> outputTexture;
SamplerState pointSampler;

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Helper functions
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

float3 GetViewPosition(float2 uv)
{
    float4 worldPos = positionTexture.SampleLevel(pointSampler, uv, 0);
    
    if (worldPos.w == 0)
    {
        return float3(0, 0, 1e10);
    }
    
    return mul(float4(worldPos.xyz, 1.0), constants.View).xyz;
}

float3 GetViewNormal(float2 uv)
{
    float3 worldNormal = normalTexture.SampleLevel(pointSampler, uv, 0).xyz;
    
    if (dot(worldNormal, worldNormal) < 0.5)
    {
        return float3(0, 0, -1);
    }
    
    return normalize(mul(float4(normalize(worldNormal), 0.0), constants.View).xyz);
}

// 合并位置和法线采样，减少纹理访问
void GetViewPositionAndNormal(float2 uv, out float3 pos, out float3 normal)
{
    float4 worldPos = positionTexture.SampleLevel(pointSampler, uv, 0);
    
    if (worldPos.w == 0)
    {
        pos = float3(0, 0, 1e10);
        normal = float3(0, 0, -1);
        return;
    }
    
    pos = mul(float4(worldPos.xyz, 1.0), constants.View).xyz;
    
    float3 worldNormal = normalTexture.SampleLevel(pointSampler, uv, 0).xyz;
    normal = normalize(mul(float4(normalize(worldNormal), 0.0), constants.View).xyz);
}

///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Main GTAO
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

[numthreads(16, 16, 1)]
void CSMain(uint3 dispatchThreadID : SV_DispatchThreadID)
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
    
    if (viewDepth > 500.0 || viewDepth < 0.1)
    {
        outputTexture[pixelCoord] = 1.0;
        return;
    }
    
    float3 viewNormal = GetViewNormal(uv);
    
    // 优化的噪声生成
    uint2 noiseCoord = pixelCoord & 3u;
    float noise1 = float(noiseCoord.x + noiseCoord.y * 4u) * 0.0625; // 1/16
    float noise2 = frac(noise1 * 7.0 + 0.5);
    
    // 预计算常用值
    float worldRadius = constants.EffectRadius * constants.RadiusMultiplier;
    float invWorldRadius = 1.0 / worldRadius;
    float projScale = abs(constants.Projection[1][1]);
    float screenRadius = clamp((worldRadius * projScale * constants.ViewportSize.y * 0.5) / viewDepth, 2.0, 100.0);
    
    int sliceCount = constants.SliceCount;
    int stepsPerSlice = constants.StepsPerSlice;
    float invStepsPerSlice = 1.0 / float(stepsPerSlice);
    
    float totalOcclusion = 0.0;
    float totalWeight = 0.0;
    
    float angleStep = PI / float(sliceCount);
    float startAngle = noise1 * PI;
    
    [unroll]
    for (int slice = 0; slice < 9; slice++) // 最大支持 9 个 slice
    {
        if (slice >= sliceCount) break;
        
        float angle = startAngle + angleStep * (float(slice) + 0.5);
        float2 dir;
        sincos(angle, dir.y, dir.x);
        
        float2 scaledDir = dir * constants.ViewportPixelSize;
        
        [unroll]
        for (int step = 1; step <= 12; step++) // 最大支持 12 步
        {
            if (step > stepsPerSlice) break;
            
            float jitter = frac(noise2 + float(step) * 0.381966);
            float t = (float(step) - 0.5 + jitter * 0.5) * invStepsPerSlice;
            
            float sampleRadius = max(pow(t, constants.SampleDistributionPower) * screenRadius, 1.0);
            float2 offset = scaledDir * sampleRadius;
            
            // 正向采样
            float2 sampleUV1 = uv + offset;
            if (all(sampleUV1 > 0.0) && all(sampleUV1 < 1.0))
            {
                float3 samplePos, sampleNormal;
                GetViewPositionAndNormal(sampleUV1, samplePos, sampleNormal);
                
                if (abs(samplePos.z) < 500.0)
                {
                    float3 toSample = samplePos - viewPos;
                    float distSq = dot(toSample, toSample);
                    float dist = sqrt(distSq);
                    
                    if (dist > 0.001 && dist < worldRadius)
                    {
                        float3 sampleDir = toSample / dist;
                        float NdotS = max(dot(viewNormal, sampleDir), 0.0);
                        
                        // 曲面修正
                        float normalSimilarity = dot(viewNormal, sampleNormal);
                        float curvatureWeight = saturate(1.0 - normalSimilarity * 0.8);
                        
                        // 距离衰减
                        float distRatio = dist * invWorldRadius;
                        float falloff = saturate(1.0 - distRatio * distRatio);
                        falloff = pow(falloff, constants.EffectFalloffRange);
                        
                        totalOcclusion += NdotS * curvatureWeight * falloff;
                        totalWeight += 1.0;
                    }
                }
            }
            
            // 反向采样
            float2 sampleUV2 = uv - offset;
            if (all(sampleUV2 > 0.0) && all(sampleUV2 < 1.0))
            {
                float3 samplePos, sampleNormal;
                GetViewPositionAndNormal(sampleUV2, samplePos, sampleNormal);
                
                if (abs(samplePos.z) < 500.0)
                {
                    float3 toSample = samplePos - viewPos;
                    float distSq = dot(toSample, toSample);
                    float dist = sqrt(distSq);
                    
                    if (dist > 0.001 && dist < worldRadius)
                    {
                        float3 sampleDir = toSample / dist;
                        float NdotS = max(dot(viewNormal, sampleDir), 0.0);
                        
                        float normalSimilarity = dot(viewNormal, sampleNormal);
                        float curvatureWeight = saturate(1.0 - normalSimilarity * 0.8);
                        
                        float distRatio = dist * invWorldRadius;
                        float falloff = saturate(1.0 - distRatio * distRatio);
                        falloff = pow(falloff, constants.EffectFalloffRange);
                        
                        totalOcclusion += NdotS * curvatureWeight * falloff;
                        totalWeight += 1.0;
                    }
                }
            }
        }
    }
    
    float ao = 1.0;
    if (totalWeight > 0.0)
    {
        ao = 1.0 - saturate(totalOcclusion / totalWeight);
    }
    
    ao = pow(saturate(ao), constants.FinalValuePower);
    
    outputTexture[pixelCoord] = ao;
}