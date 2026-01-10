static const float EPSILON = 1e-6;

struct TemporalConstants
{
    float4x4 PrevViewProjection;

    float2 ViewportSize;

    float ColorBoxSigma;

    float NormalThreshold;

    float DepthThreshold;

    int MaxHistoryLength;
};

ConstantBuffer<TemporalConstants> constants;
Texture2D colorTexture;
Texture2D positionTexture;
Texture2D normalTexture;
Texture2D historyPositionTexture;
Texture2D historyNormalTexture;
Texture2D historyAccumulatedTexture;
Texture2D historyMomentsTexture;
RWTexture2D accumulatedTexture;
RWTexture2D momentsTexture;
RWTexture2D outputTexture;
SamplerState linearSampler;

float Luminance(float3 c)
{
    return dot(c, float3(0.2126, 0.7152, 0.0722));
}

float3 SafeNormalize(float3 v)
{
    float lenSq = dot(v, v);

    return lenSq > EPSILON ? v * rsqrt(lenSq) : float3(0.0, 0.0, 1.0);
}

bool Reproject(float3 pos, float3 normal, out int2 histCoord)
{
    float4 clip = mul(float4(pos, 1.0), constants.PrevViewProjection);

    if (clip.w <= 0.0)
    {
        histCoord = int2(-1, -1);

        return false;
    }

    float2 ndc = clip.xy / clip.w;
    float2 uv = float2(ndc.x * 0.5 + 0.5, -ndc.y * 0.5 + 0.5);

    if (any(uv < 0.0) || any(uv > 1.0))
    {
        histCoord = int2(-1, -1);

        return false;
    }

    histCoord = int2(uv * constants.ViewportSize);

    float3 histPos = historyPositionTexture.SampleLevel(linearSampler, uv, 0).xyz;

    if (dot(histPos, histPos) < EPSILON)
    {
        return false;
    }

    float3 n = SafeNormalize(normal);
    float depth = length(pos);
    float planeDist = abs(dot(pos - histPos, n));

    if (planeDist > depth * constants.DepthThreshold)
    {
        return false;
    }

    float3 histNormal = historyNormalTexture.SampleLevel(linearSampler, uv, 0).xyz;

    if (dot(n, SafeNormalize(histNormal)) < constants.NormalThreshold)
    {
        return false;
    }

    return true;
}

void ComputeNeighborhood(uint2 coord, out float3 mean, out float3 stdDev, out float variance)
{
    float3 m1 = float3(0.0);
    float3 m2 = float3(0.0);

    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            int2 p = clamp(int2(coord) + int2(x, y), int2(0), int2(constants.ViewportSize) - 1);
            float3 c = colorTexture.Load(int3(p, 0)).rgb;

            m1 += c;
            m2 += c * c;
        }
    }

    m1 /= 9.0;
    m2 /= 9.0;

    float3 var = max(m2 - m1 * m1, 0.0);
    mean = m1;
    stdDev = sqrt(var + EPSILON);

    float meanLum = max(Luminance(mean), EPSILON);
    float varLum = Luminance(var);

    variance = varLum / (meanLum * meanLum) + EPSILON;
}

[numthreads(16, 16, 1)]
void CSMain(uint3 id: SV_DispatchThreadID)
{
    uint2 coord = id.xy;

    if (any(coord >= uint2(constants.ViewportSize)))
    {
        return;
    }

    float3 pos = positionTexture.Load(int3(coord, 0)).xyz;

    if (dot(pos, pos) < EPSILON)
    {
        accumulatedTexture[coord] = float4(0.0);
        momentsTexture[coord] = float4(0.0);
        outputTexture[coord] = float4(0.0);

        return;
    }

    float3 normal = normalTexture.Load(int3(coord, 0)).xyz;
    float3 color = colorTexture.Load(int3(coord, 0)).rgb;

    float3 mean, stdDev;
    float spatialVar;
    ComputeNeighborhood(coord, mean, stdDev, spatialVar);

    float3 boxMin = mean - stdDev * constants.ColorBoxSigma;
    float3 boxMax = mean + stdDev * constants.ColorBoxSigma;

    int2 histCoord;
    bool valid = Reproject(pos, normal, histCoord);

    float3 accumulated;
    float2 moments;
    float histLen;

    if (valid)
    {
        float3 histColor = historyAccumulatedTexture.Load(int3(histCoord, 0)).rgb;
        float4 histMom = historyMomentsTexture.Load(int3(histCoord, 0));

        histColor = clamp(histColor, boxMin, boxMax);
        histLen = min(histMom.w + 1.0, float(constants.MaxHistoryLength));

        float alpha = 1.0 / histLen;
        accumulated = lerp(histColor, color, alpha);

        float lum = Luminance(color);
        moments.x = lerp(histMom.x, lum, alpha);
        moments.y = lerp(histMom.y, lum * lum, alpha);
    }
    else
    {
        accumulated = color;
        histLen = 1.0;

        float lum = Luminance(color);
        moments = float2(lum, lum * lum);
    }

    float temporalVar = max(moments.y - moments.x * moments.x, 0.0);
    float meanLum = max(moments.x, EPSILON);
    float relativeVar = temporalVar / (meanLum * meanLum);

    float variance;

    if (histLen < 4.0)
    {
        float t = histLen / 4.0;
        variance = lerp(spatialVar * 4.0, relativeVar, t * t);
    }
    else
    {
        variance = relativeVar;
    }

    variance = max(variance, 0.01);

    accumulatedTexture[coord] = float4(accumulated, 1.0);
    momentsTexture[coord] = float4(moments, 0.0, histLen);
    outputTexture[coord] = float4(accumulated, variance);
}
