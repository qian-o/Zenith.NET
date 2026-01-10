static const float EPSILON = 1e-4;

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
RWTexture2D<float4> accumulatedTexture;
RWTexture2D<float4> momentsTexture;
RWTexture2D<float4> outputTexture;
SamplerState linearSampler;

float Luminance(float3 c)
{
    return dot(c, float3(0.2126, 0.7152, 0.0722));
}

float3 SafeNormalize(float3 v)
{
    float lenSq = dot(v, v);

    return lenSq > EPSILON ? v * rsqrt(lenSq) : float3(0, 0, 1);
}

bool Reproject(float3 pos, float3 normal, out int2 historyCoord)
{
    float4 clip = mul(float4(pos, 1), constants.PrevViewProjection);

    if (clip.w <= 0)
    {
        historyCoord = int2(-1);

        return false;
    }

    float2 ndc = clip.xy / clip.w;
    float2 uv = float2(ndc.x * 0.5 + 0.5, -ndc.y * 0.5 + 0.5);

    if (any(uv < 0) || any(uv > 1))
    {
        historyCoord = int2(-1);

        return false;
    }

    historyCoord = int2(uv * constants.ViewportSize);

    float3 historyPos = historyPositionTexture.SampleLevel(linearSampler, uv, 0).xyz;

    if (dot(historyPos, historyPos) < EPSILON)
    {
        return false;
    }

    float3 n = SafeNormalize(normal);

    if (abs(dot(pos - historyPos, n)) > length(pos) * constants.DepthThreshold)
    {
        return false;
    }

    float3 historyNormal = historyNormalTexture.SampleLevel(linearSampler, uv, 0).xyz;

    if (dot(n, SafeNormalize(historyNormal)) < constants.NormalThreshold)
    {
        return false;
    }

    return true;
}

void ComputeColorBounds(uint2 coord, out float3 boxMin, out float3 boxMax, out float variance)
{
    float3 m1 = 0, m2 = 0;
    int count = 0;

    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            int2 p = int2(coord) + int2(x, y);

            if (all(p >= 0) && all(p < int2(constants.ViewportSize)))
            {
                float3 c = colorTexture.Load(int3(p, 0)).rgb;

                m1 += c;
                m2 += c * c;
                count++;
            }
        }
    }

    float inv = 1.0 / count;
    float3 mean = m1 * inv;
    float3 var = max(m2 * inv - mean * mean, 0);
    float3 sigma = sqrt(var) * constants.ColorBoxSigma;

    boxMin = mean - sigma;
    boxMax = mean + sigma;
    variance = Luminance(var);
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
        accumulatedTexture[coord] = 0;
        momentsTexture[coord] = 0;
        outputTexture[coord] = 0;

        return;
    }

    float3 normal = normalTexture.Load(int3(coord, 0)).xyz;
    float3 color = colorTexture.Load(int3(coord, 0)).rgb;

    float3 boxMin, boxMax;
    float spatialVar;
    ComputeColorBounds(coord, boxMin, boxMax, spatialVar);

    int2 historyCoord;
    bool valid = Reproject(pos, normal, historyCoord);

    float3 accumulated;
    float2 moments;
    float histLen;

    if (valid)
    {
        float3 histColor = clamp(accumulatedTexture[historyCoord].rgb, boxMin, boxMax);
        float4 histMoments = momentsTexture[historyCoord];

        histLen = min(histMoments.w + 1, constants.MaxHistoryLength);

        float alpha = 1.0 / histLen;
        accumulated = lerp(histColor, color, alpha);

        float lum = Luminance(color);
        moments.x = lerp(histMoments.x, lum, alpha);
        moments.y = lerp(histMoments.y, lum * lum, alpha);
    }
    else
    {
        accumulated = color;
        histLen = 1;

        float lum = Luminance(color);
        moments = float2(lum, lum * lum);
    }

    float temporalVar = max(moments.y - moments.x * moments.x, 0);
    float t = saturate(histLen * 0.25);
    float variance = max(lerp(spatialVar, temporalVar, t), EPSILON);

    accumulatedTexture[coord] = float4(accumulated, 1);
    momentsTexture[coord] = float4(moments, 0, histLen);
    outputTexture[coord] = float4(accumulated, variance);
}
