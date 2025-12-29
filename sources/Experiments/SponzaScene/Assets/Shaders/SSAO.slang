import "Common/Fullscreen";

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
SamplerState pointSampler;

float PSMain(PSInput input) : SV_Target0
{
    float2 uv = input.TexCoord;

    float4 positionData = positionTexture.Sample(pointSampler, uv);
    float3 worldPosition = positionData.xyz;
    float linearDepth = positionData.w;

    if (linearDepth <= 0.001)
    {
        return 1.0;
    }

    float3 viewPosition = mul(float4(worldPosition, 1.0), constants.View).xyz;
    float3 viewNormal = normalize(mul(float4(normalTexture.Sample(pointSampler, uv).xyz, 0.0), constants.View).xyz);

    float3 randomRotation = normalize(noiseTexture.Sample(pointSampler, uv * constants.NoiseScale).xyz * 2.0 - 1.0);

    float3 tangent = normalize(randomRotation - viewNormal * dot(randomRotation, viewNormal));
    float3 bitangent = cross(viewNormal, tangent);
    float3x3 tangentToView = float3x3(tangent, bitangent, viewNormal);

    float totalOcclusion = 0.0;

    for (int i = 0; i < constants.KernelSize; i++)
    {
        float3 sampleOffset = mul(kernelBuffer[i].xyz, tangentToView);
        float3 samplePosition = viewPosition + sampleOffset * constants.Radius;

        float4 projectedPosition = mul(float4(samplePosition, 1.0), constants.Projection);
        projectedPosition.xyz /= projectedPosition.w;

        float2 sampleUV = projectedPosition.xy * 0.5 + 0.5;
        sampleUV.y = 1.0 - sampleUV.y;

        float3 sampledWorldPosition = positionTexture.Sample(pointSampler, sampleUV).xyz;
        float3 sampledViewPosition = mul(float4(sampledWorldPosition, 1.0), constants.View).xyz;
        float geometryDepth = sampledViewPosition.z;

        float depthDifference = abs(viewPosition.z - geometryDepth);
        float rangeWeight = smoothstep(0.0, 1.0, constants.Radius / (depthDifference + 0.0001));

        float isOccluded = geometryDepth >= samplePosition.z + constants.Bias ? 1.0 : 0.0;
        totalOcclusion += isOccluded * rangeWeight;
    }

    float visibility = 1.0 - (totalOcclusion / float(constants.KernelSize));
    visibility = pow(saturate(visibility), constants.Intensity);

    return visibility;
}
