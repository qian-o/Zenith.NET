// SVGF Temporal Accumulation Pass (Optimized for Performance)
// Accumulates color and computes variance in a single pass

static const float EPSILON = 0.0001;

struct SVGFTemporalConstants
{
    float4x4 PrevViewProjection;
    float2 ViewportSize;
    float2 TexelSize;
    float TemporalAlpha;
    float MomentsAlpha;
    uint FrameIndex;
    float Padding;
};

ConstantBuffer<SVGFTemporalConstants> constants;
Texture2D colorTexture;
Texture2D historyColorTexture;
Texture2D historyMomentsTexture;
Texture2D positionTexture;
Texture2D normalTexture;
Texture2D prevPositionTexture;
Texture2D prevNormalTexture;
RWTexture2D<float4> outputColor;
RWTexture2D<float4> outputMoments;
SamplerState linearSampler;

float3 SafeNormalize(float3 v)
{
    float len = dot(v, v);
    return len > EPSILON ? v * rsqrt(len) : float3(0.0, 1.0, 0.0);
}

float Luminance(float3 color)
{
    return dot(color, float3(0.299, 0.587, 0.114));
}

[numthreads(16, 16, 1)]
void CSMain(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint width, height;
    outputColor.GetDimensions(width, height);
    
    if (dispatchThreadID.x >= width || dispatchThreadID.y >= height)
        return;
    
    float2 uv = (float2(dispatchThreadID.xy) + 0.5) / float2(width, height);
    
    float3 currentColor = colorTexture.SampleLevel(linearSampler, uv, 0).rgb;
    float3 currentPos = positionTexture.SampleLevel(linearSampler, uv, 0).xyz;
    float3 currentNormal = normalTexture.SampleLevel(linearSampler, uv, 0).xyz;
    
    if (dot(currentPos, currentPos) < EPSILON || dot(currentNormal, currentNormal) < EPSILON)
    {
        outputColor[dispatchThreadID.xy] = float4(currentColor, 1.0);
        outputMoments[dispatchThreadID.xy] = float4(0.0, 0.0, 0.0, 0.0);
        return;
    }
    
    currentNormal = SafeNormalize(currentNormal);
    float currentLum = Luminance(currentColor);
    
    // Fast 3x3 variance estimation
    float3 colorMean = float3(0.0, 0.0, 0.0);
    float3 colorM2 = float3(0.0, 0.0, 0.0);
    
    [unroll]
    for (int dy = -1; dy <= 1; dy++)
    {
        [unroll]
        for (int dx = -1; dx <= 1; dx++)
        {
            float2 sampleUV = uv + float2(dx, dy) * constants.TexelSize;
            float3 sampleColor = colorTexture.SampleLevel(linearSampler, sampleUV, 0).rgb;
            colorMean += sampleColor;
            colorM2 += sampleColor * sampleColor;
        }
    }
    
    colorMean /= 9.0;
    colorM2 /= 9.0;
    
    float3 colorStdDev = sqrt(max(colorM2 - colorMean * colorMean, 0.0));
    float spatialVariance = Luminance(colorStdDev);
    
    // Reproject to previous frame
    float4 prevClip = mul(float4(currentPos, 1.0), constants.PrevViewProjection);
    float2 prevUV = float2(-1.0, -1.0);
    bool validHistory = false;
    
    if (prevClip.w > 0.0)
    {
        prevClip.xyz /= prevClip.w;
        prevUV = float2(prevClip.x * 0.5 + 0.5, -prevClip.y * 0.5 + 0.5);
        
        if (prevUV.x > 0.01 && prevUV.x < 0.99 && prevUV.y > 0.01 && prevUV.y < 0.99)
        {
            float3 prevPos = prevPositionTexture.SampleLevel(linearSampler, prevUV, 0).xyz;
            float3 prevNormal = prevNormalTexture.SampleLevel(linearSampler, prevUV, 0).xyz;
            
            if (dot(prevPos, prevPos) > EPSILON)
            {
                prevNormal = SafeNormalize(prevNormal);
                float posDiff = length(currentPos - prevPos);
                float depthThreshold = max(length(currentPos) * 0.03, 0.15);
                float normalDot = dot(currentNormal, prevNormal);
                
                validHistory = (posDiff < depthThreshold) && (normalDot > 0.85);
            }
        }
    }
    
    float3 accumulatedColor;
    float variance;
    float historyLength;
    
    // Dark scene detection
    float darkFactor = saturate(1.0 - currentLum * 3.0);
    
    if (validHistory && constants.FrameIndex > 0u)
    {
        // Simple bilinear sampling (faster than Catmull-Rom)
        float3 historyColor = historyColorTexture.SampleLevel(linearSampler, prevUV, 0).rgb;
        float4 historyMoments = historyMomentsTexture.SampleLevel(linearSampler, prevUV, 0);
        
        historyLength = historyMoments.y;
        
        // Wider clip box for dark scenes
        float gamma = lerp(1.5, 3.0, darkFactor);
        float3 clipMin = colorMean - colorStdDev * gamma;
        float3 clipMax = colorMean + colorStdDev * gamma;
        float3 clippedHistory = clamp(historyColor, clipMin, clipMax);
        
        // Slower alpha for dark scenes
        float maxHistoryLength = lerp(20.0, 32.0, darkFactor);
        float baseAlpha = lerp(constants.TemporalAlpha, constants.TemporalAlpha * 0.4, darkFactor);
        float alpha = max(baseAlpha, 1.0 / (historyLength + 1.0));
        
        accumulatedColor = lerp(clippedHistory, currentColor, alpha);
        
        // Update variance
        float historyVariance = historyMoments.x;
        float blend = min(historyLength / 4.0, 1.0);
        variance = lerp(spatialVariance, historyVariance * 0.85 + spatialVariance * 0.15, blend);
        variance *= (1.0 + darkFactor * 1.0);
        
        historyLength = min(historyLength + 1.0, maxHistoryLength);
    }
    else
    {
        accumulatedColor = currentColor;
        variance = spatialVariance * 3.0;
        historyLength = 1.0;
    }
    
    outputColor[dispatchThreadID.xy] = float4(accumulatedColor, 1.0);
    outputMoments[dispatchThreadID.xy] = float4(variance, historyLength, 0.0, 0.0);
}
