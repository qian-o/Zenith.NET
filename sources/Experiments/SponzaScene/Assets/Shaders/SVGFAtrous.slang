// SVGF Atrous Wavelet Filter Pass (Optimized for Performance)
// Edge-preserving spatial filter - 3x3 kernel for speed

static const float EPSILON = 0.0001;
static const float PI = 3.14159265359;

struct SVGFAtrousConstants
{
    float2 TexelSize;
    int StepSize;
    float PhiColor;
    float PhiNormal;
    float PhiDepth;
    float SigmaLuminance;
    float Padding;
};

ConstantBuffer<SVGFAtrousConstants> constants;
Texture2D colorTexture;
Texture2D varianceTexture;
Texture2D positionTexture;
Texture2D normalTexture;
RWTexture2D<float4> outputColor;
SamplerState pointSampler;

float3 SafeNormalize(float3 v)
{
    float len = dot(v, v);
    return len > EPSILON ? v * rsqrt(len) : float3(0.0, 1.0, 0.0);
}

float Luminance(float3 color)
{
    return dot(color, float3(0.299, 0.587, 0.114));
}

// 3x3 Gaussian kernel - fast
static const float kernel3x3[9] = {
    1.0/16.0, 2.0/16.0, 1.0/16.0,
    2.0/16.0, 4.0/16.0, 2.0/16.0,
    1.0/16.0, 2.0/16.0, 1.0/16.0
};

static const int2 offsets3x3[9] = {
    int2(-1, -1), int2(0, -1), int2(1, -1),
    int2(-1,  0), int2(0,  0), int2(1,  0),
    int2(-1,  1), int2(0,  1), int2(1,  1)
};

[numthreads(16, 16, 1)]
void CSMain(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint width, height;
    outputColor.GetDimensions(width, height);
    
    if (dispatchThreadID.x >= width || dispatchThreadID.y >= height)
        return;
    
    float2 uv = (float2(dispatchThreadID.xy) + 0.5) / float2(width, height);
    
    float3 centerPos = positionTexture.SampleLevel(pointSampler, uv, 0).xyz;
    float3 centerNormal = normalTexture.SampleLevel(pointSampler, uv, 0).xyz;
    
    if (dot(centerPos, centerPos) < EPSILON || dot(centerNormal, centerNormal) < EPSILON)
    {
        float3 inputColor = colorTexture.SampleLevel(pointSampler, uv, 0).rgb;
        outputColor[dispatchThreadID.xy] = float4(inputColor, 1.0);
        return;
    }
    
    centerNormal = SafeNormalize(centerNormal);
    float centerDepth = length(centerPos);
    
    float3 centerColor = colorTexture.SampleLevel(pointSampler, uv, 0).rgb;
    float centerLum = Luminance(centerColor);
    
    float4 varianceData = varianceTexture.SampleLevel(pointSampler, uv, 0);
    float variance = varianceData.x;
    
    // Dark scene detection and boost
    float darkFactor = saturate(1.0 - centerLum * 3.0);
    float darkBoost = 1.0 + darkFactor * 2.0;
    
    float phiLum = constants.SigmaLuminance * sqrt(max(variance, EPSILON)) * darkBoost + 0.05;
    float depthBoost = 1.0 + darkFactor * 1.0;
    float normalBoost = 1.0 - darkFactor * 0.3;
    
    float3 sumColor = float3(0.0, 0.0, 0.0);
    float sumWeight = 0.0;
    
    int stepSize = constants.StepSize;
    
    // Per-pixel rotation to break patterns
    uint hash = dispatchThreadID.x * 73856093u ^ dispatchThreadID.y * 19349663u ^ uint(stepSize) * 83492791u;
    float rotation = float(hash & 0xFFu) / 255.0 * PI * 0.5;
    float cosR = cos(rotation);
    float sinR = sin(rotation);
    
    [unroll]
    for (int i = 0; i < 9; i++)
    {
        // Rotate offset
        float2 baseOffset = float2(offsets3x3[i]);
        float2 rotatedOffset = float2(
            baseOffset.x * cosR - baseOffset.y * sinR,
            baseOffset.x * sinR + baseOffset.y * cosR
        );
        
        float2 sampleUV = uv + rotatedOffset * float(stepSize) * constants.TexelSize;
        
        if (sampleUV.x < 0.0 || sampleUV.x > 1.0 || sampleUV.y < 0.0 || sampleUV.y > 1.0)
        {
            sumColor += centerColor * kernel3x3[i];
            sumWeight += kernel3x3[i];
            continue;
        }
        
        float3 samplePos = positionTexture.SampleLevel(pointSampler, sampleUV, 0).xyz;
        
        if (dot(samplePos, samplePos) < EPSILON)
        {
            sumColor += centerColor * kernel3x3[i];
            sumWeight += kernel3x3[i];
            continue;
        }
        
        float3 sampleNormal = normalTexture.SampleLevel(pointSampler, sampleUV, 0).xyz;
        sampleNormal = SafeNormalize(sampleNormal);
        float sampleDepth = length(samplePos);
        
        float3 sampleColor = colorTexture.SampleLevel(pointSampler, sampleUV, 0).rgb;
        float sampleLum = Luminance(sampleColor);
        
        // Simplified edge-stopping
        float normalDot = max(dot(centerNormal, sampleNormal), 0.0);
        float normalWeight = pow(normalDot, constants.PhiNormal * normalBoost);
        
        float depthDiff = abs(centerDepth - sampleDepth);
        float depthThreshold = max(centerDepth * constants.PhiDepth * depthBoost, 0.08);
        float depthWeight = exp(-depthDiff / depthThreshold);
        
        float lumDiff = abs(centerLum - sampleLum);
        float lumWeight = exp(-lumDiff / phiLum);
        
        // Dark scene relaxation
        if (centerLum < 0.1 && normalDot > 0.7)
        {
            lumWeight = max(lumWeight, 0.8);
            depthWeight = max(depthWeight, 0.7);
        }
        
        float weight = kernel3x3[i] * normalWeight * depthWeight * lumWeight;
        
        sumColor += sampleColor * weight;
        sumWeight += weight;
    }
    
    float3 result = sumWeight > EPSILON ? sumColor / sumWeight : centerColor;
    
    outputColor[dispatchThreadID.xy] = float4(result, 1.0);
}
