#include "Common.slang"

static const uint RayCountPerProbe = 64u;
static const float GoldenRatio = 1.61803398875;

struct GiProbeConstants
{
    private uint4 Config;

    SceneEnvironment Environment;

    DescriptorHandle<RaytracingAccelerationStructure> Scene;

    DescriptorHandle<StructuredBuffer<SceneVertex>> Vertices;

    DescriptorHandle<StructuredBuffer<uint>> Indices;

    DescriptorHandle<StructuredBuffer<SceneMaterial>> Materials;

    DescriptorHandle<StructuredBuffer<SceneInstance>> Instances;

    DescriptorHandle<Texture2D<float4>> PrevIrradianceAtlas;

    DescriptorHandle<Texture2D<float2>> PrevDistanceAtlas;

    DescriptorHandle<WTexture2D<float4>> CurrIrradianceAtlas;

    DescriptorHandle<WTexture2D<float2>> CurrDistanceAtlas;

    DescriptorHandle<SamplerState> LinearSampler;

    property uint FrameIndex
    {
        get
        {
            return Config.x;
        }
    }

    property float Hysteresis
    {
        get
        {
            return asfloat(Config.y);
        }
    }

    property uint ProbeStride
    {
        get
        {
            return Config.z;
        }
    }

    property uint ProbePhase
    {
        get
        {
            return Config.w >> 1u;
        }
    }

    property uint Reset
    {
        get
        {
            return Config.w & 1u;
        }
    }
};

ConstantBuffer<GiProbeConstants> giProbe;

groupshared float3 sharedRayDir[RayCountPerProbe];
groupshared float3 sharedRayRadiance[RayCountPerProbe];
groupshared float sharedRayDistance[RayCountPerProbe];
groupshared uint sharedBackfaceCount;
groupshared float3 sharedBasisTangent;
groupshared float3 sharedBasisBitangent;
groupshared float3 sharedBasisAxis;
groupshared float3 sharedIrrTile[6][6];
groupshared float2 sharedDistTile[14][14];

float3 GetProbeWorldPosition(uint probeIndex)
{
    if (probeIndex < DDGICascade0Probes)
    {
        int x = int(probeIndex % uint(DDGICascade0Counts.x));
        int y = int((probeIndex / uint(DDGICascade0Counts.x)) % uint(DDGICascade0Counts.y));
        int z = int(probeIndex / uint(DDGICascade0Counts.x * DDGICascade0Counts.y));

        return DDGICascade0Origin + float3(x, y, z) * DDGICascade0Spacing;
    }

    uint idx = probeIndex - DDGICascade0Probes;
    int x = int(idx % uint(DDGICascade1Counts.x));
    int y = int((idx / uint(DDGICascade1Counts.x)) % uint(DDGICascade1Counts.y));
    int z = int(idx / uint(DDGICascade1Counts.x * DDGICascade1Counts.y));

    return DDGICascade1Origin + float3(x, y, z) * DDGICascade1Spacing;
}

void ComputeProbeRotationBasis(uint frameIndex, uint probeIndex, out float3 tangent, out float3 bitangent, out float3 axis)
{
    uint seed = PCGHash(probeIndex * 1973u + frameIndex * 9277u + 26699u);
    float u1 = float(seed & 0xFFFFu) * (1.0 / 65535.0);
    float u2 = float((seed >> 16u) & 0xFFFFu) * (1.0 / 65535.0);
    seed = PCGHash(seed);
    float u3 = float(seed & 0xFFFFu) * (1.0 / 65535.0);

    float cosA = cos(TwoPI * u1);
    float sinA = sin(TwoPI * u1);
    float cosB = cos(TwoPI * u2);
    float sinB = sin(TwoPI * u2);
    float cosC = 1.0 - 2.0 * u3;
    float sinC = sqrt(max(0.0, 1.0 - cosC * cosC));

    tangent = float3(cosA * cosB - sinA * sinB * cosC, sinA * cosB + cosA * sinB * cosC, sinB * sinC);
    bitangent = float3(-cosA * sinB - sinA * cosB * cosC, -sinA * sinB + cosA * cosB * cosC, cosB * sinC);
    axis = float3(sinA * sinC, -cosA * sinC, cosC);
}

float3 SphericalFibonacciDirection(uint rayIndex, uint rayCount)
{
    float fracIndex = (float(rayIndex) + 0.5) / float(rayCount);
    float z = 1.0 - 2.0 * fracIndex;
    float r = sqrt(max(0.0, 1.0 - z * z));
    float phi = TwoPI * frac(float(rayIndex) * GoldenRatio);

    return float3(r * cos(phi), r * sin(phi), z);
}

[shader("compute")]
[numthreads(64, 1, 1)]
void UpdateProbesCS(uint3 groupId: SV_GroupID, uint3 localId: SV_GroupThreadID)
{
    uint probeIndex = groupId.x * giProbe.ProbeStride + giProbe.ProbePhase;
    if (probeIndex >= DDGITotalProbes)
    {
        return;
    }

    uint tid = localId.x;
    if (tid == 0u)
    {
        float3 basisTangent;
        float3 basisBitangent;
        float3 basisAxis;
        ComputeProbeRotationBasis(giProbe.FrameIndex, probeIndex, basisTangent, basisBitangent, basisAxis);

        sharedBackfaceCount = 0u;
        sharedBasisTangent = basisTangent;
        sharedBasisBitangent = basisBitangent;
        sharedBasisAxis = basisAxis;
    }
    GroupMemoryBarrierWithGroupSync();

    float3 probePos = GetProbeWorldPosition(probeIndex);
    float3 baseDir = SphericalFibonacciDirection(tid, RayCountPerProbe);
    float3 rayDir = normalize(sharedBasisTangent * baseDir.x + sharedBasisBitangent * baseDir.y + sharedBasisAxis * baseDir.z);
    float maxDistance = (probeIndex < DDGICascade0Probes) ? 140.0 : 340.0;

    uint instanceIndex;
    uint primitiveIndex;
    float2 barycentrics;
    float hitDistance;

    float3 rayRadiance = 0.0;
    float effectiveDistance = maxDistance;

    if (!TraceClosestHit(giProbe.Scene, probePos, rayDir, RayMaskOpaque, instanceIndex, primitiveIndex, barycentrics, hitDistance))
    {
        rayRadiance = EvaluateSkyAmbient(rayDir, giProbe.Environment);
    }
    else
    {
        GeometryHit hit = ReconstructGeometryHit(instanceIndex, primitiveIndex, barycentrics,
                                                 giProbe.Vertices, giProbe.Indices, giProbe.Instances);
        if (dot(hit.GeometricNormal, rayDir) > 0.0)
        {
            InterlockedAdd(sharedBackfaceCount, 1u);
            effectiveDistance = -0.2 * min(hitDistance, maxDistance);
        }
        else
        {
            effectiveDistance = min(hitDistance, maxDistance);

            SceneMaterial material = giProbe.Materials[hit.MaterialId];
            float3 albedo = material.Albedo;
            float roughness = max(material.Roughness, MinimumRoughness);
            EvaluateProceduralPattern(material, hit.ObjectPosition, hit.LocalNormal, 0.0, albedo, roughness);

            float3 hitNormal = dot(hit.Normal, hit.GeometricNormal) < 0.0 ? -hit.Normal : hit.Normal;
            float3 keyDir = giProbe.Environment.KeyLightDirection;
            float3 shadowOrigin = OffsetRayOrigin(hit.Position, hit.GeometricNormal, keyDir);

            float3 directSun = 0.0;
            float sunNdotL = saturate(dot(hitNormal, keyDir));
            if (sunNdotL > 0.0 && TraceVisibilityRay(giProbe.Scene, shadowOrigin, keyDir, RayMaxDistance))
            {
                directSun = albedo * (1.0 - saturate(material.Metallic)) * InvPI * sunNdotL * giProbe.Environment.KeyLightIrradiance;
            }

            float3 directCeiling = 0.0;
            if (hit.Position.y < CeilingLightCenter.y - 0.5 && abs(hit.Position.x) < 420.0 && hit.Position.z > -150.0 && hit.Position.z < 600.0)
            {
                float3 ceilIrradiance = EvaluateClippedQuadIrradiance(giProbe.Environment, hit.Position, hitNormal);
                if (dot(ceilIrradiance, ceilIrradiance) > 1e-6)
                {
                    float3 toCeiling = CeilingLightCenter - hit.Position;
                    float ceilingDist = length(toCeiling);
                    float3 toCeilingDir = toCeiling / max(ceilingDist, 1e-4);
                    float3 ceilShadowOrigin = OffsetRayOrigin(hit.Position, hit.GeometricNormal, toCeilingDir);
                    if (TraceVisibilityRay(giProbe.Scene, ceilShadowOrigin, toCeilingDir, ceilingDist - 1.5))
                    {
                        directCeiling = albedo * (1.0 - saturate(material.Metallic)) * ceilIrradiance;
                    }
                }
            }

            float3 directNight = EvaluateNightLightsSecondary(giProbe.Environment, hit.Position, hitNormal,
                                                              albedo * (1.0 - saturate(material.Metallic)));

            float3 bounceGI = 0.0;
            if (giProbe.Reset == 0u)
            {
                float3 indirect = SampleDDGIIrradiance(hit.Position, hitNormal, -rayDir,
                                                       giProbe.PrevIrradianceAtlas, giProbe.PrevDistanceAtlas,
                                                       giProbe.LinearSampler);
                bounceGI = albedo * (1.0 - saturate(material.Metallic)) * indirect;
            }

            bool isAnalyticalLight = hit.MaterialId == CeilingLightMaterialId
                                  || hit.MaterialId == LanternCoreMaterialId
                                  || hit.MaterialId == RecessedStripMaterialId;
            float3 emission = isAnalyticalLight ? 0.0 : EvaluateMaterialEmission(hit.MaterialId, material, giProbe.Environment);
            rayRadiance = min(directSun + directCeiling + directNight + bounceGI + emission, float3(24.0));
        }
    }

    sharedRayDir[tid] = rayDir;
    sharedRayRadiance[tid] = rayRadiance;
    sharedRayDistance[tid] = effectiveDistance;
    GroupMemoryBarrierWithGroupSync();

    uint2 tile = uint2(probeIndex % DDGIAtlasColumns, probeIndex / DDGIAtlasColumns);
    float currValidity = 1.0 - smoothstep(10.0, 24.0, float(sharedBackfaceCount));

    int2 localIrr = int2(tid & 7u, tid >> 3u);
    int2 innerIrr = localIrr - 1;
    if (innerIrr.x >= 0 && innerIrr.x < 6 && innerIrr.y >= 0 && innerIrr.y < 6)
    {
        float2 oct = ((float2(innerIrr) + 0.5) / 6.0) * 2.0 - 1.0;
        float3 texelDir = OctDecode(oct);

        float3 irrSum = 0.0;
        float wSum = 0.0;
        for (uint r = 0u; r < RayCountPerProbe; r++)
        {
            float w = max(0.0, dot(sharedRayDir[r], texelDir));
            if (w > 1e-4)
            {
                irrSum += sharedRayRadiance[r] * w;
                wSum += w;
            }
        }
        sharedIrrTile[innerIrr.y][innerIrr.x] = wSum > 1e-4 ? (irrSum / wSum) : 0.0;
    }
    GroupMemoryBarrierWithGroupSync();

    int2 wrappedIrr = OctBorderToInterior(localIrr, 6);
    float3 currIrr = sharedIrrTile[wrappedIrr.y][wrappedIrr.x];
    uint2 irrPixel = tile * 8u + uint2(localIrr);
    float3 blendedIrr = currIrr;
    float blendedValidity = currValidity;
    if (giProbe.Hysteresis > 0.0)
    {
        float4 prevIrr = giProbe.PrevIrradianceAtlas.Load(int3(irrPixel, 0));
        blendedIrr = lerp(currIrr, prevIrr.rgb, giProbe.Hysteresis);
        blendedValidity = lerp(currValidity, prevIrr.w, giProbe.Hysteresis);
    }
    giProbe.CurrIrradianceAtlas.Store(irrPixel, float4(blendedIrr, blendedValidity));

    for (uint i = tid; i < 196u; i += 64u)
    {
        int2 innerDist = int2(i % 14u, i / 14u);
        float2 oct = ((float2(innerDist) + 0.5) / 14.0) * 2.0 - 1.0;
        float3 texelDir = OctDecode(oct);

        float2 distSum = 0.0;
        float wSum = 0.0;
        for (uint r = 0u; r < RayCountPerProbe; r++)
        {
            float cosW = max(0.0, dot(sharedRayDir[r], texelDir));
            if (cosW > 0.76)
            {
                float c2 = cosW * cosW;
                float c4 = c2 * c2;
                float c8 = c4 * c4;
                float c16 = c8 * c8;
                float w = c16 * c16 * c16;
                if (w > 1e-5)
                {
                    float d = sharedRayDistance[r];
                    distSum += float2(d, d * abs(d)) * w;
                    wSum += w;
                }
            }
        }
        sharedDistTile[innerDist.y][innerDist.x] = wSum > 1e-5 ? (distSum / wSum) : float2(maxDistance, maxDistance * maxDistance);
    }
    GroupMemoryBarrierWithGroupSync();

    for (uint j = tid; j < 256u; j += 64u)
    {
        int2 localDist = int2(j & 15u, j >> 4u);
        int2 wrappedDist = OctBorderToInterior(localDist, 14);
        float2 currDist = sharedDistTile[wrappedDist.y][wrappedDist.x];
        uint2 distPixel = tile * 16u + uint2(localDist);
        float2 blendedDist = currDist;
        if (giProbe.Hysteresis > 0.0)
        {
            float2 prevDist = giProbe.PrevDistanceAtlas.Load(int3(distPixel, 0)).xy;
            blendedDist = lerp(currDist, prevDist, giProbe.Hysteresis);
        }
        giProbe.CurrDistanceAtlas.Store(distPixel, blendedDist);
    }
}
