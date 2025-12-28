import "Common/Fullscreen";

struct SSAOConstants
{
    float4x4 ViewProjection;

    float2 ScreenSize;

    float2 NoiseScale;

    float Radius;

    float Bias;

    float Intensity;

    int KernelSize;
};

ConstantBuffer<SSAOConstants> constants;
StructuredBuffer<float4> ssaoKernel;
Texture2D positionTexture;
Texture2D normalTexture;
Texture2D depthTexture;
Texture2D noiseTexture;
SamplerState pointSampler;
SamplerState linearSampler;

float4 PSMain(PSInput input) : SV_Target0
{
    float2 uv = input.TexCoord;

    // 获取当前像素的世界空间位置和法线
    float3 worldPosition = positionTexture.Sample(linearSampler, uv).xyz;
    float3 worldNormal = normalize(normalTexture.Sample(linearSampler, uv).xyz);

    // 获取深度，跳过天空像素
    float depth = depthTexture.Sample(pointSampler, uv).r;
    if (depth >= 0.99)
    {
        return float4(1.0, 1.0, 1.0, 1.0);
    }

    // 采样噪声用于随机旋转采样核
    float3 randomVec = normalize(noiseTexture.Sample(pointSampler, uv * constants.NoiseScale).xyz * 2.0 - 1.0);

    // 创建 TBN 矩阵，将采样向量从切线空间转换到世界空间
    float3 tangent = normalize(randomVec - worldNormal * dot(randomVec, worldNormal));
    float3 bitangent = cross(worldNormal, tangent);
    float3x3 TBN = float3x3(tangent, bitangent, worldNormal);

    // 累计遮蔽
    float occlusion = 0.0;
    int validSamples = 0;

    for (int i = 0; i < constants.KernelSize; i++)
    {
        // 获取采样点（切线空间中的半球采样）
        float3 sampleDir = mul(ssaoKernel[i].xyz, TBN);
        float3 samplePos = worldPosition + sampleDir * constants.Radius;

        // 将采样点投影到屏幕空间
        float4 offset = mul(float4(samplePos, 1.0), constants.ViewProjection);
        offset.xyz /= offset.w;
        offset.xy = offset.xy * 0.5 + 0.5;
        offset.y = 1.0 - offset.y;

        // 检查 UV 是否在屏幕范围内
        if (offset.x < 0.0 || offset.x > 1.0 || offset.y < 0.0 || offset.y > 1.0)
        {
            continue;
        }

        // 获取采样点处的实际位置
        float3 sampledPosition = positionTexture.Sample(linearSampler, offset.xy).xyz;

        // 范围检查
        float distance = length(worldPosition - sampledPosition);
        float rangeCheck = smoothstep(0.0, 1.0, constants.Radius / max(distance, 0.001));

        // 深度比较：如果采样点在实际几何体后面，则被遮蔽
        float sampleDepth = length(sampledPosition - worldPosition);
        float fragDepth = length(samplePos - worldPosition);

        if (sampleDepth <= fragDepth - constants.Bias)
        {
            occlusion += rangeCheck;
        }

        validSamples++;
    }

    // 归一化并反转
    if (validSamples > 0)
    {
        occlusion = 1.0 - (occlusion / float(validSamples));
    }
    else
    {
        occlusion = 1.0;
    }

    // 应用强度
    occlusion = pow(saturate(occlusion), constants.Intensity);

    return float4(occlusion, occlusion, occlusion, 1.0);
}
