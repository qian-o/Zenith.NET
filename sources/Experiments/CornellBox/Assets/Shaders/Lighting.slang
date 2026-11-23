#include "Common.slang"

static const uint FlagReset = 1u;
static const uint FlagHasReactiveMask = 2u;

struct LightingConstants
{
    float4x4 InverseJitteredViewProjection;

    float4x4 InverseJitteredProjection;

    float4x4 InverseView;

    float4x4 ViewProjection;

    float4x4 PreviousViewProjection;

    private float4 CameraPositionAndFlags;

    private float4 RenderConfig;

    SceneEnvironment Environment;

    DescriptorHandle<RaytracingAccelerationStructure> Scene;

    DescriptorHandle<StructuredBuffer<SceneVertex>> Vertices;

    DescriptorHandle<StructuredBuffer<uint>> Indices;

    DescriptorHandle<StructuredBuffer<SceneMaterial>> Materials;

    DescriptorHandle<StructuredBuffer<SceneInstance>> Instances;

    DescriptorHandle<Texture2D<float4>> AlbedoAndMaterial;

    DescriptorHandle<Texture2D<float4>> NormalAndRoughness;

    DescriptorHandle<Texture2D<float>> Depth;

    DescriptorHandle<Texture2D<float2>> MotionVectors;

    DescriptorHandle<Texture2D<float4>> ShadowAo;

    DescriptorHandle<Texture2D<float4>> IrradianceAtlas;

    DescriptorHandle<Texture2D<float2>> DistanceAtlas;

    DescriptorHandle<SamplerState> LinearSampler;

    DescriptorHandle<Texture2D<float>> PrevLinearDepth;

    DescriptorHandle<WTexture2D<float4>> Color;

    DescriptorHandle<WTexture2D<float>> ReactiveMask;

    property float3 CameraPosition
    {
        get
        {
            return CameraPositionAndFlags.xyz;
        }
    }

    property uint Flags
    {
        get
        {
            return asuint(CameraPositionAndFlags.w);
        }
    }

    property float2 RenderSize
    {
        get
        {
            return RenderConfig.xy;
        }
    }
};

ConstantBuffer<LightingConstants> lighting;

float3 ShadeOpaqueSurface(uint materialId, float3 position, float3 shadingNormal, float3 viewDir,
                          float3 albedo, float roughness, SceneMaterial material,
                          float sunVis, float areaVis, float ao, float nightVis, bool isPrimary, out float3 outDdgiIrradiance)
{
    float metallic = saturate(material.Metallic);
    float clearcoat = saturate(material.Clearcoat);
    float clearcoatRoughness = max(material.ClearcoatRoughness, MinimumRoughness);

    float3 sunDirect = sunVis > 0.0
        ? EvaluateDirectSunPBR(lighting.Environment, albedo, roughness, metallic, clearcoat, clearcoatRoughness, shadingNormal, viewDir) * sunVis
        : 0.0;

    float NdotV = saturate(dot(shadingNormal, viewDir));
    float3 F0 = lerp(float3(0.04, 0.04, 0.04), albedo, metallic);
    float3 F = FresnelSchlick(NdotV, F0);
    float Fc = clearcoat > 0.0 ? (0.04 + 0.96 * pow(1.0 - NdotV, 5.0)) * clearcoat : 0.0;
    float3 kd = (1.0 - F) * (1.0 - metallic) * (1.0 - Fc * 0.5);

    float3 ceilingDiffuse = areaVis > 0.0
        ? kd * albedo * EvaluateClippedQuadIrradiance(lighting.Environment, position, shadingNormal) * areaVis
        : 0.0;

    float3 nightDirect = isPrimary
        ? EvaluateNightLightsPBR(lighting.Environment, position, shadingNormal, viewDir,
                                 albedo, roughness, metallic, clearcoat, clearcoatRoughness, nightVis)
        : EvaluateNightLightsSecondary(lighting.Environment, position, shadingNormal, albedo * (1.0 - metallic) * InvPI);

    outDdgiIrradiance = SampleDDGIIrradiance(position, shadingNormal, viewDir,
                                             lighting.IrradianceAtlas, lighting.DistanceAtlas,
                                             lighting.LinearSampler);
    float3 indirectDiffuse = kd * albedo * outDdgiIrradiance * lerp(1.0, ao, 0.75);
    float3 emission = EvaluateMaterialEmission(materialId, material, lighting.Environment);
    float3 caustics = isPrimary
        ? EvaluateGlassCaustics(lighting.Environment, position, shadingNormal, sunVis, areaVis, kd * albedo)
        : 0.0;
    float3 subsurface = (isPrimary && materialId == WaterMaterialId)
        ? (1.0 - F) * EvaluateWaterSubsurface(lighting.Environment, position, shadingNormal, viewDir, sunVis)
        : 0.0;

    return max(sunDirect + ceilingDiffuse + indirectDiffuse + caustics, 0.0) + nightDirect + subsurface + emission;
}

float3 ShadeSecondaryHit(GeometryHit hit, float3 rayDir, float hitDistance, bool includeEmissiveSpecular)
{
    SceneMaterial material = lighting.Materials[hit.MaterialId];
    float3 albedo = material.Albedo;
    float roughness = max(material.Roughness, MinimumRoughness);
    EvaluateProceduralPattern(material, hit.ObjectPosition, hit.LocalNormal, 0.0, albedo, roughness);

    float3 normal = dot(hit.Normal, rayDir) > 0.0 ? -hit.Normal : hit.Normal;
    float3 geoNormal = dot(hit.GeometricNormal, rayDir) > 0.0 ? -hit.GeometricNormal : hit.GeometricNormal;
    if (hit.MaterialId == WaterMaterialId)
    {
        float detailFade = saturate(1.0 - hitDistance * 0.0008);
        normal = EvaluateProceduralNormal(hit.MaterialId, material, hit.Position, hit.ObjectPosition,
                                          normal, hit.LocalNormal, lighting.Environment.ElapsedSeconds, detailFade);
    }

    float3 viewDir = -rayDir;

    float3 keyDir = lighting.Environment.KeyLightDirection;
    float3 shadowOrigin = OffsetRayOrigin(hit.Position, geoNormal, keyDir);
    float sunVis = dot(normal, keyDir) > 0.0
        ? (TraceVisibilityRay(lighting.Scene, shadowOrigin, keyDir, RayMaxDistance) ? 1.0 : 0.0)
        : 0.0;

    float areaVis = 0.0;
    if (hit.Position.y < CeilingLightCenter.y - 0.5 && abs(hit.Position.x) < 350.0 && hit.Position.z > -320.0 && hit.Position.z < 585.0)
    {
        float3 toCeiling = CeilingLightCenter - hit.Position;
        float dist = length(toCeiling);
        float3 dirToCeiling = toCeiling / max(dist, 1e-4);
        if (dot(normal, dirToCeiling) > -0.2)
        {
            float3 ceilOrigin = OffsetRayOrigin(hit.Position, geoNormal, dirToCeiling);
            areaVis = TraceVisibilityRay(lighting.Scene, ceilOrigin, dirToCeiling, dist - 1.5) ? 1.0 : 0.0;
        }
    }

    float3 dummyDdgi;
    float3 color = ShadeOpaqueSurface(hit.MaterialId, hit.Position, normal, viewDir, albedo, roughness, material, sunVis, areaVis, 1.0, 1.0, false, dummyDdgi);

    float metallic = saturate(material.Metallic);
    float clearcoat = saturate(material.Clearcoat);
    if (includeEmissiveSpecular && (metallic > 0.15 || clearcoat > 0.35))
    {
        float3 reflDir = reflect(rayDir, normal);
        float3 envDir = normalize(lerp(normal, reflDir, 0.75));
        float3 envIrr = SampleDDGIIrradiance(hit.Position, envDir, viewDir,
                                             lighting.IrradianceAtlas, lighting.DistanceAtlas,
                                             lighting.LinearSampler);
        float3 envSky = envDir.y > 0.05 && !(abs(hit.Position.x) < 324.0 && hit.Position.y < 560.0 && hit.Position.z > 0.0 && hit.Position.z < 588.0)
            ? EvaluateSkyAmbient(reflDir, lighting.Environment)
            : 0.0;
        float3 envRadiance = max(envIrr, envSky * 0.65);

        float3 F0 = lerp(float3(0.04, 0.04, 0.04), albedo, metallic);
        float NdotV = saturate(dot(normal, viewDir));
        float3 specScale = EstimateEnvSpecularAlbedo(F0, roughness, NdotV) * (metallic + clearcoat * 0.25);
        color += envRadiance * specScale;
    }

    return ApplyAerialPerspective(color, hitDistance, lighting.Environment);
}

float EvaluateReactivity(uint2 pixel, float2 motionVector, float linearDepth, bool isDynamicNow)
{
    if ((lighting.Flags & FlagReset) != 0u)
    {
        return 0.0;
    }

    float2 prevUv = (float2(pixel) + 0.5) / lighting.RenderSize + motionVector;
    if (any(prevUv < 0.0) || any(prevUv > 1.0))
    {
        return 0.85;
    }

    int2 prevCenter = int2(prevUv * lighting.RenderSize);
    int2 maxPixel = int2(lighting.RenderSize) - 1;
    float maxPrevDepth = 0.0;
    bool wasDynamicNearby = false;
    static const int2 Offsets[5] = { int2(0, 0), int2(-1, 0), int2(1, 0), int2(0, -1), int2(0, 1) };

    for (int i = 0; i < 5; i++)
    {
        int2 p = clamp(prevCenter + Offsets[i], int2(0, 0), maxPixel);
        float rawPrev = lighting.PrevLinearDepth.Load(int3(p, 0));
        maxPrevDepth = max(maxPrevDepth, abs(rawPrev));
        if (rawPrev < 0.0)
        {
            wasDynamicNearby = true;
        }
    }

    float pixelSpeed = length(motionVector * lighting.RenderSize);
    float reactivity = 0.0;

    if (isDynamicNow)
    {
        reactivity = saturate(pixelSpeed * 1.4);
    }
    else if (wasDynamicNearby)
    {
        reactivity = 0.80;
    }
    else if (pixelSpeed > 0.15 && maxPrevDepth > 0.0 && (linearDepth - maxPrevDepth) > max(16.0, linearDepth * 0.06))
    {
        reactivity = 0.65 * saturate((pixelSpeed - 0.15) * 0.5);
    }

    return reactivity;
}

float3 ShadeGlassPixel(float3 hitPos, float3 frontNormal, float3 viewDir, SceneMaterial material, float baseF0, float pixelSpeed, out float reflectionReactivity)
{
    float3 rayDir = -viewDir;
    float ior = max(material.Ior, 1.0);
    float cosTheta = abs(dot(frontNormal, viewDir));
    float fresnel = DielectricFresnelSchlick(cosTheta, ior);
    bool hitDynamic = false;

    float3 reflectedRadiance = 0.0;
    float3 reflDir = reflect(rayDir, frontNormal);
    float3 reflOrigin = hitPos + frontNormal * max(RayOffset, length(hitPos - lighting.CameraPosition) * 0.0005);

    uint rInst;
    uint rPrim;
    float2 rBary;
    float rDist;
    if (TraceClosestHit(lighting.Scene, reflOrigin, reflDir, RayMaskOpaque, rInst, rPrim, rBary, rDist))
    {
        GeometryHit rHit = ReconstructGeometryHit(rInst, rPrim, rBary, lighting.Vertices, lighting.Indices, lighting.Instances);
        reflectedRadiance = ShadeSecondaryHit(rHit, reflDir, rDist, true);
        if (rHit.IsDynamic != 0u)
        {
            float3 prevPos = mul(float4(rHit.ObjectPosition, 1.0), lighting.Instances[rInst].PreviousObjectToWorld).xyz;
            if (dot(rHit.Position - prevPos, rHit.Position - prevPos) > 1e-4)
            {
                hitDynamic = true;
            }
        }
    }
    else
    {
        reflectedRadiance = EvaluateSkyFull(reflDir, lighting.Environment);
    }

    float glassRough = max(material.Roughness, MinimumRoughness);
    float3 glassF0 = float3(baseF0);
    float3 keyHighlight = 0.0;
    float3 keyDir = lighting.Environment.KeyLightDirection;
    if (dot(frontNormal, keyDir) > 0.0 && TraceVisibilityRay(lighting.Scene, reflOrigin, keyDir, RayMaxDistance))
    {
        keyHighlight += EvaluateDirectSunSpecular(lighting.Environment, glassRough, 0.0, material.Albedo, frontNormal, viewDir, ior, true);
    }

    if (hitPos.y < CeilingLightCenter.y - 0.5 && abs(hitPos.x) < 350.0 && hitPos.z > -320.0 && hitPos.z < 585.0)
    {
        float3 toCeiling = CeilingLightCenter - hitPos;
        float ceilingDist = length(toCeiling);
        float3 dirToCeiling = toCeiling / max(ceilingDist, 1e-4);
        if (dot(frontNormal, dirToCeiling) > 0.0 && TraceVisibilityRay(lighting.Scene, reflOrigin, dirToCeiling, ceilingDist - 1.5))
        {
            keyHighlight += EvaluateAreaLightSpecular(lighting.Environment, hitPos, frontNormal, viewDir, glassRough, glassF0);
        }
    }

    if (lighting.Environment.NightLightIntensity > 0.001)
    {
        int bestIdx = -1;
        float bestScore = 0.0;
        float3 bestL = 0.0;
        float bestDist = 0.0;
        float bestAtten = 0.0;
        float bestNdotL = 0.0;
        for (uint i = 0u; i < NightLanternCount; i++)
        {
            float3 toLantern = NightLanternPositions[i] - hitPos;
            float distSq = dot(toLantern, toLantern);
            float invDist;
            float atten = ComputeLanternAttenuation(distSq, invDist);
            if (atten > 0.0)
            {
                float dist = distSq * invDist;
                float3 L = toLantern * invDist;
                float ndl = saturate(dot(frontNormal, L));
                float score = atten * ndl;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIdx = int(i);
                    bestL = L;
                    bestDist = dist;
                    bestAtten = atten;
                    bestNdotL = ndl;
                }
            }
        }

        if (bestIdx >= 0 && bestScore > 5e-8 && TraceVisibilityRay(lighting.Scene, reflOrigin, bestL, bestDist - 26.0))
        {
            float NdotV = max(dot(frontNormal, viewDir), 1e-3);
            float lightSinTheta = saturate(11.5 / bestDist);
            float effRough = clamp(sqrt(glassRough * glassRough + 0.5 * lightSinTheta), MinimumRoughness, 1.0);
            float3 specBrdf = EvaluateDirectLightBrdf(bestL, bestNdotL, NdotV, frontNormal, viewDir, glassF0,
                                                      0.0, 1.0, effRough, 0.0, 0.0, MinimumRoughness);
            keyHighlight += specBrdf * (bestNdotL * bestAtten * 38000.0 * lighting.Environment.NightLightIntensity) * float3(1.00, 0.64, 0.28);
        }
    }

    float3 refractedRadiance = 0.0;
    float3 transmittance = 1.0;
    float3 absorbCoeff = max(1.0 - material.Albedo, 0.01) * 0.014;

    float3 currNormal = frontNormal;
    float3 currDir = rayDir;
    bool insideGlass = true;
    float eta = 1.0 / ior;
    float3 refrDir = refract(currDir, currNormal, eta);
    if (dot(refrDir, refrDir) < 1e-6)
    {
        refrDir = reflect(currDir, currNormal);
        insideGlass = false;
    }
    else
    {
        refrDir = normalize(refrDir);
    }

    currDir = refrDir;
    float3 stepOrigin = hitPos - frontNormal * 0.12;
    bool hitOpaque = false;
    uint opaqueInst = 0u;
    float opaqueDist = RayMaxDistance;
    float3 opaqueDir = currDir;
    GeometryHit opaqueHit = {};

    [loop]
    for (int step = 0; step < 4; step++)
    {
        uint hInst;
        uint hPrim;
        float2 hBary;
        float hDist;
        if (!TraceClosestHit(lighting.Scene, stepOrigin, currDir, RayMaskAll, hInst, hPrim, hBary, hDist))
        {
            break;
        }

        if (insideGlass)
        {
            transmittance *= exp(-absorbCoeff * hDist);
        }

        GeometryHit hit = ReconstructGeometryHit(hInst, hPrim, hBary, lighting.Vertices, lighting.Indices, lighting.Instances);
        SceneMaterial hitMat = lighting.Materials[hit.MaterialId];

        if (hitMat.Transmission <= 0.5)
        {
            hitOpaque = true;
            opaqueInst = hInst;
            opaqueDist = hDist;
            opaqueDir = currDir;
            opaqueHit = hit;
            break;
        }

        if (hit.IsDynamic != 0u)
        {
            hitDynamic = true;
        }

        float3 exitNormal = dot(hit.Normal, currDir) > 0.0 ? -hit.Normal : hit.Normal;
        float3 exitGeo = dot(hit.GeometricNormal, currDir) > 0.0 ? -hit.GeometricNormal : hit.GeometricNormal;
        float nextEta = insideGlass ? max(hitMat.Ior, 1.0) : (1.0 / max(hitMat.Ior, 1.0));
        float3 nextDir = refract(currDir, exitNormal, nextEta);

        if (dot(nextDir, nextDir) < 1e-6)
        {
            currDir = normalize(reflect(currDir, exitNormal));
            stepOrigin = OffsetRayOrigin(hit.Position, exitGeo, currDir);
        }
        else
        {
            currDir = normalize(nextDir);
            insideGlass = !insideGlass;
            stepOrigin = hit.Position - exitGeo * 0.12;
        }
    }

    if (hitOpaque)
    {
        if (opaqueHit.IsDynamic != 0u)
        {
            float3 prevPos = mul(float4(opaqueHit.ObjectPosition, 1.0), lighting.Instances[opaqueInst].PreviousObjectToWorld).xyz;
            if (dot(opaqueHit.Position - prevPos, opaqueHit.Position - prevPos) > 1e-4)
            {
                hitDynamic = true;
            }
        }

        refractedRadiance = transmittance * ShadeSecondaryHit(opaqueHit, opaqueDir, opaqueDist, true);
    }
    else
    {
        refractedRadiance = transmittance * EvaluateSkyFull(currDir, lighting.Environment);
    }

    if (hitDynamic)
    {
        reflectionReactivity = 0.85;
    }
    else if (pixelSpeed > 0.15)
    {
        reflectionReactivity = saturate(pixelSpeed * 0.6) * saturate(0.55 + fresnel * 0.35);
    }
    else
    {
        reflectionReactivity = 0.0;
    }

    return reflectedRadiance * fresnel + refractedRadiance * (1.0 - fresnel) + keyHighlight;
}

[shader("compute")]
[numthreads(8, 8, 1)]
void LightingCS(uint3 dispatchThreadId: SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadId.xy;
    uint width = uint(lighting.RenderSize.x);
    uint height = uint(lighting.RenderSize.y);
    if (pixel.x >= width || pixel.y >= height)
    {
        return;
    }

    float2 uv = (float2(pixel) + 0.5) / lighting.RenderSize;
    float deviceDepth = lighting.Depth.Load(int3(pixel, 0));

    if (deviceDepth >= 0.999999)
    {
        float3 rayDir = GetCameraRayDirection(uv, lighting.InverseJitteredProjection, lighting.InverseView);
        float3 skyColor = min(EvaluateSkyFull(rayDir, lighting.Environment), float3(32.0));
        lighting.Color.Store(pixel, float4(skyColor, 1.0));

        if ((lighting.Flags & FlagHasReactiveMask) != 0u)
        {
            lighting.ReactiveMask.Store(pixel, 0.0);
        }

        return;
    }

    float4 albedoMat = lighting.AlbedoAndMaterial.Load(int3(pixel, 0));
    float3 albedo = albedoMat.rgb;
    uint materialId = min(uint(round(albedoMat.a * 255.0)), MaxMaterialId);
    SceneMaterial material = lighting.Materials[materialId];

    float4 normalRough = lighting.NormalAndRoughness.Load(int3(pixel, 0));
    float3 normal = normalize(normalRough.xyz);
    bool isDynamic = normalRough.w < 0.0;
    float effectiveRoughness = max(abs(normalRough.w), MinimumRoughness);

    float3 worldPos = ReconstructWorldPosition(uv, deviceDepth, lighting.InverseJitteredViewProjection);
    float3 toCamera = lighting.CameraPosition - worldPos;
    float linearDepth = length(toCamera);
    float3 viewDir = toCamera / max(linearDepth, 1e-4);

    bool computeReactive = (lighting.Flags & FlagHasReactiveMask) != 0u;
    float2 motionVector = (computeReactive || material.Transmission > 0.5) ? lighting.MotionVectors.Load(int3(pixel, 0)) : 0.0;
    float pixelSpeed = length(motionVector * lighting.RenderSize);
    float reactivity = computeReactive ? EvaluateReactivity(pixel, motionVector, linearDepth, isDynamic) : 0.0;

    if (material.Transmission > 0.5)
    {
        float ior = max(material.Ior, 1.0);
        float r0 = (1.0 - ior) / (1.0 + ior);
        float glassReactivity;
        float3 glassColor = ShadeGlassPixel(worldPos, normal, viewDir, material, r0 * r0, pixelSpeed, glassReactivity);
        lighting.Color.Store(pixel, float4(glassColor, 1.0));

        if (computeReactive)
        {
            lighting.ReactiveMask.Store(pixel, saturate(max(reactivity, glassReactivity)));
        }

        return;
    }

    float4 shadowAo = lighting.ShadowAo.Load(int3(pixel, 0));
    float sunVis = shadowAo.r;
    float areaVis = shadowAo.g;
    float ao = shadowAo.b;
    float nightVis = shadowAo.a;

    float metallic = saturate(material.Metallic);
    float clearcoat = saturate(material.Clearcoat);
    float clearcoatRoughness = max(material.ClearcoatRoughness, MinimumRoughness);
    float baseRoughness = effectiveRoughness;
    if (clearcoat > 0.0)
    {
        float baseMatRough = max(material.Roughness, MinimumRoughness) + (material.PatternType == PatternStripes ? 0.06 : 0.0);
        float geoVar = (material.PatternType == PatternChecker || material.PatternType == PatternGrid)
            ? 0.0
            : max(0.0, effectiveRoughness * effectiveRoughness - baseMatRough * baseMatRough);
        clearcoatRoughness = clamp(sqrt(clearcoatRoughness * clearcoatRoughness + geoVar), MinimumRoughness, 1.0);
    }

    float3 ddgiIrradiance;
    float3 directAndDiffuse = ShadeOpaqueSurface(materialId, worldPos, normal, viewDir, albedo, baseRoughness, material, sunVis, areaVis, ao, nightVis, true, ddgiIrradiance);

    float NdotV = saturate(dot(normal, viewDir));
    float3 F0 = lerp(float3(0.04, 0.04, 0.04), albedo, metallic);
    float3 envBaseSpec = EstimateEnvSpecularAlbedo(F0, baseRoughness, NdotV);
    envBaseSpec *= 1.0 + F0 * saturate(1.0 / max(envBaseSpec, 0.04) - 1.0) * (metallic * 0.35);
    float3 envCoatSpec = clearcoat > 0.0
        ? EstimateEnvSpecularAlbedo(float3(0.04, 0.04, 0.04), clearcoatRoughness, NdotV) * clearcoat
        : 0.0;
    float3 totalSpecAlbedo = envBaseSpec * (1.0 - envCoatSpec.r) + envCoatSpec;

    float lobeRoughness = clearcoat > 0.2 ? clearcoatRoughness : baseRoughness;
    float mirrorWeight = saturate(1.0 - smoothstep(0.06, 0.42, lobeRoughness));
    float3 envColor = ddgiIrradiance;

    if (mirrorWeight > 0.001)
    {
        float3 reflDir = reflect(-viewDir, normal);
        float3 reflOrigin = worldPos + normal * max(RayOffset, linearDepth * 0.0004);

        float3 tracedEnv = 0.0;
        bool hitCeilingLight = false;
        uint rInst;
        uint rPrim;
        float2 rBary;
        float rDist;

        if (TraceClosestHit(lighting.Scene, reflOrigin, reflDir, RayMaskAll, rInst, rPrim, rBary, rDist))
        {
            GeometryHit rHit = ReconstructGeometryHit(rInst, rPrim, rBary, lighting.Vertices, lighting.Indices, lighting.Instances);
            SceneMaterial rMat = lighting.Materials[rHit.MaterialId];

            if (rMat.Transmission > 0.5)
            {
                float3 rNormal = dot(rHit.Normal, reflDir) > 0.0 ? -rHit.Normal : rHit.Normal;
                float rIor = max(rMat.Ior, 1.0);
                float rFresnel = DielectricFresnelSchlick(abs(dot(rNormal, -reflDir)), rIor);
                float3 tDir = refract(reflDir, rNormal, 1.0 / rIor);
                tDir = dot(tDir, tDir) > 1e-6 ? normalize(tDir) : reflect(reflDir, rNormal);

                uint tInst;
                uint tPrim;
                float2 tBary;
                float tDist;
                float3 refractedGlass = TraceClosestHit(lighting.Scene, rHit.Position + tDir * 0.25, tDir, RayMaskOpaque, tInst, tPrim, tBary, tDist)
                    ? ShadeSecondaryHit(ReconstructGeometryHit(tInst, tPrim, tBary, lighting.Vertices, lighting.Indices, lighting.Instances), tDir, tDist, false)
                    : EvaluateSkyFull(tDir, lighting.Environment);
                refractedGlass *= lerp(float3(1.0), rMat.Albedo, 0.85);

                float3 secondaryRefl = reflect(reflDir, rNormal);
                float3 reflectedGlass = SampleDDGIIrradiance(rHit.Position, rNormal, -reflDir, lighting.IrradianceAtlas, lighting.DistanceAtlas, lighting.LinearSampler);
                bool rInsideSanctuary = abs(rHit.Position.x) < 292.0 && rHit.Position.y < 560.0 && rHit.Position.z > 0.0 && rHit.Position.z < 562.0;
                if (!rInsideSanctuary)
                {
                    reflectedGlass = max(reflectedGlass, EvaluateSkyAmbient(secondaryRefl, lighting.Environment));
                }

                tracedEnv = lerp(refractedGlass, reflectedGlass, rFresnel);
            }
            else
            {
                hitCeilingLight = rHit.MaterialId == CeilingLightMaterialId;
                tracedEnv = ShadeSecondaryHit(rHit, reflDir, rDist, true);
            }

            if (computeReactive && rHit.IsDynamic != 0u)
            {
                float3 prevReflPos = mul(float4(rHit.ObjectPosition, 1.0), lighting.Instances[rInst].PreviousObjectToWorld).xyz;
                float3 reflDelta = rHit.Position - prevReflPos;
                float4 curClip = mul(float4(worldPos, 1.0), lighting.ViewProjection);
                float4 prevClip = mul(float4(worldPos - reflDelta, 1.0), lighting.PreviousViewProjection);
                float reflSpeed = length(ComputeMotionVector(curClip, prevClip) * lighting.RenderSize);
                float reflWeight = mirrorWeight * max(totalSpecAlbedo.r, max(totalSpecAlbedo.g, totalSpecAlbedo.b));
                reactivity = max(reactivity, saturate(reflSpeed * 1.6) * reflWeight);
            }
        }
        else
        {
            tracedEnv = mirrorWeight > 0.55
                ? EvaluateSkyFull(reflDir, lighting.Environment)
                : EvaluateSkyAmbient(reflDir, lighting.Environment);
        }

        tracedEnv = min(tracedEnv, lerp(float3(28.0), float3(6.0), saturate((lobeRoughness - 0.08) * 3.0)));

        envColor = (mirrorWeight >= 0.98 || (hitCeilingLight && mirrorWeight > 0.5))
            ? tracedEnv
            : lerp(ddgiIrradiance, tracedEnv, mirrorWeight);
    }

    float areaSpecScale = areaVis * (1.0 - mirrorWeight);
    float3 analyticAreaSpec = 0.0;
    if (areaSpecScale > 1e-4)
    {
        float3 baseAreaSpec = EvaluateAreaLightSpecular(lighting.Environment, worldPos, normal, viewDir, baseRoughness, F0) * (1.0 - envCoatSpec.r);
        float3 coatAreaSpec = clearcoat > 0.0
            ? EvaluateAreaLightSpecular(lighting.Environment, worldPos, normal, viewDir, clearcoatRoughness, float3(0.04)) * clearcoat
            : 0.0;
        analyticAreaSpec = (baseAreaSpec + coatAreaSpec) * areaSpecScale;
    }

    float3 specularRadiance = envColor * totalSpecAlbedo * lerp(1.0, ao, 0.45) + analyticAreaSpec;

    float3 totalColor = ApplyAerialPerspective(directAndDiffuse + specularRadiance, linearDepth, lighting.Environment);
    lighting.Color.Store(pixel, float4(totalColor, 1.0));

    if (computeReactive)
    {
        if (materialId == WaterMaterialId && !lighting.Environment.IsPaused)
        {
            reactivity = max(reactivity, 0.25);
        }

        lighting.ReactiveMask.Store(pixel, reactivity);
    }
}
