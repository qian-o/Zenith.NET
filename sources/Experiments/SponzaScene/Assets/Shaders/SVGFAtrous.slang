static const float EPSILON = 1e-4;
static const float KERNEL[3] = { 1.0, 0.667, 0.167 };

struct AtrousConstants
{
    float2 ViewportSize;

    int StepWidth;

    float PhiColor;

    float PhiNormal;

    float PhiDepth;
};

ConstantBuffer<AtrousConstants> constants;
Texture2D inputTexture;
Texture2D positionTexture;
Texture2D normalTexture;
RWTexture2D<float4> outputTexture;

float Luminance(float3 c)
{
    return dot(c, float3(0.2126, 0.7152, 0.0722));
}

float3 SafeNormalize(float3 v)
{
    float lenSq = dot(v, v);

    return lenSq > EPSILON ? v * rsqrt(lenSq) : float3(0, 0, 1);
}

[numthreads(16, 16, 1)]
void CSMain(uint3 id: SV_DispatchThreadID)
{
    uint2 coord = id.xy;

    if (any(coord >= uint2(constants.ViewportSize)))
    {
        return;
    }

    float4 centerData = inputTexture.Load(int3(coord, 0));
    float3 centerPos = positionTexture.Load(int3(coord, 0)).xyz;

    if (dot(centerPos, centerPos) < EPSILON)
    {
        outputTexture[coord] = 0;

        return;
    }

    float3 centerColor = centerData.rgb;
    float variance = max(centerData.a, EPSILON);
    float3 centerNormal = SafeNormalize(normalTexture.Load(int3(coord, 0)).xyz);
    float centerLum = Luminance(centerColor);
    float centerDepth = length(centerPos);
    float phiColor = constants.PhiColor * sqrt(variance) + EPSILON;
    float phiDepth = centerDepth * constants.PhiDepth + EPSILON;

    float3 colorSum = centerColor;
    float varSum = variance;
    float weightSum = 1.0;

    [unroll]
    for (int dy = -2; dy <= 2; dy++)
    {
        [unroll]
        for (int dx = -2; dx <= 2; dx++)
        {
            if (dx == 0 && dy == 0)
            {
                continue;
            }

            int2 p = int2(coord) + int2(dx, dy) * constants.StepWidth;

            if (any(p < 0) || any(p >= int2(constants.ViewportSize)))
            {
                continue;
            }

            float3 samplePos = positionTexture.Load(int3(p, 0)).xyz;

            if (dot(samplePos, samplePos) < EPSILON)
            {
                continue;
            }

            float4 sampleData = inputTexture.Load(int3(p, 0));
            float3 sampleNormal = SafeNormalize(normalTexture.Load(int3(p, 0)).xyz);

            float wKernel = KERNEL[abs(dx)] * KERNEL[abs(dy)];
            float wColor = exp(-abs(Luminance(sampleData.rgb) - centerLum) / phiColor);
            float wNormal = pow(max(dot(centerNormal, sampleNormal), 0), constants.PhiNormal);
            float wDepth = exp(-abs(dot(samplePos - centerPos, centerNormal)) / phiDepth);

            float w = wKernel * wColor * wNormal * wDepth;

            colorSum += sampleData.rgb * w;
            varSum += max(sampleData.a, EPSILON) * w * w;
            weightSum += w;
        }
    }

    float invWeight = 1.0 / weightSum;

    outputTexture[coord] = float4(colorSum * invWeight, varSum * invWeight * invWeight);
}
