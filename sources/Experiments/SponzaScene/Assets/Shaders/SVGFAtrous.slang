static const float EPSILON = 1e-6;
static const float KERNEL[3] = { 1.0, 0.75, 0.25 };

struct AtrousConstants
{
    float ViewportWidth;

    float ViewportHeight;

    int StepWidth;

    float PhiColor;

    float PhiNormal;

    float PhiDepth;

    float _pad0;

    float _pad1;
};

ConstantBuffer<AtrousConstants> constants;
Texture2D inputTexture;
Texture2D positionTexture;
Texture2D normalTexture;
RWTexture2D outputTexture;

float Luminance(float3 c)
{
    return dot(c, float3(0.2126, 0.7152, 0.0722));
}

float3 SafeNormalize(float3 v)
{
    float lenSq = dot(v, v);

    return lenSq > EPSILON ? v * rsqrt(lenSq) : float3(0.0, 0.0, 1.0);
}

[numthreads(16, 16, 1)]
void CSMain(uint3 id: SV_DispatchThreadID)
{
    uint2 coord = id.xy;

    if (any(coord >= uint2(constants.ViewportWidth, constants.ViewportHeight)))
    {
        return;
    }

    float3 centerPos = positionTexture.Load(int3(coord, 0)).xyz;

    if (dot(centerPos, centerPos) < EPSILON)
    {
        outputTexture[coord] = float4(0.0);

        return;
    }

    float4 centerData = inputTexture.Load(int3(coord, 0));
    float3 centerColor = centerData.rgb;
    float centerVar = max(centerData.a, EPSILON);
    float3 centerNormal = SafeNormalize(normalTexture.Load(int3(coord, 0)).xyz);
    float centerLum = max(Luminance(centerColor), EPSILON);
    float centerDepth = length(centerPos);

    float phiLum = constants.PhiColor * sqrt(centerVar * centerLum * centerLum) + 0.001;
    float phiDepth = centerDepth * constants.PhiDepth + 0.01;

    float3 colorSum = centerColor;
    float varSum = centerVar;
    float wSum = 1.0;

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

            if (any(p < 0) || any(p >= int2(constants.ViewportWidth, constants.ViewportHeight)))
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

            float wK = KERNEL[abs(dx)] * KERNEL[abs(dy)];
            float wL = exp(-abs(Luminance(sampleData.rgb) - centerLum) / phiLum);
            float wN = pow(max(dot(centerNormal, sampleNormal), 0.0), constants.PhiNormal);
            float wD = exp(-abs(dot(samplePos - centerPos, centerNormal)) / phiDepth);

            float w = wK * wL * wN * wD;

            colorSum += sampleData.rgb * w;
            varSum += max(sampleData.a, EPSILON) * w;
            wSum += w;
        }
    }

    float invW = 1.0 / wSum;

    outputTexture[coord] = float4(colorSum * invW, varSum * invW);
}
