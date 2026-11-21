#include "PathTracingCommon.slang"

static const int MaxPathBounces = 3;

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

    uint Reset;
    uint3 Padding;

    DescriptorHandle<RaytracingAccelerationStructure> Scene;

    DescriptorHandle<StructuredBuffer<SceneVertex>> Vertices;

    DescriptorHandle<StructuredBuffer<uint>> Indices;

    DescriptorHandle<StructuredBuffer<SceneMaterial>> Materials;

    DescriptorHandle<StructuredBuffer<SceneInstance>> Instances;

    DescriptorHandle<RWTexture2D<uint4>> VisibilityStorage;

    DescriptorHandle<Texture2D<uint4>> VisibilitySampled;

    DescriptorHandle<RWTexture2D<float4>> ReflectionGuideStorage;

    DescriptorHandle<Texture2D<float4>> ReflectionGuideSampled;

    DescriptorHandle<Texture2D<float>> PreviousLinearDepth;

    DescriptorHandle<RWTexture2D<float>> CurrentLinearDepthStorage;

    DescriptorHandle<Texture2D<float>> CurrentLinearDepthSampled;

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

bool TraceSurfaceHit(float3 origin, float3 direction, out SurfaceHit surface, out float hitDistance, bool flipToRay)
{
    uint instanceIndex;
    uint primitiveIndex;
    float2 barycentrics;
    if (!TraceClosestHit(origin, direction, instanceIndex, primitiveIndex, barycentrics, hitDistance))
    {
        surface = (SurfaceHit)0;

        return false;
    }

    surface = ReconstructSurface(instanceIndex, primitiveIndex, barycentrics, direction, pathTracing.Vertices,
                                 pathTracing.Indices, pathTracing.Materials, pathTracing.Instances, flipToRay);

    return true;
}

float2 ComputeVirtualReflectionMotion(SurfaceHit baseSurface, SurfaceHit reflectedSurface)
{
    SceneInstance reflectedInstance = pathTracing.Instances[reflectedSurface.InstanceIndex];
    float3 previousReflectedPos =
        mul(float4(reflectedSurface.ObjectPosition, 1.0), reflectedInstance.PreviousObjectToWorld).xyz;
    float3 reflectedDelta = reflectedSurface.Position - previousReflectedPos;
    float4 currentClip = mul(float4(baseSurface.Position, 1.0), pathTracing.ViewProjection);
    float4 previousClip = mul(float4(baseSurface.Position - reflectedDelta, 1.0), pathTracing.PreviousViewProjection);

    return ComputeMotionVector(currentClip, previousClip);
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

    for (int dy = -1; dy <= 1; dy++)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            int2 samplePos = clamp(previousCenter + int2(dx, dy), int2(0, 0), maxPixel);
            float rawPrev = pathTracing.PreviousLinearDepth.Load(int3(samplePos, 0));
            maxPrevDepth = max(maxPrevDepth, abs(rawPrev));

            if (rawPrev < 0.0)
            {
                wasDynamicNearby = true;
            }
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

SurfaceHit EvaluateGuideReactivity(uint2 pixel, uint4 vis, float3 direction, out float reactivity,
                                   out bool isPureMirror, out float3 reflectedTint, out float specularHitDistance)
{
    uint instanceIndex = vis.x & 0xFFFFu;
    float glassReactivity = float((vis.x >> 16u) & 0xFFFFu) * (1.0 / 65535.0);
    uint primitiveIndex = vis.y;
    float2 barycentrics = UnpackBarycentrics(vis.z);

    SurfaceHit surface =
        ReconstructSurface(instanceIndex, primitiveIndex, barycentrics, direction, pathTracing.Vertices,
                           pathTracing.Indices, pathTracing.Materials, pathTracing.Instances, true);

    float4 aux = pathTracing.ReflectionGuideSampled.Load(int3(pixel, 0));
    specularHitDistance = aux.x;
    float2 stochasticReflMotion = aux.yz;
    bool hasStochasticDynamicRefl = aux.w > 0.5;

    float signedDepth = pathTracing.CurrentLinearDepthSampled.Load(int3(pixel, 0));
    float currentLinearW = abs(signedDepth);
    bool isDynamicNow = signedDepth < 0.0;

    SceneInstance inst = pathTracing.Instances[instanceIndex];
    float3 prevSurfacePos = mul(float4(surface.ObjectPosition, 1.0), inst.PreviousObjectToWorld).xyz;
    float4 currentClip = mul(float4(surface.Position, 1.0), pathTracing.ViewProjection);
    float4 previousClip = mul(float4(prevSurfacePos, 1.0), pathTracing.PreviousViewProjection);
    float2 effectiveMotion = ComputeMotionVector(currentClip, previousClip);

    reflectedTint = float3(1.0, 1.0, 1.0);
    isPureMirror =
        surface.IsDynamic == 0u && surface.Transmission < 0.08 && surface.Metallic > 0.85 && surface.Roughness <= 0.06;

    if (isPureMirror)
    {
        float3 reflDir = reflect(direction, surface.Normal);
        if (dot(surface.Normal, reflDir) > 0.0)
        {
            SurfaceHit mirrorHit;
            float mirrorDist;
            if (TraceSurfaceHit(OffsetRayOrigin(surface.Position, surface.GeometricNormal, reflDir), reflDir, mirrorHit,
                                mirrorDist, true))
            {
                specularHitDistance = min(mirrorDist, MissDistance);
                reflectedTint = mirrorHit.Albedo;

                if (mirrorHit.IsDynamic != 0u)
                {
                    SceneInstance mirrorInst = pathTracing.Instances[mirrorHit.InstanceIndex];
                    float3 prevMirrorPos =
                        mul(float4(mirrorHit.ObjectPosition, 1.0), mirrorInst.PreviousObjectToWorld).xyz;
                    float3 delta = mirrorHit.Position - prevMirrorPos;
                    float4 prevVirtualClip =
                        mul(float4(surface.Position - delta, 1.0), pathTracing.PreviousViewProjection);
                    effectiveMotion = ComputeMotionVector(currentClip, prevVirtualClip);
                    pathTracing.MotionVectors[pixel] = effectiveMotion;
                    isDynamicNow = true;
                    specularHitDistance = 0.0;
                }
            }
            else
            {
                specularHitDistance = MissDistance;
            }
        }
    }

    reactivity = EvaluateDisocclusionReactivity(pixel, pathTracing.RenderSize, effectiveMotion, currentLinearW,
                                                isDynamicNow, pathTracing.Reset != 0u);

    reactivity = max(reactivity, glassReactivity);

    if (hasStochasticDynamicRefl)
    {
        float reflSpeed = length(stochasticReflMotion * pathTracing.RenderSize);
        reactivity = max(reactivity, saturate(reflSpeed * 1.8) * (1.0 - saturate(surface.Roughness * 2.5)));
    }

    reactivity = saturate(reactivity);

    return surface;
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

        pathTracing.VisibilityStorage[pixel] = uint4(InvalidIndex, 0u, 0u, 0u);
        pathTracing.Depth[pixel] = 1.0;
        pathTracing.MotionVectors[pixel] = ComputeMotionVector(currentSkyClip, previousSkyClip);
        pathTracing.CurrentLinearDepthStorage[pixel] = MissDistance;

        return;
    }

    SurfaceHit primarySurface =
        ReconstructSurface(primaryInstance, primaryPrimitive, primaryBarycentrics, direction, pathTracing.Vertices,
                           pathTracing.Indices, pathTracing.Materials, pathTracing.Instances, true);

    float3 anchorWorldPos = primarySurface.Position;
    float3 anchorPrevWorldPos =
        mul(float4(primarySurface.ObjectPosition, 1.0), pathTracing.Instances[primaryInstance].PreviousObjectToWorld)
            .xyz;
    bool anchorIsDynamic = primarySurface.IsDynamic != 0u;
    float glassReactivity = 0.0;

    if (primarySurface.Transmission > 0.08 && primarySurface.Roughness < 0.25)
    {
        float cosTheta = abs(dot(primarySurface.Normal, -direction));
        float fresnel = DielectricFresnelSchlick(cosTheta, primarySurface.Ior);
        glassReactivity = saturate(0.58 + fresnel * 0.38 + (anchorIsDynamic ? 0.30 : 0.0));

        uint currentInst = primaryInstance;
        uint currentPrim = primaryPrimitive;
        float2 currentBary = primaryBarycentrics;
        float3 transDir = direction;
        float totalTravel = primaryDistance;
        bool foundBackground = false;
        SurfaceHit backgroundSurface = primarySurface;

        for (int layer = 0; layer < 2; layer++)
        {
            SurfaceHit rawHit =
                ReconstructSurface(currentInst, currentPrim, currentBary, transDir, pathTracing.Vertices,
                                   pathTracing.Indices, pathTracing.Materials, pathTracing.Instances, false);

            bool entering = dot(rawHit.GeometricNormal, transDir) < 0.0;
            float3 orientedNormal = entering ? rawHit.Normal : -rawHit.Normal;
            float3 orientedGeoNormal = entering ? rawHit.GeometricNormal : -rawHit.GeometricNormal;
            float eta = rawHit.Ior <= 1.04 ? 1.0 : (entering ? (1.0 / rawHit.Ior) : rawHit.Ior);
            float3 refracted = refract(transDir, orientedNormal, eta);

            if (dot(refracted, refracted) < 1e-4)
            {
                refracted = transDir;
            }

            transDir = normalize(refracted);
            float3 transOrigin = OffsetRayOrigin(rawHit.Position, orientedGeoNormal, transDir);

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
            currentInst = nextInstance;
            currentPrim = nextPrimitive;
            currentBary = nextBary;
            backgroundSurface =
                ReconstructSurface(nextInstance, nextPrimitive, nextBary, transDir, pathTracing.Vertices,
                                   pathTracing.Indices, pathTracing.Materials, pathTracing.Instances, true);

            if (backgroundSurface.Transmission <= 0.08)
            {
                foundBackground = true;
                break;
            }
        }

        if (foundBackground && !anchorIsDynamic)
        {
            SceneInstance bgInst = pathTracing.Instances[backgroundSurface.InstanceIndex];
            float3 bgPrevWorld = mul(float4(backgroundSurface.ObjectPosition, 1.0), bgInst.PreviousObjectToWorld).xyz;
            float3 bgDelta = backgroundSurface.Position - bgPrevWorld;
            float3 virtualCurrent = origin + direction * min(totalTravel, RayMaxDistance);

            anchorWorldPos = virtualCurrent;
            anchorPrevWorldPos = virtualCurrent - bgDelta;
            anchorIsDynamic = backgroundSurface.IsDynamic != 0u;
        }
    }

    uint glassBits = uint(round(saturate(glassReactivity) * 65535.0));
    uint packedInstanceAndFlags = (primaryInstance & 0xFFFFu) | ((glassBits & 0xFFFFu) << 16u);

    float4 currentClip = mul(float4(anchorWorldPos, 1.0), pathTracing.ViewProjection);
    float4 previousClip = mul(float4(anchorPrevWorldPos, 1.0), pathTracing.PreviousViewProjection);
    float2 motionVector = ComputeMotionVector(currentClip, previousClip);
    float clipDepth = saturate(currentClip.z / max(currentClip.w, 1e-5));
    float cameraDistance = length(anchorWorldPos - origin);
    float signedLinearDepth = anchorIsDynamic ? -cameraDistance : cameraDistance;

    pathTracing.VisibilityStorage[pixel] =
        uint4(packedInstanceAndFlags, primaryPrimitive, PackBarycentrics(primaryBarycentrics), asuint(primaryDistance));
    pathTracing.Depth[pixel] = clipDepth;
    pathTracing.MotionVectors[pixel] = motionVector;
    pathTracing.CurrentLinearDepthStorage[pixel] = signedLinearDepth;
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
    if (vis.x == InvalidIndex)
    {
        float3 skyColor = EvaluateSkyRadiance(direction, true);
        pathTracing.Color[pixel] = float4(min(skyColor, float3(24.0)), 1.0);
        pathTracing.ReflectionGuideStorage[pixel] = float4(MissDistance, 0.0, 0.0, 0.0);

        return;
    }

    uint primaryInstance = vis.x & 0xFFFFu;
    uint primaryPrimitive = vis.y;
    float2 primaryBary = UnpackBarycentrics(vis.z);
    float primaryDistance = asfloat(vis.w);

    SurfaceHit firstSurface =
        ReconstructSurface(primaryInstance, primaryPrimitive, primaryBary, direction, pathTracing.Vertices,
                           pathTracing.Indices, pathTracing.Materials, pathTracing.Instances, false);

    uint rng = InitRandomSeed(pixel, renderSize, pathTracing.FrameIndex);
    float3 throughput = float3(1.0, 1.0, 1.0);
    float3 radiance = 0.0;
    float specularHitDistance = 0.0;
    float2 reflectionMotion = 0.0;
    float hasDynamicReflection = 0.0;
    bool primaryTookSpecular = false;
    bool lastBounceWasDelta = false;

    for (int bounce = 0; bounce <= MaxPathBounces; bounce++)
    {
        SurfaceHit surface = firstSurface;
        float hitDist = primaryDistance;
        bool hasHit = true;

        if (bounce > 0)
        {
            hasHit = TraceSurfaceHit(origin, direction, surface, hitDist, false);

            if (bounce == 1 && primaryTookSpecular)
            {
                specularHitDistance = hasHit ? min(hitDist, MissDistance) : MissDistance;
                if (hasHit && surface.IsDynamic != 0u && firstSurface.IsDynamic == 0u)
                {
                    reflectionMotion = ComputeVirtualReflectionMotion(firstSurface, surface);
                    hasDynamicReflection = 1.0;
                }
            }
        }

        if (!hasHit)
        {
            bool showSun = bounce == 0 || lastBounceWasDelta;
            float3 env = EvaluateSkyRadiance(direction, showSun);
            radiance += throughput * env;
            break;
        }

        if (surface.Emission > 0.0)
        {
            float emissionScale = (bounce == 0 || lastBounceWasDelta) ? 1.0 : 0.35;
            radiance += throughput * surface.Albedo * (surface.Emission * emissionScale);
            break;
        }

        if (surface.Transmission > 0.5)
        {
            bool entering = dot(surface.GeometricNormal, direction) < 0.0;
            float3 orientedNormal = entering ? surface.Normal : -surface.Normal;
            float3 orientedGeo = entering ? surface.GeometricNormal : -surface.GeometricNormal;

            if (surface.Roughness > 0.045)
            {
                orientedNormal = SampleGGXHalfVector(orientedNormal, surface.Roughness, rng);
            }

            float cosTheta = saturate(dot(-direction, orientedNormal));
            float fresnel = DielectricFresnelSchlick(cosTheta, surface.Ior);
            float eta = surface.Ior <= 1.04 ? 1.0 : (entering ? (1.0 / surface.Ior) : surface.Ior);
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

        if (bounce == MaxPathBounces)
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

    float3 clampedColor = min(max(radiance, 0.0), float3(28.0));
    pathTracing.Color[pixel] = float4(clampedColor, 1.0);
    pathTracing.ReflectionGuideStorage[pixel] =
        float4(specularHitDistance, reflectionMotion.x, reflectionMotion.y, hasDynamicReflection);
}

[shader("compute")]
[numthreads(8, 8, 1)]
void ReactiveMaskCS(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadID.xy;
    uint2 renderSize = uint2(pathTracing.RenderSize);

    if (any(pixel >= renderSize))
    {
        return;
    }

    uint4 vis = pathTracing.VisibilitySampled.Load(int3(pixel, 0));
    if (vis.x == InvalidIndex)
    {
        pathTracing.ReactiveMask[pixel] = 0.0;

        return;
    }

    float3 direction = GetCameraRayDirection(pixel);

    float reactivity;
    bool isPureMirror;
    float3 reflectedTint;
    float specularHitDistance;
    EvaluateGuideReactivity(pixel, vis, direction, reactivity, isPureMirror, reflectedTint, specularHitDistance);

    pathTracing.ReactiveMask[pixel] = reactivity;
}

[shader("compute")]
[numthreads(8, 8, 1)]
void RayReconstructionGuidesCS(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadID.xy;
    uint2 renderSize = uint2(pathTracing.RenderSize);

    if (any(pixel >= renderSize))
    {
        return;
    }

    float3 direction = GetCameraRayDirection(pixel);

    uint4 vis = pathTracing.VisibilitySampled.Load(int3(pixel, 0));
    if (vis.x == InvalidIndex)
    {
        pathTracing.ReactiveMask[pixel] = 0.0;
        pathTracing.DiffuseAlbedo[pixel] = float4(0.35, 0.35, 0.35, 1.0);
        pathTracing.SpecularAlbedo[pixel] = float4(0.0, 0.0, 0.0, 1.0);
        pathTracing.Normals[pixel] = float4(-direction, 0.0);
        pathTracing.Roughness[pixel] = 1.0;
        pathTracing.SpecularHitDistance[pixel] = MissDistance;

        return;
    }

    float reactivity;
    bool isPureMirror;
    float3 reflectedTint;
    float specularHitDistance;
    SurfaceHit surface =
        EvaluateGuideReactivity(pixel, vis, direction, reactivity, isPureMirror, reflectedTint, specularHitDistance);

    pathTracing.ReactiveMask[pixel] = reactivity;

    float effectiveRoughness =
        surface.Clearcoat > 0.5 ? min(surface.Roughness, surface.ClearcoatRoughness) : surface.Roughness;

    float3 F0 = surface.Transmission > 0.5 ? float3(0.06, 0.06, 0.06)
                                           : lerp(float3(0.04, 0.04, 0.04), surface.Albedo, surface.Metallic);
    float3 specAlbedo = EstimateEnvSpecularAlbedo(F0, effectiveRoughness, dot(surface.Normal, -direction));
    specAlbedo *= lerp(float3(1.0, 1.0, 1.0), reflectedTint, isPureMirror ? surface.Metallic : 0.0);

    float3 diffAlbedo = surface.Transmission > 0.5 ? surface.Albedo * 0.25 : surface.Albedo * (1.0 - surface.Metallic);

    if (surface.IsDynamic != 0u)
    {
        specularHitDistance = 0.0;
    }

    pathTracing.DiffuseAlbedo[pixel] = float4(diffAlbedo, 1.0);
    pathTracing.SpecularAlbedo[pixel] = float4(specAlbedo, 1.0);
    pathTracing.Normals[pixel] = float4(surface.Normal, 0.0);
    pathTracing.Roughness[pixel] = effectiveRoughness;
    pathTracing.SpecularHitDistance[pixel] = max(specularHitDistance, 0.0);
}
