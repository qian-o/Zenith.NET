static const float EPSILON = 0.0001;

struct SSAOConstants
{
    float4x4 View;

    float4x4 Projection;

    float2 ScreenSize;

    float2 NoiseScale;

    float Radius;

    float Bias;

    float Intensity;

    int KernelSize;
};

ConstantBuffer<SSAOConstants> constants;
StructuredBuffer<float4> kernelBuffer;
Texture2D positionTexture;
Texture2D normalTexture;
Texture2D noiseTexture;
RWTexture2D outputTexture;
SamplerState pointSampler;

float3 SafeNormalize(float3 v)
{
    float len = dot(v, v);

    return len > EPSILON ? v * rsqrt(len) : float3(0.0, 1.0, 0.0);
}

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

    float3 worldPosition = positionTexture.SampleLevel(pointSampler, uv, 0).xyz;
    float3 worldNormal = normalTexture.SampleLevel(pointSampler, uv, 0).xyz;

    float posLenSq = dot(worldPosition, worldPosition);
    float normalLenSq = dot(worldNormal, worldNormal);

    if (posLenSq < EPSILON || normalLenSq < EPSILON)
    {
        outputTexture[dispatchThreadID.xy] = 1.0;

        return;
    }

    float3 viewPosition = mul(float4(worldPosition, 1.0), constants.View).xyz;
    float3 viewNormal = SafeNormalize(mul(float4(worldNormal, 0.0), constants.View).xyz);

    float3 randomRotation = noiseTexture.SampleLevel(pointSampler, uv * constants.NoiseScale, 0).xyz * 2.0 - 1.0;
    randomRotation = SafeNormalize(randomRotation);

    float3 tangent = SafeNormalize(randomRotation - viewNormal * dot(randomRotation, viewNormal));
    float3 bitangent = cross(viewNormal, tangent);
    float3x3 tangentToView = float3x3(tangent, bitangent, viewNormal);

    float totalOcclusion = 0.0;
    float validSamples = 0.0;

    for (int i = 0; i < constants.KernelSize; i++)
    {
        float3 sampleOffset = mul(kernelBuffer[i].xyz, tangentToView);
        float3 samplePosition = viewPosition + sampleOffset * constants.Radius;

        float4 projectedPosition = mul(float4(samplePosition, 1.0), constants.Projection);
        projectedPosition.xyz /= projectedPosition.w;

        float2 sampleUV = projectedPosition.xy * 0.5 + 0.5;
        sampleUV.y = 1.0 - sampleUV.y;

        if (any(sampleUV < 0.0) || any(sampleUV > 1.0))
        {
            continue;
        }

        float3 sampledWorldPosition = positionTexture.SampleLevel(pointSampler, sampleUV, 0).xyz;

        if (dot(sampledWorldPosition, sampledWorldPosition) < EPSILON)
        {
            continue;
        }

        float3 sampledViewPosition = mul(float4(sampledWorldPosition, 1.0), constants.View).xyz;
        float geometryDepth = sampledViewPosition.z;

        float depthDifference = abs(viewPosition.z - geometryDepth);
        float rangeWeight = smoothstep(0.0, 1.0, constants.Radius / (depthDifference + EPSILON));

        float isOccluded = geometryDepth >= samplePosition.z + constants.Bias ? 1.0 : 0.0;
        totalOcclusion += isOccluded * rangeWeight;

        validSamples += 1.0;
    }

    float visibility = 1.0;

    if (validSamples > 0.0)
    {
        visibility = 1.0 - (totalOcclusion / validSamples);
        visibility = pow(saturate(visibility), constants.Intensity);
    }

    outputTexture[dispatchThreadID.xy] = visibility;
}
