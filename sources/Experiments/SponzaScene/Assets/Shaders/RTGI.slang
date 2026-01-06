static const float PI = 3.14159265359;
static const float INV_PI = 0.31830988618;
static const float EPSILON = 0.0001;
static const float RAY_BIAS = 0.05;
static const float RAY_MIN_T = 0.01;
static const float RAY_MAX_DISTANCE = 100.0;
static const uint RAYS_PER_PIXEL = 1;

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
    float4x4 ViewProjection;

    DirectionalLight DirectionalLight;

    uint FrameIndex;

    uint Width;

    uint Height;
};

struct GIPayload
{
    float3 Color;

    float HitDistance;
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

float3 CalculateSkyColor(float3 direction, float3 sunDirection, float3 sunColor, float sunIntensity)
{
    float3 sunDir = SafeNormalize(-sunDirection);
    float sunHeight = sunDir.y;

    float dayFactor = saturate(sunHeight * 2.0);
    float nightFactor = saturate(-sunHeight * 3.0);

    float3 zenithColor = lerp(lerp(float3(0.1, 0.4, 1.0), float3(0.38, 0.22, 0.58) + sunColor * 0.32, 1.0 - dayFactor),
                              float3(0.005, 0.005, 0.015),
                              nightFactor);
    float3 horizonColor = lerp(lerp(float3(0.5, 0.7, 1.0), sunColor * float3(1.0, 0.8, 0.55), 1.0 - dayFactor),
                               float3(0.015, 0.015, 0.04),
                               nightFactor);

    float t = pow(saturate(direction.y * 0.5 + 0.5), 0.35);

    return lerp(horizonColor, zenithColor, t) * (0.6 + dayFactor * 0.4) * sunIntensity * 0.32;
}

bool ReprojectToScreen(float3 worldPos, float4x4 viewProj, out float2 screenUV, out float projDepth)
{
    float4 clipPos = mul(float4(worldPos, 1.0), viewProj);
    clipPos.xyz /= clipPos.w;

    screenUV = float2(clipPos.x * 0.5 + 0.5, -clipPos.y * 0.5 + 0.5);
    projDepth = clipPos.z;

    return all(screenUV >= 0.0) && all(screenUV <= 1.0) && projDepth > 0.0 && projDepth < 1.0;
}

float3 CalculatePointLightContribution(float3 worldPos, float3 normal)
{
    float3 totalLight = float3(0.0);
    uint pointLightCount = pointLights.getCount();

    for (uint i = 0; i < pointLightCount; i++)
    {
        PointLight light = pointLights[i];

        float3 toLight = light.Position - worldPos;
        float distance = length(toLight);

        if (distance > light.Radius)
        {
            continue;
        }

        float3 L = toLight / distance;
        float NdotL = max(dot(normal, L), 0.0);

        if (NdotL <= 0.0)
        {
            continue;
        }

        float distanceRatio = distance / light.Radius;
        float distanceRatio2 = distanceRatio * distanceRatio;
        float radiusAttenuation = saturate(1.0 - distanceRatio2 * distanceRatio2);
        radiusAttenuation *= radiusAttenuation;

        float physicalFalloff = 1.0 / max(distance * distance, EPSILON);
        float attenuation = radiusAttenuation * physicalFalloff;

        totalLight += light.Color * light.Intensity * attenuation * NdotL;
    }

    return totalLight;
}

[shader("raygeneration")]
void RayGen()
{
    uint2 pixelCoord = DispatchRaysIndex().xy;
    float2 uv = (float2(pixelCoord) + 0.5) / float2(constants.Width, constants.Height);

    float3 worldPos = positionTexture.SampleLevel(linearSampler, uv, 0).xyz;
    float3 normal = normalTexture.SampleLevel(linearSampler, uv, 0).xyz;
    float3 albedo = albedoTexture.SampleLevel(linearSampler, uv, 0).rgb;
    float depth = depthTexture.SampleLevel(linearSampler, uv, 0).r;

    if (depth < EPSILON || dot(normal, normal) < EPSILON)
    {
        outputTexture[pixelCoord] = float4(0.0, 0.0, 0.0, 1.0);
        return;
    }

    normal = SafeNormalize(normal);

    uint seed = pixelCoord.x + pixelCoord.y * constants.Width + constants.FrameIndex * 719393;

    float3 indirectLight = float3(0.0);

    for (uint i = 0; i < RAYS_PER_PIXEL; i++)
    {
        float3 rayDir = CosineSampleHemisphere(normal, seed);

        float NdotR = dot(normal, rayDir);
        float adaptiveBias = RAY_BIAS / max(NdotR, 0.1);

        float3 rayOrigin = worldPos + normal * adaptiveBias + rayDir * RAY_MIN_T;

        RayDesc ray;
        ray.Origin = rayOrigin;
        ray.Direction = rayDir;
        ray.TMin = RAY_MIN_T;
        ray.TMax = RAY_MAX_DISTANCE;

        GIPayload payload;
        payload.Color = float3(0.0);
        payload.HitDistance = -1.0;

        TraceRay(tlas, RAY_FLAG_CULL_BACK_FACING_TRIANGLES, 0xFF, 0, 0, 0, ray, payload);

        if (payload.HitDistance > 0.0 && payload.HitDistance < RAY_MIN_T * 2.0)
        {
            payload.Color = CalculateSkyColor(rayDir,
                                              constants.DirectionalLight.Direction,
                                              constants.DirectionalLight.Color,
                                              constants.DirectionalLight.Intensity);
        }

        indirectLight += payload.Color;
    }

    indirectLight /= float(RAYS_PER_PIXEL);

    float3 giColor = indirectLight * albedo;

    outputTexture[pixelCoord] = float4(giColor, 1.0);
}

[shader("miss")]
void Miss(inout GIPayload payload)
{
    float3 rayDir = WorldRayDirection();

    payload.Color = CalculateSkyColor(rayDir,
                                      constants.DirectionalLight.Direction,
                                      constants.DirectionalLight.Color,
                                      constants.DirectionalLight.Intensity);
    payload.HitDistance = -1.0;
}

[shader("closesthit")]
void ClosestHit(inout GIPayload payload, BuiltInTriangleIntersectionAttributes attribs)
{
    float3 hitPos = WorldRayOrigin() + WorldRayDirection() * RayTCurrent();

    float2 hitScreenUV;
    float hitProjDepth;

    if (ReprojectToScreen(hitPos, constants.ViewProjection, hitScreenUV, hitProjDepth))
    {
        float3 hitAlbedo = albedoTexture.SampleLevel(linearSampler, hitScreenUV, 0).rgb;
        float3 hitNormal = normalTexture.SampleLevel(linearSampler, hitScreenUV, 0).xyz;
        float hitDepth = depthTexture.SampleLevel(linearSampler, hitScreenUV, 0).r;

        float3 gbufferPos = positionTexture.SampleLevel(linearSampler, hitScreenUV, 0).xyz;
        float distToGBuffer = length(hitPos - gbufferPos);

        if (hitDepth > EPSILON && distToGBuffer < 10.0)
        {
            hitNormal = SafeNormalize(hitNormal);

            float3 sunDir = SafeNormalize(-constants.DirectionalLight.Direction);
            float NdotL = max(dot(hitNormal, sunDir), 0.0);

            float3 directLight = constants.DirectionalLight.Color * constants.DirectionalLight.Intensity * NdotL * 0.3;

            float3 pointLightContrib = CalculatePointLightContribution(hitPos, hitNormal) * 0.5;

            float3 ambient = CalculateSkyColor(hitNormal,
                                               constants.DirectionalLight.Direction,
                                               constants.DirectionalLight.Color,
                                               constants.DirectionalLight.Intensity) *
                             0.5;

            payload.Color = hitAlbedo * (directLight + pointLightContrib + ambient);
            payload.HitDistance = RayTCurrent();

            return;
        }
    }

    payload.Color = CalculateSkyColor(WorldRayDirection(),
                                      constants.DirectionalLight.Direction,
                                      constants.DirectionalLight.Color,
                                      constants.DirectionalLight.Intensity) *
                    0.5;
    payload.HitDistance = RayTCurrent();
}
