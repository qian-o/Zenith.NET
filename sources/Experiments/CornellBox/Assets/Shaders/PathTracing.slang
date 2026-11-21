#include "PathTracingCommon.slang"

struct PathTracingConstants
{
    float4x4 InverseView;

    float4x4 InverseProjection;

    float4x4 ViewProjection;

    float4x4 PreviousViewProjection;

    float3 CameraPosition;
    uint FrameIndex;

    float2 RenderSize;
    float2 JitterOffset;

    uint Flags;
    uint3 Padding;

    DescriptorHandle<RaytracingAccelerationStructure> Scene;

    DescriptorHandle<StructuredBuffer<SceneVertex>> Vertices;

    DescriptorHandle<StructuredBuffer<uint>> Indices;

    DescriptorHandle<StructuredBuffer<SceneMaterial>> Materials;

    DescriptorHandle<StructuredBuffer<SceneInstance>> Instances;

    DescriptorHandle<RWTexture2D<uint4>> VisibilityStorage;

    DescriptorHandle<Texture2D<uint4>> VisibilitySampled;

    DescriptorHandle<Texture2D<float>> PreviousLinearDepth;

    DescriptorHandle<RWTexture2D<float>> CurrentLinearDepth;

    DescriptorHandle<RWTexture2D<float4>> Color;

    DescriptorHandle<RWTexture2D<float>> Depth;

    DescriptorHandle<RWTexture2D<float2>> MotionVectors;

    DescriptorHandle<RWTexture2D<float>> ReactiveMask;

    DescriptorHandle<RWTexture2D<float4>> DiffuseAlbedo;

    DescriptorHandle<RWTexture2D<float4>> SpecularAlbedo;

    DescriptorHandle<RWTexture2D<float4>> Normals;

    DescriptorHandle<RWTexture2D<float>> Roughness;

    DescriptorHandle<RWTexture2D<float>> SpecularHitDistance;
};

ConstantBuffer<PathTracingConstants> pathTracing;

float3 GetCameraRayDirection(uint2 pixel)
{
    return ComputeCameraRayDirection(pixel, pathTracing.RenderSize, pathTracing.JitterOffset,
                                     pathTracing.InverseProjection, pathTracing.InverseView);
}

bool TraceClosestHit(float3 origin, float3 direction, out uint instanceIndex, out uint primitiveIndex,
                     out float2 barycentrics, out float hitDistance)
{
    RayDesc ray;
    ray.Origin = origin;
    ray.Direction = direction;
    ray.TMin = RayMinDistance;
    ray.TMax = RayMaxDistance;

    RayQuery<RAY_FLAG_FORCE_OPAQUE | RAY_FLAG_SKIP_PROCEDURAL_PRIMITIVES> query;
    query.TraceRayInline(pathTracing.Scene, RAY_FLAG_NONE, 0xFFu, ray);
    query.Proceed();

    if (query.CommittedStatus() == COMMITTED_NOTHING)
    {
        instanceIndex = InvalidIndex;
        primitiveIndex = 0u;
        barycentrics = 0.0;
        hitDistance = MissDistance;

        return false;
    }

    instanceIndex = query.CommittedInstanceIndex();
    primitiveIndex = query.CommittedPrimitiveIndex();
    barycentrics = query.CommittedTriangleBarycentrics();
    hitDistance = query.CommittedRayT();

    return true;
}

float EvaluateDisocclusionReactivity(uint2 pixel, float2 renderSize, float2 motionVector, float currentLinearW,
                                     bool isDynamicNow, bool isResetFrame)
{
    if (isResetFrame)
    {
        return 0.0;
    }

    float2 currentUv = (float2(pixel) + 0.5) / renderSize;
    float2 previousUv = currentUv + motionVector;

    if (any(previousUv < 0.0) || any(previousUv > 1.0))
    {
        return 0.85;
    }

    int2 previousCenter = int2(previousUv * renderSize);
    int2 maxPixel = int2(renderSize) - 1;
    float maxPrevDepth = 0.0;
    bool wasDynamicNearby = false;

    static const int2 Offsets[5] = { int2(0, 0), int2(-1, 0), int2(1, 0), int2(0, -1), int2(0, 1) };
    for (int i = 0; i < 5; i++)
    {
        int2 samplePos = clamp(previousCenter + Offsets[i], int2(0, 0), maxPixel);
        float rawPrev = pathTracing.PreviousLinearDepth.Load(int3(samplePos, 0));
        maxPrevDepth = max(maxPrevDepth, abs(rawPrev));

        if (rawPrev < 0.0)
        {
            wasDynamicNearby = true;
        }
    }

    float reactivity = 0.0;
    if (maxPrevDepth > 0.0 && (currentLinearW - maxPrevDepth) > max(16.0, currentLinearW * 0.06))
    {
        reactivity = 0.9;
    }

    if (!isDynamicNow && wasDynamicNearby)
    {
        reactivity = max(reactivity, 0.85);
    }

    if (isDynamicNow)
    {
        float pixelSpeed = length(motionVector * renderSize);
        reactivity = max(reactivity, saturate(pixelSpeed * 1.5));
    }

    return reactivity;
}

[shader("compute")]
[numthreads(8, 8, 1)]
void PrimaryRayCS(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadID.xy;
    uint2 renderSize = uint2(pathTracing.RenderSize);

    if (any(pixel >= renderSize))
    {
        return;
    }

    float3 origin = pathTracing.CameraPosition;
    float3 direction = GetCameraRayDirection(pixel);

    uint primaryInstance;
    uint primaryPrimitive;
    float2 primaryBarycentrics;
    float primaryDistance;

    if (!TraceClosestHit(origin, direction, primaryInstance, primaryPrimitive, primaryBarycentrics, primaryDistance))
    {
        float4 currentSkyClip = mul(float4(direction, 0.0), pathTracing.ViewProjection);
        float4 previousSkyClip = mul(float4(direction, 0.0), pathTracing.PreviousViewProjection);

        pathTracing.VisibilityStorage[pixel] = uint4(0u, 0u, 0u, asuint(-1.0));
        pathTracing.Depth[pixel] = 1.0;
        pathTracing.MotionVectors[pixel] = ComputeMotionVector(currentSkyClip, previousSkyClip);
        pathTracing.CurrentLinearDepth[pixel] = MissDistance;

        if ((pathTracing.Flags & FlagHasRayReconstructionGuides) != 0u)
        {
            pathTracing.DiffuseAlbedo[pixel] = float4(0.35, 0.35, 0.35, 1.0);
            pathTracing.SpecularAlbedo[pixel] = float4(0.0, 0.0, 0.0, 1.0);
            pathTracing.Normals[pixel] = float4(-direction, 0.0);
            pathTracing.Roughness[pixel] = 1.0;
        }

        return;
    }

    GeometryHit primaryHit = ReconstructGeometryHit(primaryInstance, primaryPrimitive, primaryBarycentrics,
                                                    pathTracing.Vertices, pathTracing.Indices, pathTracing.Instances);
    SceneMaterial primaryMat = pathTracing.Materials[primaryHit.MaterialId];

    float3 albedo = primaryMat.Albedo;
    float roughness = max(primaryMat.Roughness, MinimumRoughness);
    EvaluateProceduralPattern(primaryMat, primaryHit.ObjectPosition, primaryHit.LocalNormal, albedo, roughness);

    float3 primaryNormal = primaryHit.Normal;
    float3 primaryGeoNormal = primaryHit.GeometricNormal;
    if (dot(primaryGeoNormal, direction) > 0.0)
    {
        primaryNormal = -primaryNormal;
        primaryGeoNormal = -primaryGeoNormal;
    }

    float3 anchorWorldPos = primaryHit.Position;
    float3 anchorPrevWorldPos =
        mul(float4(primaryHit.ObjectPosition, 1.0), pathTracing.Instances[primaryInstance].PreviousObjectToWorld).xyz;
    bool anchorIsDynamic = primaryHit.IsDynamic != 0u;
    bool primaryIsDynamic = anchorIsDynamic;
    float transmission = saturate(primaryMat.Transmission);
    float glassReactivity = 0.0;

    if (transmission > 0.08 && roughness < 0.25)
    {
        float ior = max(primaryMat.Ior, 1.0);
        float cosTheta = abs(dot(primaryNormal, -direction));
        float fresnel = DielectricFresnelSchlick(cosTheta, ior);
        glassReactivity = saturate(0.58 + fresnel * 0.38 + (anchorIsDynamic ? 0.30 : 0.0));

        float3 rawPos = primaryHit.Position;
        float3 rawNormal = primaryHit.Normal;
        float3 rawGeoNormal = primaryHit.GeometricNormal;
        float rawIor = ior;
        float3 transDir = direction;
        float totalTravel = primaryDistance;
        bool foundBackground = false;
        float3 bgPos = 0.0;
        float3 bgPrevWorld = 0.0;
        bool bgIsDynamic = false;

        for (int layer = 0; layer < 2; layer++)
        {
            bool entering = dot(rawGeoNormal, transDir) < 0.0;
            float3 orientedNormal = entering ? rawNormal : -rawNormal;
            float3 orientedGeoNormal = entering ? rawGeoNormal : -rawGeoNormal;
            float eta = rawIor <= 1.04 ? 1.0 : (entering ? (1.0 / rawIor) : rawIor);
            float3 refracted = refract(transDir, orientedNormal, eta);

            if (dot(refracted, refracted) < 1e-4)
            {
                refracted = transDir;
            }

            transDir = normalize(refracted);
            float3 transOrigin = OffsetRayOrigin(rawPos, orientedGeoNormal, transDir);

            uint nextInstance;
            uint nextPrimitive;
            float2 nextBary;
            float nextDist;

            if (!TraceClosestHit(transOrigin, transDir, nextInstance, nextPrimitive, nextBary, nextDist))
            {
                totalTravel = MissDistance;
                break;
            }

            totalTravel += nextDist;
            GeometryHit nextHit = ReconstructGeometryHit(nextInstance, nextPrimitive, nextBary, pathTracing.Vertices,
                                                         pathTracing.Indices, pathTracing.Instances);
            SceneMaterial nextMat = pathTracing.Materials[nextHit.MaterialId];

            if (saturate(nextMat.Transmission) <= 0.08)
            {
                foundBackground = true;
                bgPos = nextHit.Position;
                bgPrevWorld = mul(float4(nextHit.ObjectPosition, 1.0),
                                  pathTracing.Instances[nextInstance].PreviousObjectToWorld)
                                  .xyz;
                bgIsDynamic = nextHit.IsDynamic != 0u;
                break;
            }

            rawPos = nextHit.Position;
            rawNormal = nextHit.Normal;
            rawGeoNormal = nextHit.GeometricNormal;
            rawIor = max(nextMat.Ior, 1.0);
        }

        if (foundBackground && !anchorIsDynamic)
        {
            float3 bgDelta = bgPos - bgPrevWorld;
            float3 virtualCurrent = origin + direction * min(totalTravel, RayMaxDistance);

            anchorWorldPos = virtualCurrent;
            anchorPrevWorldPos = virtualCurrent - bgDelta;
            anchorIsDynamic = bgIsDynamic;
        }
    }

    float4 currentClip = mul(float4(anchorWorldPos, 1.0), pathTracing.ViewProjection);
    float4 previousClip = mul(float4(anchorPrevWorldPos, 1.0), pathTracing.PreviousViewProjection);
    float2 motionVector = ComputeMotionVector(currentClip, previousClip);
    float clipDepth = saturate(currentClip.z / max(currentClip.w, 1e-5));
    float cameraDistance = length(anchorWorldPos - origin);
    float signedLinearDepth = anchorIsDynamic ? -cameraDistance : cameraDistance;

    float baseReactivity = 0.0;
    if ((pathTracing.Flags & FlagHasReactiveMask) != 0u)
    {
        float disocclusionReactivity = EvaluateDisocclusionReactivity(pixel, pathTracing.RenderSize, motionVector,
                                                                      cameraDistance, anchorIsDynamic,
                                                                      (pathTracing.Flags & FlagReset) != 0u);
        baseReactivity = saturate(max(disocclusionReactivity, glassReactivity));
    }

    pathTracing.VisibilityStorage[pixel] =
        uint4(PackNormal32(primaryNormal),
              PackGeoAndMeta(primaryGeoNormal, primaryHit.MaterialId, primaryIsDynamic, baseReactivity),
              PackAlbedoRoughness(albedo, roughness), asuint(primaryDistance));
    pathTracing.Depth[pixel] = clipDepth;
    pathTracing.MotionVectors[pixel] = motionVector;
    pathTracing.CurrentLinearDepth[pixel] = signedLinearDepth;

    if ((pathTracing.Flags & FlagHasRayReconstructionGuides) != 0u)
    {
        float metallic = saturate(primaryMat.Metallic);
        float clearcoat = saturate(primaryMat.Clearcoat);
        float clearcoatRoughness = max(primaryMat.ClearcoatRoughness, MinimumRoughness);
        float effectiveRoughness = clearcoat > 0.5 ? min(roughness, clearcoatRoughness) : roughness;

        float3 F0 = transmission > 0.5 ? float3(0.06, 0.06, 0.06)
                                       : lerp(float3(0.04, 0.04, 0.04), albedo, metallic);
        float3 specAlbedo = EstimateEnvSpecularAlbedo(F0, effectiveRoughness, dot(primaryNormal, -direction));
        float3 diffAlbedo = transmission > 0.5 ? albedo * 0.25 : albedo * (1.0 - metallic);

        pathTracing.DiffuseAlbedo[pixel] = float4(diffAlbedo, 1.0);
        pathTracing.SpecularAlbedo[pixel] = float4(specAlbedo, 1.0);
        pathTracing.Normals[pixel] = float4(primaryNormal, 0.0);
        pathTracing.Roughness[pixel] = effectiveRoughness;
    }
}

[shader("compute")]
[numthreads(8, 8, 1)]
void PathTraceCS(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadID.xy;
    uint2 renderSize = uint2(pathTracing.RenderSize);

    if (any(pixel >= renderSize))
    {
        return;
    }

    float3 origin = pathTracing.CameraPosition;
    float3 direction = GetCameraRayDirection(pixel);

    uint4 vis = pathTracing.VisibilitySampled.Load(int3(pixel, 0));
    float primaryDistance = asfloat(vis.w);

    if (primaryDistance < 0.0)
    {
        float3 skyColor = EvaluateSkyRadiance(direction, true);
        pathTracing.Color[pixel] = float4(min(skyColor, float3(24.0)), 1.0);

        if ((pathTracing.Flags & FlagHasReactiveMask) != 0u)
        {
            pathTracing.ReactiveMask[pixel] = 0.0;
        }

        if ((pathTracing.Flags & FlagHasRayReconstructionGuides) != 0u)
        {
            pathTracing.SpecularHitDistance[pixel] = MissDistance;
        }

        return;
    }

    float3 primaryGeoNormal;
    uint primaryMaterialId;
    bool primaryIsDynamic;
    float reactivity;
    UnpackGeoAndMeta(vis.y, primaryGeoNormal, primaryMaterialId, primaryIsDynamic, reactivity);

    float3 primaryAlbedo;
    float primaryRoughness;
    UnpackAlbedoRoughness(vis.z, primaryAlbedo, primaryRoughness);

    ShadingSurface surface;
    surface.Position = origin + direction * primaryDistance;
    surface.Normal = UnpackNormal32(vis.x);
    surface.GeometricNormal = primaryGeoNormal;
    surface.Albedo = primaryAlbedo;
    surface.Roughness = primaryRoughness;

    SceneMaterial primaryMat = pathTracing.Materials[primaryMaterialId];
    surface.Metallic = saturate(primaryMat.Metallic);
    surface.Clearcoat = saturate(primaryMat.Clearcoat);
    surface.ClearcoatRoughness = max(primaryMat.ClearcoatRoughness, MinimumRoughness);

    float emission = primaryMat.Emission;
    float transmission = saturate(primaryMat.Transmission);
    float ior = max(primaryMat.Ior, 1.0);

    uint rng = InitRandomSeed(pixel, renderSize, pathTracing.FrameIndex);
    float3 throughput = float3(1.0, 1.0, 1.0);
    float3 radiance = 0.0;
    float specularHitDistance = 0.0;
    float primaryReflectivity = 1.0 - saturate(primaryRoughness * 2.5);
    bool primaryTookSpecular = false;
    bool lastBounceWasDelta = false;
    int maxBounces = transmission > 0.5 ? 3 : 2;

    for (int bounce = 0; bounce <= maxBounces; bounce++)
    {
        if (bounce > 0)
        {
            uint hitInstance;
            uint hitPrimitive;
            float2 hitBarycentrics;
            float hitDist;
            bool hasHit = TraceClosestHit(origin, direction, hitInstance, hitPrimitive, hitBarycentrics, hitDist);

            if (bounce == 1 && primaryTookSpecular)
            {
                specularHitDistance = hasHit ? min(hitDist, MissDistance) : MissDistance;
            }

            if (!hasHit)
            {
                bool showSun = lastBounceWasDelta;
                radiance += throughput * EvaluateSkyRadiance(direction, showSun);
                break;
            }

            GeometryHit hit = ReconstructGeometryHit(hitInstance, hitPrimitive, hitBarycentrics, pathTracing.Vertices,
                                                     pathTracing.Indices, pathTracing.Instances);

            if (bounce == 1 && primaryTookSpecular && !primaryIsDynamic && hit.IsDynamic != 0u)
            {
                float3 prevReflectedPos = mul(float4(hit.ObjectPosition, 1.0),
                                              pathTracing.Instances[hitInstance].PreviousObjectToWorld)
                                              .xyz;
                float3 reflectedDelta = hit.Position - prevReflectedPos;
                float4 currentClip = mul(float4(origin, 1.0), pathTracing.ViewProjection);
                float4 previousClip = mul(float4(origin - reflectedDelta, 1.0), pathTracing.PreviousViewProjection);
                float2 reflectionMotion = ComputeMotionVector(currentClip, previousClip);
                float reflSpeed = length(reflectionMotion * pathTracing.RenderSize);

                reactivity = max(reactivity, saturate(reflSpeed * 1.8) * primaryReflectivity);

                if (primaryRoughness <= 0.06 && surface.Metallic > 0.85)
                {
                    specularHitDistance = 0.0;
                }
            }

            SceneMaterial mat = pathTracing.Materials[hit.MaterialId];
            surface = BuildShadingSurface(hit, mat);
            emission = mat.Emission;
            transmission = saturate(mat.Transmission);
            ior = max(mat.Ior, 1.0);
        }

        if (emission > 0.0)
        {
            float emissionScale = (bounce == 0 || lastBounceWasDelta) ? 1.0 : 0.35;
            radiance += throughput * surface.Albedo * (emission * emissionScale);
            break;
        }

        if (transmission > 0.5)
        {
            bool entering = dot(surface.GeometricNormal, direction) < 0.0;
            float3 orientedNormal = entering ? surface.Normal : -surface.Normal;
            float3 orientedGeo = entering ? surface.GeometricNormal : -surface.GeometricNormal;

            if (surface.Roughness > 0.045)
            {
                orientedNormal = SampleGGXHalfVector(orientedNormal, surface.Roughness, rng);
            }

            float cosTheta = saturate(dot(-direction, orientedNormal));
            float fresnel = DielectricFresnelSchlick(cosTheta, ior);
            float eta = ior <= 1.04 ? 1.0 : (entering ? (1.0 / ior) : ior);
            float3 refractedDir = refract(direction, orientedNormal, eta);
            bool totalInternalReflection = dot(refractedDir, refractedDir) < 1e-4;

            if (totalInternalReflection || NextRandom(rng) < fresnel)
            {
                direction = normalize(reflect(direction, orientedNormal));
                origin = OffsetRayOrigin(surface.Position, orientedGeo, direction);
                if (bounce == 0)
                {
                    primaryTookSpecular = true;
                }
            }
            else
            {
                direction = normalize(refractedDir);
                origin = OffsetRayOrigin(surface.Position, orientedGeo, direction);
                throughput *= lerp(float3(1.0, 1.0, 1.0), surface.Albedo, entering ? 0.55 : 0.25);
                if (bounce == 0)
                {
                    specularHitDistance = 0.0;
                }
            }

            lastBounceWasDelta = surface.Roughness < 0.08;
            continue;
        }

        if (dot(surface.GeometricNormal, direction) > 0.0)
        {
            surface.Normal = -surface.Normal;
            surface.GeometricNormal = -surface.GeometricNormal;
        }

        float3 V = -direction;

        radiance += throughput * SampleDirectLighting(pathTracing.Scene, surface, V, rng);

        if (bounce == maxBounces)
        {
            break;
        }

        float specProb;
        float coatProb;
        float diffProb;
        ComputeLobeProbabilities(surface, V, specProb, coatProb, diffProb);

        float lobePick = NextRandom(rng);
        float3 nextDir;
        float NdotV = max(dot(surface.Normal, V), 1e-3);
        float3 F0 = lerp(float3(0.04, 0.04, 0.04), surface.Albedo, surface.Metallic);

        if (lobePick < coatProb)
        {
            float3 H = SampleGGXHalfVector(surface.Normal, surface.ClearcoatRoughness, rng);
            nextDir = reflect(-V, H);
            float NdotL = dot(surface.Normal, nextDir);
            if (NdotL <= 0.0)
            {
                break;
            }

            float NdotH = max(dot(surface.Normal, H), 1e-4);
            float HdotV = max(dot(H, V), 1e-4);
            float Fc = (0.04 + 0.96 * pow(saturate(1.0 - HdotV), 5.0)) * surface.Clearcoat;
            float Gc = GeometrySmith(NdotV, NdotL, surface.ClearcoatRoughness);
            float3 coatWeight = float3(Fc * Gc * HdotV / max(NdotV * NdotH * coatProb, 1e-4));
            throughput *= min(coatWeight, float3(8.0));

            if (bounce == 0)
            {
                primaryTookSpecular = true;
            }

            lastBounceWasDelta = surface.ClearcoatRoughness < 0.07;
        }
        else if (lobePick < (coatProb + specProb))
        {
            float3 H = SampleGGXHalfVector(surface.Normal, surface.Roughness, rng);
            nextDir = reflect(-V, H);
            float NdotL = dot(surface.Normal, nextDir);
            if (NdotL <= 0.0)
            {
                break;
            }

            float NdotH = max(dot(surface.Normal, H), 1e-4);
            float HdotV = max(dot(H, V), 1e-4);
            float3 F = FresnelSchlick(HdotV, F0);
            float G = GeometrySmith(NdotV, NdotL, surface.Roughness);
            float3 specThroughput = (F * G * HdotV) / max(NdotV * NdotH * specProb, 1e-4);
            throughput *= min(specThroughput, float3(8.0));

            if (bounce == 0)
            {
                primaryTookSpecular = true;
            }

            lastBounceWasDelta = surface.Roughness < 0.07;
        }
        else
        {
            nextDir = SampleCosineHemisphere(surface.Normal, rng);
            float3 H = normalize(V + nextDir);
            float HdotV = max(dot(H, V), 0.0);
            float3 F = FresnelSchlick(HdotV, F0);
            float3 kD = (1.0 - F) * (1.0 - surface.Metallic);
            throughput *= (kD * surface.Albedo) / max(diffProb, 1e-4);
            lastBounceWasDelta = false;
        }

        origin = OffsetRayOrigin(surface.Position, surface.GeometricNormal, nextDir);
        direction = nextDir;

        if (bounce >= 1)
        {
            float survival = clamp(max(throughput.r, max(throughput.g, throughput.b)), 0.08, 0.95);
            if (NextRandom(rng) > survival)
            {
                break;
            }

            throughput /= survival;
        }
    }

    pathTracing.Color[pixel] = float4(min(max(radiance, 0.0), float3(28.0)), 1.0);

    if ((pathTracing.Flags & FlagHasReactiveMask) != 0u)
    {
        pathTracing.ReactiveMask[pixel] = saturate(reactivity);
    }

    if ((pathTracing.Flags & FlagHasRayReconstructionGuides) != 0u)
    {
        pathTracing.SpecularHitDistance[pixel] = primaryIsDynamic ? 0.0 : max(specularHitDistance, 0.0);
    }
}
