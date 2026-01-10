static const float EPSILON = 1e-6;
static const float KERNEL[3] = { 1.0, 0.75, 0.25 };

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
        return;

    float3 centerPos = positionTexture.Load(int3(coord, 0)).xyz;
    if (dot(centerPos, centerPos) < EPSILON)
    {
        outputTexture[coord] = 0;
        return;
    }

    float4 centerData = inputTexture.Load(int3(coord, 0));
    float3 centerColor = centerData.rgb;
    float centerVar = max(centerData.a, EPSILON);
    float3 centerNormal = SafeNormalize(normalTexture.Load(int3(coord, 0)).xyz);
    float centerLum = max(Luminance(centerColor), EPSILON);
    float centerDepth = length(centerPos);

    float sigmaSq = centerVar * centerLum * centerLum;
    float phiLum = constants.PhiColor * sqrt(sigmaSq) + 0.001;
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
                continue;

            int2 p = int2(coord) + int2(dx, dy) * constants.StepWidth;
            if (any(p < 0) || any(p >= int2(constants.ViewportSize)))
                continue;

            float3 samplePos = positionTexture.Load(int3(p, 0)).xyz;
            if (dot(samplePos, samplePos) < EPSILON)
                continue;

            float4 sampleData = inputTexture.Load(int3(p, 0));
            float3 sampleColor = sampleData.rgb;
            float sampleVar = max(sampleData.a, EPSILON);
            float3 sampleNormal = SafeNormalize(normalTexture.Load(int3(p, 0)).xyz);

            float wK = KERNEL[abs(dx)] * KERNEL[abs(dy)];

            float sampleLum = Luminance(sampleColor);
            float lumDiff = abs(sampleLum - centerLum);
            float wL = exp(-lumDiff / phiLum);

            float nDot = max(dot(centerNormal, sampleNormal), 0);
            float wN = pow(nDot, constants.PhiNormal);

            float planeDist = abs(dot(samplePos - centerPos, centerNormal));
            float wD = exp(-planeDist / phiDepth);

            float w = wK * wL * wN * wD;

            colorSum += sampleColor * w;
            varSum += sampleVar * w;
            wSum += w;
        }
    }

    float invW = 1.0 / wSum;
    float3 filtered = colorSum * invW;
    float filteredVar = varSum * invW;

    outputTexture[coord] = float4(filtered, filteredVar);
}
