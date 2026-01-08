static const float PI = 3.14159265359;
static const float INV_PI = 0.31830988618;
static const float EPSILON = 0.0001;

static const uint MAX_ACCUMULATED_FRAMES = 1024;

static const float RAY_MAX_DISTANCE = 200.0;
static const float SHADOW_RAY_DISTANCE = 500.0;
static const float GBUFFER_DISTANCE_THRESHOLD = 3.0;

static const float RAY_ORIGIN_NORMAL_OFFSET = 0.1;
static const float RAY_TMIN = 0.01;

struct DirectionalLight
{
    float3 Direction;

    float Intensity;

    float3 Color;
};

struct PointLight
{
    float3 Position;

    float Radius;

    float3 Color;

    float Intensity;
};

struct RTGIConstants
{
    uint Width;

    uint Height;

    uint FrameIndex;

    float Intensity;

    float4x4 ViewProjection;

    DirectionalLight DirectionalLight;
};

struct GIPayload
{
    float3 Radiance;
};

struct ShadowPayload
{
    bool InShadow;
};

ConstantBuffer<RTGIConstants> constants;
RaytracingAccelerationStructure tlas;
StructuredBuffer<PointLight> pointLights;
Texture2D albedoTexture;
Texture2D normalTexture;
Texture2D positionTexture;
Texture2D depthTexture;
RWTexture2D<float4> outputTexture;
SamplerState linearSampler;

// ============================================
// Utility
// ============================================

float3 SafeNormalize(float3 v)
{
    float len = dot(v, v);

    return len > EPSILON ? v * rsqrt(len) : float3(0.0, 1.0, 0.0);
}

uint WangHash(uint seed)
{
    seed = (seed ^ 61) ^ (seed >> 16);
    seed *= 9;
    seed = seed ^ (seed >> 4);
    seed *= 0x27d4eb2d;
    seed = seed ^ (seed >> 15);

    return seed;
}

float RandomFloat(inout uint seed)
{
    seed = WangHash(seed);

    return float(seed) / 4294967295.0;
}

float2 RandomFloat2(inout uint seed)
{
    return float2(RandomFloat(seed), RandomFloat(seed));
}

// ============================================
// Sampling
// ============================================

float3 CosineSampleHemisphere(float3 normal, inout uint seed)
{
    float2 u = RandomFloat2(seed);

    float r = sqrt(u.x);
    float phi = 2.0 * PI * u.y;

    float3 localDir = float3(r * cos(phi), r * sin(phi), sqrt(max(0.0, 1.0 - u.x)));

    float3 up = abs(normal.y) < 0.999 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
    float3 tangent = normalize(cross(up, normal));
    float3 bitangent = cross(normal, tangent);

    return normalize(tangent * localDir.x + bitangent * localDir.y + normal * localDir.z);
}

float3 OffsetRayOrigin(float3 position, float3 normal)
{
    return position + normal * RAY_ORIGIN_NORMAL_OFFSET;
}

bool ReprojectToScreen(float3 worldPos, float4x4 viewProj, out float2 screenUV)
{
    float4 clipPos = mul(float4(worldPos, 1.0), viewProj);

    if (clipPos.w <= 0.0)
    {
        screenUV = float2(-1.0);

        return false;
    }

    clipPos.xyz /= clipPos.w;
    screenUV = float2(clipPos.x * 0.5 + 0.5, -clipPos.y * 0.5 + 0.5);

    return all(screenUV >= 0.0) && all(screenUV <= 1.0) && clipPos.z > 0.0 && clipPos.z < 1.0;
}

// ============================================
// Sky
// ============================================

float3 GetSkyRadiance(float3 direction)
{
    float3 sunDir = SafeNormalize(-constants.DirectionalLight.Direction);
    float sunHeight = max(sunDir.y, 0.0);
    float elevation = direction.y;

    float3 skyBlue = float3(0.4, 0.6, 1.0);
    float3 horizonColor = float3(0.7, 0.78, 0.9);
    float3 groundColor = float3(0.25, 0.22, 0.2);

    float t = pow(saturate(elevation * 0.5 + 0.5), 0.4);
    float3 skyColor = lerp(horizonColor, skyBlue, t);

    if (elevation < 0.0)
    {
        float groundBlend = saturate(-elevation * 2.0);

        skyColor = lerp(skyColor, groundColor, groundBlend);
    }

    float baseBrightness = 0.25 + sunHeight * 0.2;

    return skyColor * constants.DirectionalLight.Intensity * baseBrightness;
}

float3 GetAmbientRadiance(float3 normal)
{
    float3 skyUp = GetSkyRadiance(float3(0.0, 1.0, 0.0));
    float3 skyHorizon = GetSkyRadiance(float3(1.0, 0.0, 0.0));
    float3 skyDown = GetSkyRadiance(float3(0.0, -1.0, 0.0)) * 0.5;

    float upWeight = max(normal.y, 0.0);
    float downWeight = max(-normal.y, 0.0);
    float sideWeight = 1.0 - abs(normal.y);

    float3 ambient = skyUp * upWeight + skyDown * downWeight + skyHorizon * sideWeight;

    return ambient * 0.8;
}

// ============================================
// Shadow Testing
// ============================================

bool TraceShadowRay(float3 origin, float3 direction, float maxDistance)
{
    RayDesc shadowRay;
    shadowRay.Origin = origin;
    shadowRay.Direction = direction;
    shadowRay.TMin = RAY_TMIN;
    shadowRay.TMax = maxDistance;

    ShadowPayload shadowPayload;
    shadowPayload.InShadow = true;

    TraceRay(tlas, RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH | RAY_FLAG_SKIP_CLOSEST_HIT_SHADER, 0xFF, 1, 0, 1, shadowRay, shadowPayload);

    return shadowPayload.InShadow;
}

// ============================================
// Surface Lighting
// ============================================

float3 CalculateSurfaceRadiance(float3 hitPos, float3 hitNormal, float3 hitAlbedo)
{
    float3 Lo = float3(0.0);

    float3 sunDir = SafeNormalize(-constants.DirectionalLight.Direction);
    float NdotL = max(dot(hitNormal, sunDir), 0.0);

    if (NdotL > 0.0)
    {
        float3 shadowOrigin = OffsetRayOrigin(hitPos, hitNormal);

        if (!TraceShadowRay(shadowOrigin, sunDir, SHADOW_RAY_DISTANCE))
        {
            float3 sunRadiance = constants.DirectionalLight.Color * constants.DirectionalLight.Intensity;

            Lo += (hitAlbedo * INV_PI) * sunRadiance * NdotL;
        }
    }

    float3 ambient = GetAmbientRadiance(hitNormal);
    Lo += hitAlbedo * ambient;

    uint pointLightCount = pointLights.getCount();

    for (uint i = 0; i < pointLightCount; i++)
    {
        PointLight light = pointLights[i];

        float3 toLight = light.Position - hitPos;
        float distance = length(toLight);

        if (distance > light.Radius)
        {
            continue;
        }

        float3 L = toLight / distance;
        float lightNdotL = max(dot(hitNormal, L), 0.0);

        if (lightNdotL <= 0.0)
        {
            continue;
        }

        float distanceRatio = distance / light.Radius;
        float distanceRatio2 = distanceRatio * distanceRatio;
        float radiusAttenuation = saturate(1.0 - distanceRatio2 * distanceRatio2);
        radiusAttenuation *= radiusAttenuation;

        float physicalFalloff = 1.0 / max(distance * distance, EPSILON);
        float attenuation = radiusAttenuation * physicalFalloff;

        float3 lightRadiance = light.Color * light.Intensity * attenuation;

        Lo += (hitAlbedo * INV_PI) * lightRadiance * lightNdotL;
    }

    return Lo;
}

float3 EstimateOffscreenRadiance(float3 hitPos, float3 rayDir, float hitDistance)
{
    float3 sunDir = SafeNormalize(-constants.DirectionalLight.Direction);
    float3 estimatedAlbedo = float3(0.5, 0.5, 0.5);
    float3 estimatedNormal = -rayDir;

    float NdotL = max(dot(estimatedNormal, sunDir), 0.0);

    float3 directLight = float3(0.0);

    if (NdotL > 0.0)
    {
        float3 shadowOrigin = OffsetRayOrigin(hitPos, estimatedNormal);

        if (!TraceShadowRay(shadowOrigin, sunDir, SHADOW_RAY_DISTANCE))
        {
            float3 sunRadiance = constants.DirectionalLight.Color * constants.DirectionalLight.Intensity;

            directLight = (estimatedAlbedo * INV_PI) * sunRadiance * NdotL;
        }
    }

    float3 ambient = GetAmbientRadiance(estimatedNormal);

    float distanceFade = saturate(1.0 - hitDistance / RAY_MAX_DISTANCE * 0.3);

    return (directLight + estimatedAlbedo * ambient) * distanceFade;
}

[shader("raygeneration")]
void RayGen()
{
    uint2 pixelCoord = DispatchRaysIndex().xy;
    float2 uv = (float2(pixelCoord) + 0.5) / float2(constants.Width, constants.Height);

    float3 worldPos = positionTexture.SampleLevel(linearSampler, uv, 0).xyz;
    float3 normal = normalTexture.SampleLevel(linearSampler, uv, 0).xyz;
    float depth = depthTexture.SampleLevel(linearSampler, uv, 0).r;

    if (depth < EPSILON || dot(normal, normal) < EPSILON)
    {
        outputTexture[pixelCoord] = float4(0.0, 0.0, 0.0, 1.0);

        return;
    }

    normal = SafeNormalize(normal);

    uint seed = pixelCoord.x + pixelCoord.y * constants.Width + constants.FrameIndex * 719393;

    float3 rayDir = CosineSampleHemisphere(normal, seed);
    float3 rayOrigin = OffsetRayOrigin(worldPos, normal);

    RayDesc ray;
    ray.Origin = rayOrigin;
    ray.Direction = rayDir;
    ray.TMin = RAY_TMIN;
    ray.TMax = RAY_MAX_DISTANCE;

    GIPayload payload;
    payload.Radiance = float3(0.0);

    TraceRay(tlas, RAY_FLAG_CULL_BACK_FACING_TRIANGLES, 0xFF, 0, 0, 0, ray, payload);

    float3 irradiance = payload.Radiance * constants.Intensity;

    if (constants.FrameIndex > 0)
    {
        float blendFactor = 1.0 / float(min(constants.FrameIndex + 1, MAX_ACCUMULATED_FRAMES));

        irradiance = lerp(outputTexture[pixelCoord].rgb, irradiance, blendFactor);
    }

    outputTexture[pixelCoord] = float4(irradiance, 1.0);
}

[shader("miss")]
void Miss(inout GIPayload payload)
{
    payload.Radiance = GetSkyRadiance(WorldRayDirection());
}

[shader("miss")]
void ShadowMiss(inout ShadowPayload payload)
{
    payload.InShadow = false;
}

[shader("closesthit")]
void ClosestHit(inout GIPayload payload, BuiltInTriangleIntersectionAttributes attribs)
{
    float3 hitPos = WorldRayOrigin() + WorldRayDirection() * RayTCurrent();
    float3 rayDir = WorldRayDirection();

    float2 hitScreenUV;

    if (ReprojectToScreen(hitPos, constants.ViewProjection, hitScreenUV))
    {
        float3 hitAlbedo = albedoTexture.SampleLevel(linearSampler, hitScreenUV, 0).rgb;
        float3 hitNormal = normalTexture.SampleLevel(linearSampler, hitScreenUV, 0).xyz;
        float hitDepth = depthTexture.SampleLevel(linearSampler, hitScreenUV, 0).r;
        float3 gbufferPos = positionTexture.SampleLevel(linearSampler, hitScreenUV, 0).xyz;

        float distToGBuffer = length(hitPos - gbufferPos);

        if (hitDepth > EPSILON && distToGBuffer < GBUFFER_DISTANCE_THRESHOLD)
        {
            payload.Radiance = CalculateSurfaceRadiance(hitPos, SafeNormalize(hitNormal), hitAlbedo);

            return;
        }

        if (hitDepth > EPSILON)
        {
            float blendFactor = saturate((distToGBuffer - GBUFFER_DISTANCE_THRESHOLD) / GBUFFER_DISTANCE_THRESHOLD);
            float3 estimatedNormal = SafeNormalize(lerp(hitNormal, -rayDir, blendFactor * 0.4));

            payload.Radiance = CalculateSurfaceRadiance(hitPos, estimatedNormal, hitAlbedo);

            return;
        }
    }

    payload.Radiance = EstimateOffscreenRadiance(hitPos, rayDir, RayTCurrent());
}
