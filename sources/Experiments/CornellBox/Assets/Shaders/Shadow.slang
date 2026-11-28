#include "Common.slang"

static const float AoRadius = 135.0;
static const uint FlagResetAll = 1u << 0;
static const uint FlagResetLight = 1u << 1;

struct ShadowConstants
{
    float4x4 InverseJitteredViewProjection;

    private float4 CameraPositionAndFrame;

    private float4 PreviousCameraPositionAndPadding;

    private uint4 RenderConfig;

    private float4 JitterDeltaAndNight;

    private float4 KeyLightDirectionAndSoftCos;

    DescriptorHandle<RaytracingAccelerationStructure> Scene;

    DescriptorHandle<Texture2D<float>> Depth;

    DescriptorHandle<Texture2D<float4>> NormalAndRoughness;

    DescriptorHandle<Texture2D<float2>> MotionVectors;

    DescriptorHandle<Texture2D<float>> PreviousLinearDepth;

    DescriptorHandle<WTexture2D<float>> CurrentLinearDepth;

    DescriptorHandle<Texture2D<float4>> PreviousHistory;

    DescriptorHandle<WTexture2D<float4>> CurrentHistory;

    DescriptorHandle<Texture2D<float4>> FilterInput;

    DescriptorHandle<WTexture2D<float4>> FilterOutput;

    DescriptorHandle<Texture2D<float2>> PreviousHistoryLength;

    DescriptorHandle<WTexture2D<float2>> CurrentHistoryLength;

    property float3 CameraPosition
    {
        get {
            return CameraPositionAndFrame.xyz;
        }
    }

    property uint FrameIndex
    {
        get {
            return asuint(CameraPositionAndFrame.w);
        }
    }

    property float3 PreviousCameraPosition
    {
        get {
            return PreviousCameraPositionAndPadding.xyz;
        }
    }

    property uint Width
    {
        get {
            return RenderConfig.x;
        }
    }

    property uint Height
    {
        get {
            return RenderConfig.y;
        }
    }

    property uint Reset
    {
        get {
            return RenderConfig.z;
        }
    }

    property uint StepSize
    {
        get {
            return RenderConfig.w;
        }
    }

    property float2 JitterDelta
    {
        get {
            return JitterDeltaAndNight.xy;
        }
    }

    property float NightLightIntensity
    {
        get {
            return JitterDeltaAndNight.z;
        }
    }

    property float3 KeyLightDirection
    {
        get {
            return KeyLightDirectionAndSoftCos.xyz;
        }
    }

    property float SoftKeyCosRadius
    {
        get {
            return KeyLightDirectionAndSoftCos.w;
        }
    }
};

ConstantBuffer<ShadowConstants> shadow;

float TraceAO(float3 origin, float3 normal, float u1, float u2, float uRange)
{
    float phi = TwoPI * u1;
    float sinTheta = sqrt(u2);
    float cosTheta = sqrt(max(0.0, 1.0 - u2));

    float3 tangent;
    float3 bitangent;
    BuildOrthonormalBasis(normal, tangent, bitangent);
    float3 dir = normalize(tangent * (cos(phi) * sinTheta) + bitangent * (sin(phi) * sinTheta) + normal * cosTheta);

    RayDesc ray;
    ray.Origin = origin;
    ray.Direction = dir;
    ray.TMin = RayMinDistance;
    ray.TMax = AoRadius * lerp(0.08, 1.0, uRange * uRange);

    RayQuery<RAY_FLAG_FORCE_OPAQUE | RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH | RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES>
        query;
    query.TraceRayInline(shadow.Scene, RAY_FLAG_NONE, RayMaskAll, ray);
    query.Proceed();

    return query.CommittedStatus() == COMMITTED_NOTHING ? 1.0 : 0.0;
}

float TraceLanternSoftVis(uint lanternIdx, float3 shadowOrigin, float u, float v)
{
    float3 toCenter = NightLanternPositions[lanternIdx] - shadowOrigin;
    float3 lightT;
    float3 lightB;
    BuildOrthonormalBasis(normalize(toCenter), lightT, lightB);
    float r = 14.5 * sqrt(u);
    float a = TwoPI * v;
    float3 sampleTarget = NightLanternPositions[lanternIdx] + (lightT * cos(a) + lightB * sin(a)) * r;
    float3 toSample = sampleTarget - shadowOrigin;
    float sampleDist = length(toSample);
    float3 sampleDir = toSample / max(sampleDist, 1e-4);

    return TraceVisibilityRay(shadow.Scene, shadowOrigin, sampleDir, sampleDist - GetLanternShadowTrim(sampleDir))
               ? 1.0
               : 0.0;
}

[shader("compute")]
[numthreads(8, 8, 1)]
void TraceShadowCS(uint3 dispatchThreadId: SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadId.xy;
    if (pixel.x >= shadow.Width || pixel.y >= shadow.Height)
    {
        return;
    }

    float deviceDepth = shadow.Depth.Load(int3(pixel, 0));
    if (deviceDepth >= 0.999999)
    {
        shadow.CurrentLinearDepth.Store(pixel, 65504.0);
        shadow.CurrentHistory.Store(pixel, float4(1.0, 1.0, 1.0, 1.0));
        shadow.CurrentHistoryLength.Store(pixel, float2(0.0, 0.0));

        return;
    }

    float2 renderSize = float2(shadow.Width, shadow.Height);
    float2 uv = (float2(pixel) + 0.5) / renderSize;
    float3 worldPos = ReconstructWorldPosition(uv, deviceDepth, shadow.InverseJitteredViewProjection);
    float linearDepth = length(worldPos - shadow.CameraPosition);

    float4 normalRough = shadow.NormalAndRoughness.Load(int3(pixel, 0));
    float3 normal = normalize(normalRough.xyz);
    bool isDynamic = normalRough.w < 0.0;

    float2 p = float2(pixel);
    float2 warpCoord = p * 0.024;
    float warp0 = ValueNoise2D(warpCoord + float2(11.3, 7.1)) * 1.15;
    float warp1 = ValueNoise2D(warpCoord + float2(53.7, 29.9)) * 1.15;
    float warp2 = ValueNoise2D(warpCoord + float2(97.1, 61.3)) * 1.15;
    float frame = float(shadow.FrameIndex & 1023u);
    float u = frac(0.7548776662 * p.x + 0.5698402910 * p.y + warp0 + 0.6180339887 * frame);
    float v = frac(-0.5698402910 * p.x + 0.7548776662 * p.y + warp1 + 0.4142135624 * frame);
    float uRange = frac(0.6180339887 * p.x + 0.3819660113 * p.y + warp2 + 0.7320508076 * frame);

    float bias = max(0.05, linearDepth * 0.0003 + (linearDepth * linearDepth) * 1.5e-7);
    float3 shadowOrigin = worldPos + normal * bias;

    float sunVis = 0.0;
    float3 keyDir = shadow.KeyLightDirection;
    if (dot(normal, keyDir) > 0.0)
    {
        float3 sunT;
        float3 sunB;
        BuildOrthonormalBasis(keyDir, sunT, sunB);
        float cosTheta = lerp(1.0, shadow.SoftKeyCosRadius, u);
        float sinTheta = sqrt(max(0.0, 1.0 - cosTheta * cosTheta));
        float phi = TwoPI * v;
        float3 sunDir = normalize(keyDir * cosTheta + (sunT * cos(phi) + sunB * sin(phi)) * sinTheta);
        sunVis =
            TraceVisibilityRay(shadow.Scene, shadowOrigin + keyDir * (bias * 0.35), sunDir, RayMaxDistance) ? 1.0 : 0.0;
    }

    float areaVis = 0.0;
    if (IsCeilingLightPotentiallyVisible(worldPos))
    {
        float3 lightSample = CeilingLightCenter + float3((u * 2.0 - 1.0) * CeilingLightHalfSize.x, 0.0,
                                                         (v * 2.0 - 1.0) * CeilingLightHalfSize.y);
        float3 toLight = lightSample - worldPos;
        float dist = length(toLight);
        float3 dirToLight = toLight / max(dist, 1e-4);
        if (dot(normal, dirToLight) > -0.15)
        {
            areaVis = TraceVisibilityRay(shadow.Scene, shadowOrigin, dirToLight, dist - 1.5) ? 1.0 : 0.0;
        }
    }

    float ao = TraceAO(shadowOrigin, normal, frac(u + 0.5), frac(v + 0.5), uRange);

    float nightVis = 1.0;
    if (shadow.NightLightIntensity > 0.001)
    {
        float scores[NightLanternCount];
        int bestIdx = -1;
        float bestScore = 0.0;
        float totalScore = 0.0;
        for (uint i = 0u; i < NightLanternCount; i++)
        {
            float3 L;
            float dist;
            float att = ComputeLanternAttenuation(worldPos, NightLanternPositions[i], L, dist);
            float s = att > 0.0 ? att * saturate(dot(normal, L)) : 0.0;
            scores[i] = s;
            totalScore += s;
            if (s > bestScore)
            {
                bestScore = s;
                bestIdx = int(i);
            }
        }

        if (bestIdx >= 0 && totalScore > 1e-9)
        {
            float visBest = TraceLanternSoftVis(uint(bestIdx), shadowOrigin, u, v);
            float restScore = max(totalScore - bestScore, 0.0);
            if (restScore > bestScore * 0.01)
            {
                float threshold = uRange * restScore;
                float accum = 0.0;
                uint secondIdx = uint(bestIdx);
                for (uint i = 0u; i < NightLanternCount; i++)
                {
                    if (int(i) == bestIdx || scores[i] <= 0.0)
                    {
                        continue;
                    }

                    accum += scores[i];
                    secondIdx = i;
                    if (accum >= threshold)
                    {
                        break;
                    }
                }

                float visRest = TraceLanternSoftVis(secondIdx, shadowOrigin, frac(u + 0.37), frac(v + 0.61));
                nightVis = (bestScore * visBest + restScore * visRest) / totalScore;
            }
            else
            {
                nightVis = visBest;
            }
        }
    }

    float4 currentSignal = float4(sunVis, areaVis, ao, nightVis);

    float2 motionVector = shadow.MotionVectors.Load(int3(pixel, 0));
    float2 mvPixels = motionVector * renderSize;
    bool movingDynamic = isDynamic && dot(mvPixels, mvPixels) > 0.0025;

    float4 accumSignal = currentSignal;
    float geoHistoryLen = 1.0;
    float lightHistoryLen = 1.0;

    if ((shadow.Reset & FlagResetAll) == 0u)
    {
        float2 prevUv = uv + motionVector + shadow.JitterDelta;
        if (all(prevUv >= 0.0) && all(prevUv <= 1.0))
        {
            float2 prevTexel = prevUv * renderSize - 0.5;
            int2 basePixel = int2(floor(prevTexel));
            float2 fracCoord = frac(prevTexel);
            int2 maxPixel = int2(shadow.Width - 1u, shadow.Height - 1u);

            float4 sumHist = 0.0;
            float2 sumHistLen = 0.0;
            float weightSum = 0.0;
            float3 viewDir = (shadow.CameraPosition - worldPos) / max(linearDepth, 1e-4);
            float slopeFactor = clamp(1.0 / max(abs(dot(normal, viewDir)), 0.16), 1.0, 5.0);
            float expectedPrevDepth = length(worldPos - shadow.PreviousCameraPosition);
            float depthThreshold = max(movingDynamic ? 6.5 : 2.5, expectedPrevDepth * 0.015 * slopeFactor);

            for (int dy = 0; dy <= 1; dy++)
            {
                for (int dx = 0; dx <= 1; dx++)
                {
                    int2 p = clamp(basePixel + int2(dx, dy), int2(0, 0), maxPixel);
                    float rawPrevD = shadow.PreviousLinearDepth.Load(int3(p, 0));
                    float prevD = abs(rawPrevD);
                    bool prevDyn = rawPrevD < 0.0;
                    if (prevD < 60000.0 && prevDyn == movingDynamic && abs(prevD - expectedPrevDepth) < depthThreshold)
                    {
                        float bilinear =
                            (dx == 0 ? 1.0 - fracCoord.x : fracCoord.x) * (dy == 0 ? 1.0 - fracCoord.y : fracCoord.y);
                        sumHist += shadow.PreviousHistory.Load(int3(p, 0)) * bilinear;
                        sumHistLen += shadow.PreviousHistoryLength.Load(int3(p, 0)) * bilinear;
                        weightSum += bilinear;
                    }
                }
            }

            if (weightSum > 0.05)
            {
                float4 prevHist = sumHist / weightSum;
                float2 prevHistLen = sumHistLen / weightSum;
                float maxHist = movingDynamic ? 6.0 : lerp(32.0, 18.0, saturate(length(mvPixels) * 0.12));
                geoHistoryLen = min(prevHistLen.x + 1.0, maxHist);

                bool resetLight = (shadow.Reset & FlagResetLight) != 0u;
                lightHistoryLen = resetLight ? 1.0 : min(prevHistLen.y + 1.0, maxHist);

                accumSignal.gb = lerp(prevHist.gb, currentSignal.gb, 1.0 / geoHistoryLen);
                accumSignal.r = resetLight ? currentSignal.r : lerp(prevHist.r, currentSignal.r, 1.0 / lightHistoryLen);
                accumSignal.a = resetLight ? currentSignal.a : lerp(prevHist.a, currentSignal.a, 1.0 / lightHistoryLen);
            }
        }
    }

    shadow.CurrentLinearDepth.Store(pixel, movingDynamic ? -linearDepth : linearDepth);
    shadow.CurrentHistory.Store(pixel, accumSignal);
    shadow.CurrentHistoryLength.Store(pixel, float2(geoHistoryLen, lightHistoryLen));
}

[shader("compute")]
[numthreads(8, 8, 1)]
void FilterShadowCS(uint3 dispatchThreadId: SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadId.xy;
    if (pixel.x >= shadow.Width || pixel.y >= shadow.Height)
    {
        return;
    }

    float centerDepth = abs(shadow.PreviousLinearDepth.Load(int3(pixel, 0)));
    if (centerDepth >= 60000.0)
    {
        shadow.FilterOutput.Store(pixel, float4(1.0, 1.0, 1.0, 1.0));

        return;
    }

    float3 centerNormal = normalize(shadow.NormalAndRoughness.Load(int3(pixel, 0)).xyz);
    float4 centerValue = shadow.FilterInput.Load(int3(pixel, 0));
    float2 histLens = shadow.PreviousHistoryLength.Load(int3(pixel, 0));
    float geoHist = histLens.x;
    float lightHist = histLens.y;

    float2 uv = (float2(pixel) + 0.5) / float2(shadow.Width, shadow.Height);
    float3 rayDir =
        normalize(ReconstructWorldPosition(uv, 0.5, shadow.InverseJitteredViewProjection) - shadow.CameraPosition);
    float slopeFactor = clamp(1.0 / max(abs(dot(centerNormal, rayDir)), 0.16), 1.0, 5.0);

    int step = int(shadow.StepSize);
    float step4WeightGeo = step == 4 ? (1.0 - smoothstep(4.0, 12.0, geoHist)) : 1.0;
    float step4WeightLight = step == 4 ? (1.0 - smoothstep(4.0, 12.0, lightHist)) : 1.0;
    if (step4WeightGeo <= 0.0 && step4WeightLight <= 0.0)
    {
        shadow.FilterOutput.Store(pixel, centerValue);

        return;
    }

    int2 maxPixel = int2(shadow.Width - 1u, shadow.Height - 1u);
    float baseDepthScale = centerDepth * 0.010 * slopeFactor * float(step);
    float geoDepthScale = max(baseDepthScale * (geoHist < 6.0 ? 2.2 : 1.0), 0.75);
    float lightDepthScale = max(baseDepthScale * (lightHist < 6.0 ? 2.5 : 1.0), 0.75);

    float2 sumGeo = centerValue.gb;
    float sumWeightGeo = 1.0;
    float2 sumLight = centerValue.ra;
    float sumWeightLight = 1.0;

    static const float Kernel[3] = { 1.0, 0.5, 0.25 };
    for (int dy = -1; dy <= 1; dy++)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dy == 0)
            {
                continue;
            }

            int2 samplePixel = clamp(int2(pixel) + int2(dx, dy) * step, int2(0, 0), maxPixel);
            float sampleDepth = abs(shadow.PreviousLinearDepth.Load(int3(samplePixel, 0)));
            if (sampleDepth >= 60000.0)
            {
                continue;
            }

            float3 sampleNormal = normalize(shadow.NormalAndRoughness.Load(int3(samplePixel, 0)).xyz);
            float depthDiff = abs(sampleDepth - centerDepth);
            float nd = saturate(dot(centerNormal, sampleNormal));
            float nd2 = nd * nd;
            float nd4 = nd2 * nd2;
            float nd8 = nd4 * nd4;
            float nd16 = nd8 * nd8;
            float wKernel = Kernel[abs(dx)] * Kernel[abs(dy)];
            float4 sampleValue = shadow.FilterInput.Load(int3(samplePixel, 0));

            float wNormalGeo = geoHist < 6.0 ? (nd8 * nd4) : (nd16 * nd16);
            float wGeo = wKernel * exp(-depthDiff / geoDepthScale) * wNormalGeo * step4WeightGeo;
            if (wGeo > 1e-4)
            {
                sumGeo += sampleValue.gb * wGeo;
                sumWeightGeo += wGeo;
            }

            float wNormalLight = lightHist < 6.0 ? (nd4 * nd2) : (nd16 * nd16);
            float wLight = wKernel * exp(-depthDiff / lightDepthScale) * wNormalLight * step4WeightLight;
            if (wLight > 1e-4)
            {
                sumLight += sampleValue.ra * wLight;
                sumWeightLight += wLight;
            }
        }
    }

    float2 outGeo = sumGeo / sumWeightGeo;
    float2 outLight = sumLight / sumWeightLight;
    shadow.FilterOutput.Store(pixel, float4(outLight.x, outGeo.x, outGeo.y, outLight.y));
}
