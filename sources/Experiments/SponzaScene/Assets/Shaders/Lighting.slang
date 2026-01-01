static const float PI = 3.14159265359;
static const float INV_PI = 0.31830988618;
static const float EPSILON = 0.0001;

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

struct LightingConstants
{
    float4 CameraPosition;

    float4x4 InverseViewProjection;

    DirectionalLight DirectionalLight;
};

struct CSMData
{
    float4x4 View;

    float4x4 Projection;

    float NearPlane;

    float FarPlane;
}

struct SkyParams
{
    float DayFactor;

    float SunsetFactor;

    float NightFactor;

    float SunVis;

    float3 SunDir;
};

ConstantBuffer<LightingConstants> constants;
StructuredBuffer<PointLight> pointLights;
StructuredBuffer<CSMData> csmDatas;
Texture2D albedoTexture;
Texture2D normalTexture;
Texture2D positionTexture;
Texture2D metallicRoughnessTexture;
Texture2D emissiveTexture;
Texture2DArray csmTextures;
Texture2D ssaoTexture;
RWTexture2D outputTexture;
SamplerState pointSampler;
SamplerComparisonState shadowSampler;

// ============================================
// Utility
// ============================================

float3 SafeNormalize(float3 v)
{
    float len = dot(v, v);
    return len > EPSILON ? v * rsqrt(len) : float3(0.0, 1.0, 0.0);
}

SkyParams CalculateSkyParams(float3 sunDirection)
{
    SkyParams params;
    params.SunDir = SafeNormalize(-sunDirection);
    float sunHeight = params.SunDir.y;

    params.DayFactor = saturate(sunHeight * 2.0);
    params.SunsetFactor = 1.0 - params.DayFactor;
    params.SunsetFactor *= params.SunsetFactor;
    params.NightFactor = saturate(-sunHeight * 3.0);
    params.SunVis = smoothstep(-0.1, 0.15, sunHeight);

    return params;
}

float3 BlendSkyColors(float3 dayColor, float3 sunsetColor, float3 nightColor, SkyParams params)
{
    return lerp(lerp(dayColor, sunsetColor, params.SunsetFactor), nightColor, params.NightFactor);
}

// ============================================
// Shadow Mapping
// ============================================

int GetCascadeIndex(float distanceToCamera)
{
    uint cascadeCount = csmDatas.getCount();
    for (uint i = 0; i < cascadeCount; i++)
    {
        if (distanceToCamera < csmDatas[i].FarPlane)
            return int(i);
    }
    return int(cascadeCount - 1);
}

float3 WorldToShadowCoord(float3 worldPos, int cascadeIndex)
{
    float4 lightSpacePos =
        mul(mul(float4(worldPos, 1.0), csmDatas[cascadeIndex].View), csmDatas[cascadeIndex].Projection);
    lightSpacePos.xyz /= lightSpacePos.w;
    return float3(lightSpacePos.x * 0.5 + 0.5, -lightSpacePos.y * 0.5 + 0.5, lightSpacePos.z);
}

float SampleShadowPCF(float3 shadowCoord, int cascadeIndex, float2 texelSize)
{
    float shadow = 0.0;
    const int pcfRadius = 2;

    [unroll]
    for (int x = -pcfRadius; x <= pcfRadius; x++)
    {
        [unroll]
        for (int y = -pcfRadius; y <= pcfRadius; y++)
        {
            float2 offset = float2(x, y) * texelSize;
            shadow += csmTextures.SampleCmpLevelZero(shadowSampler, float3(shadowCoord.xy + offset, cascadeIndex),
                                                     shadowCoord.z);
        }
    }

    return shadow * (1.0 / ((pcfRadius * 2 + 1) * (pcfRadius * 2 + 1)));
}

float CalculateShadow(float3 worldPos, float3 cameraPos, float3 normal, float3 lightDir)
{
    uint cascadeCount = csmDatas.getCount();
    if (cascadeCount == 0)
        return 1.0;

    float distanceToCamera = length(worldPos - cameraPos);
    int cascadeIndex = GetCascadeIndex(distanceToCamera);

    float NdotL = dot(normal, -lightDir);
    float3 biasedPos = worldPos + normal * max(0.05 * (1.0 - NdotL), 0.005);

    float3 shadowCoord = WorldToShadowCoord(biasedPos, cascadeIndex);

    // Margin to avoid edge artifacts
    const float margin = 0.02;
    if (any(shadowCoord.xy < margin) || any(shadowCoord.xy > 1.0 - margin) || shadowCoord.z < 0.0 ||
        shadowCoord.z > 1.0)
        return 1.0;

    uint width, height, elements;
    csmTextures.GetDimensions(width, height, elements);
    float2 texelSize = 1.0 / float2(width, height);

    float shadow = SampleShadowPCF(shadowCoord, cascadeIndex, texelSize);

    // Cascade edge blending
    float cascadeFar = csmDatas[cascadeIndex].FarPlane;
    float blendStart = cascadeFar - (cascadeFar - csmDatas[cascadeIndex].NearPlane) * 0.1;

    if (distanceToCamera > blendStart && cascadeIndex < int(cascadeCount - 1))
    {
        float3 nextShadowCoord = WorldToShadowCoord(biasedPos, cascadeIndex + 1);

        if (all(nextShadowCoord.xy > margin) && all(nextShadowCoord.xy < 1.0 - margin) && nextShadowCoord.z > 0.0 &&
            nextShadowCoord.z < 1.0)
        {
            float nextShadow = SampleShadowPCF(nextShadowCoord, cascadeIndex + 1, texelSize);
            shadow = lerp(shadow, nextShadow, (distanceToCamera - blendStart) / (cascadeFar - blendStart));
        }
    }

    return shadow;
}

// ============================================
// Procedural Skybox
// ============================================

float3 CalculateSkybox(float3 viewDir, float3 sunDirection, float3 sunColor, float sunIntensity)
{
    viewDir = SafeNormalize(viewDir);
    SkyParams params = CalculateSkyParams(sunDirection);
    float elevation = viewDir.y;

    float3 zenithColor = BlendSkyColors(float3(0.15, 0.35, 0.75), float3(0.12, 0.08, 0.25) + sunColor * 0.1,
                                        float3(0.02, 0.02, 0.06), params);

    float3 horizonColor =
        BlendSkyColors(float3(0.5, 0.7, 0.9), sunColor * float3(1.0, 0.6, 0.3), float3(0.05, 0.05, 0.1), params);

    float3 groundColor = float3(0.15, 0.12, 0.1) * (0.3 + params.DayFactor * 0.7);
    groundColor = lerp(groundColor, groundColor * sunColor, params.SunsetFactor * 0.5);

    float skyBlend = pow(saturate(elevation), 0.4 + params.DayFactor * 0.2);
    float3 skyColor = lerp(horizonColor, zenithColor, skyBlend);

    float groundBlend = smoothstep(0.0, -0.5, elevation);
    skyColor = lerp(skyColor, groundColor, groundBlend * groundBlend);
    skyColor = lerp(skyColor, horizonColor, exp(-abs(elevation) * (8.0 + params.DayFactor * 4.0)) * 0.15);

    // Sun glow
    float sunDot = dot(viewDir, params.SunDir);
    float glowIntensity = sunIntensity * 0.2;

    float glow1 =
        pow(max(sunDot * 0.5 + 0.5, 0.0), 8.0 + params.DayFactor * 4.0) * 0.05 * (1.0 + params.SunsetFactor * 0.5);
    float glow2 = pow(max(sunDot, 0.0), 32.0 + params.DayFactor * 16.0) * 0.15 * glowIntensity;
    float glow3 = pow(max(sunDot, 0.0), 128.0 + params.DayFactor * 64.0) * 0.3 * glowIntensity;
    float glow4 = pow(max(sunDot, 0.0), 512.0 + params.DayFactor * 256.0) * 0.8 * glowIntensity;

    float discThreshold = 0.9998 + params.DayFactor * 0.0001;
    float sunDisc = smoothstep(discThreshold, discThreshold + 0.0001, sunDot) * 2.0 * glowIntensity;

    float3 glowColor = lerp(sunColor * float3(1.0, 0.6, 0.3), sunColor, params.DayFactor) * params.SunVis;
    float3 discColor = lerp(float3(1.0, 0.85, 0.6), float3(1.0, 0.98, 0.94), params.DayFactor) * params.SunVis;

    skyColor += glowColor * (glow1 + glow2) + sunColor * params.SunVis * (glow3 + glow4) + discColor * sunDisc;

    // Horizon glow
    float horizonGlow = exp(-elevation * elevation * (15.0 + params.DayFactor * 10.0));
    float3 viewHoriz = SafeNormalize(float3(viewDir.x, 0.0, viewDir.z));
    float3 sunHoriz = SafeNormalize(float3(params.SunDir.x, 0.0, params.SunDir.z));
    float horizSunDot = max(dot(viewHoriz, sunHoriz), 0.0);

    float3 warmGlow = lerp(float3(0.3, 0.25, 0.3), sunColor * 0.8, horizSunDot * params.SunsetFactor);
    skyColor += warmGlow * horizonGlow * (0.1 + params.SunsetFactor * 0.15) * (0.5 + horizSunDot * 0.8) * params.SunVis;

    // Stars
    if (params.NightFactor > 0.1 && elevation > 0.0)
    {
        float starNoise = frac(sin(dot(viewDir.xz * 100.0, float2(12.9898, 78.233))) * 43758.5453);
        skyColor += smoothstep(0.98, 1.0, starNoise) * params.NightFactor * elevation * 0.5;
    }

    return skyColor;
}

float3 SampleSkyColor(float3 direction, float3 sunDirection, float3 sunColor, float sunIntensity)
{
    SkyParams params = CalculateSkyParams(sunDirection);

    float3 zenithColor = BlendSkyColors(float3(0.2, 0.4, 0.8), float3(0.15, 0.1, 0.3) + sunColor * 0.15,
                                        float3(0.02, 0.02, 0.05), params);

    float3 horizonColor =
        BlendSkyColors(float3(0.5, 0.7, 0.9), sunColor * float3(0.8, 0.5, 0.3), float3(0.03, 0.03, 0.06), params);

    float t = pow(saturate(direction.y * 0.5 + 0.5), 0.5);
    return lerp(horizonColor, zenithColor, t) * (0.4 + params.DayFactor * 0.6) * sunIntensity * 0.25;
}

float3 GetViewDirection(float2 uv)
{
    float2 ndc = float2(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0);
    float4 worldPos = mul(float4(ndc, 1.0, 1.0), constants.InverseViewProjection);
    return SafeNormalize(worldPos.xyz / worldPos.w - constants.CameraPosition.xyz);
}

// ============================================
// PBR
// ============================================

float DistributionGGX(float NdotH, float roughness)
{
    float a2 = roughness * roughness;
    a2 *= a2;
    float denom = NdotH * NdotH * (a2 - 1.0) + 1.0;
    return a2 / (PI * denom * denom + EPSILON);
}

float GeometrySmith(float NdotV, float NdotL, float roughness)
{
    float k = (roughness + 1.0) * (roughness + 1.0) * 0.125;
    return (NdotV / (NdotV * (1.0 - k) + k)) * (NdotL / (NdotL * (1.0 - k) + k));
}

float3 FresnelSchlick(float cosTheta, float3 F0)
{
    float t = 1.0 - cosTheta;
    float t2 = t * t;
    return F0 + (1.0 - F0) * (t2 * t2 * t);
}

float3 FresnelSchlickRoughness(float cosTheta, float3 F0, float roughness)
{
    float t = 1.0 - cosTheta;
    float t2 = t * t;
    return F0 + (max(1.0 - roughness, F0) - F0) * (t2 * t2 * t);
}

float3 CalculatePBRLight(float3 L, float3 V, float3 N, float3 albedo, float metallic, float roughness, float3 radiance)
{
    float3 H = SafeNormalize(V + L);
    float3 F0 = lerp(0.04, albedo, metallic);

    float NdotV = max(dot(N, V), EPSILON);
    float NdotL = max(dot(N, L), 0.0);
    float NdotH = max(dot(N, H), 0.0);

    float D = DistributionGGX(NdotH, roughness);
    float G = GeometrySmith(NdotV, NdotL, roughness);
    float3 F = FresnelSchlick(max(dot(H, V), 0.0), F0);

    float3 specular = (D * G * F) / (4.0 * NdotV * NdotL + EPSILON);
    float3 kD = (1.0 - F) * (1.0 - metallic);

    return (kD * albedo * INV_PI + specular) * radiance * NdotL;
}

float3 CalculateAmbient(float3 N, float3 V, float3 albedo, float metallic, float roughness, float ao, float shadow,
                        float3 sunDirection, float3 sunColor, float sunIntensity)
{
    float3 F0 = lerp(0.04, albedo, metallic);
    float NdotV = max(dot(N, V), EPSILON);

    float3 F = FresnelSchlickRoughness(NdotV, F0, roughness);
    float3 kD = (1.0 - F) * (1.0 - metallic);

    float3 sunDir = SafeNormalize(-sunDirection);
    float sunHeight = saturate(sunDir.y);

    float3 skyIrradiance = SampleSkyColor(N, sunDirection, sunColor, sunIntensity);
    float3 groundBounce = sunColor * sunIntensity * 0.04 * sunHeight;

    float hemiFactor = N.y * 0.5 + 0.5;
    float shadowAmbientFactor = lerp(0.35, 1.0, shadow);

    float NdotL = max(dot(N, sunDir), 0.0);
    float3 indirectSun = sunColor * sunIntensity * 0.05 * pow(NdotL * 0.5 + 0.5, 2.0) * sunHeight * shadow;

    float3 diffuse = kD * albedo * (lerp(groundBounce, skyIrradiance, hemiFactor) * shadowAmbientFactor + indirectSun);

    float3 R = reflect(-V, N);
    float3 skySpecular = SampleSkyColor(R, sunDirection, sunColor, sunIntensity);
    float sunReflect = pow(max(dot(R, sunDir), 0.0), 16.0 / max(roughness * roughness * 16.0, 1.0));
    float3 sunHighlight = sunColor * sunIntensity * sunReflect * (1.0 - roughness) * 0.15 * shadow;

    float horizonFade = saturate(1.0 + dot(R, N));
    float3 specularAmbient =
        (skySpecular * 0.25 * shadowAmbientFactor + sunHighlight) * F * (1.0 - roughness * 0.4) * horizonFade;

    return (diffuse + specularAmbient) * ao;
}

float3 CalculateDirectionalLight(DirectionalLight light, float3 V, float3 N, float3 albedo, float metallic,
                                 float roughness, float shadow)
{
    float3 radiance = light.Color * light.Intensity * lerp(0.05, 1.0, shadow * shadow);
    return CalculatePBRLight(SafeNormalize(-light.Direction), V, N, albedo, metallic, roughness, radiance);
}

float3 CalculatePointLight(PointLight light, float3 worldPos, float3 V, float3 N, float3 albedo, float metallic,
                           float roughness)
{
    float3 L = light.Position - worldPos;
    float distance = length(L);

    if (distance > light.Radius)
        return 0.0;

    L /= distance;
    float distanceRatio = distance / light.Radius;
    float attenuation = saturate(1.0 - distanceRatio * distanceRatio);
    attenuation *= attenuation / (distance * distance + 1.0);

    return CalculatePBRLight(L, V, N, albedo, metallic, roughness, light.Color * light.Intensity * attenuation);
}

// ============================================
// Main
// ============================================

[numthreads(16, 16, 1)]
void CSMain(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint width, height;
    outputTexture.GetDimensions(width, height);

    if (dispatchThreadID.x >= width || dispatchThreadID.y >= height)
        return;

    float2 uv = (float2(dispatchThreadID.xy) + 0.5) / float2(width, height);
    float3 worldPos = positionTexture.SampleLevel(pointSampler, uv, 0).xyz;
    float3 normalData = normalTexture.SampleLevel(pointSampler, uv, 0).xyz;

    // Check for invalid surface data
    float posLenSq = dot(worldPos, worldPos);
    float normalLenSq = dot(normalData, normalData);

    if (posLenSq < EPSILON || normalLenSq < EPSILON)
    {
        outputTexture[dispatchThreadID.xy] =
            float4(CalculateSkybox(GetViewDirection(uv), constants.DirectionalLight.Direction,
                                   constants.DirectionalLight.Color, constants.DirectionalLight.Intensity),
                   1.0);
        return;
    }

    float3 albedo = albedoTexture.SampleLevel(pointSampler, uv, 0).rgb;
    float3 N = SafeNormalize(normalData);
    float2 metallicRoughness = metallicRoughnessTexture.SampleLevel(pointSampler, uv, 0).xy;
    metallicRoughness.y = max(metallicRoughness.y, 0.04);

    float4 emissiveData = emissiveTexture.SampleLevel(pointSampler, uv, 0);
    float ao = max(ssaoTexture.SampleLevel(pointSampler, uv, 0).r, 0.1);

    float3 V = SafeNormalize(constants.CameraPosition.xyz - worldPos);
    float shadow = CalculateShadow(worldPos, constants.CameraPosition.xyz, N, constants.DirectionalLight.Direction);

    float3 color = CalculateAmbient(N, V, albedo, metallicRoughness.x, metallicRoughness.y, ao, shadow,
                                    constants.DirectionalLight.Direction, constants.DirectionalLight.Color,
                                    constants.DirectionalLight.Intensity);

    color += CalculateDirectionalLight(constants.DirectionalLight, V, N, albedo, metallicRoughness.x,
                                       metallicRoughness.y, shadow);

    uint pointLightCount = pointLights.getCount();
    for (uint i = 0; i < pointLightCount; i++)
        color += CalculatePointLight(pointLights[i], worldPos, V, N, albedo, metallicRoughness.x, metallicRoughness.y);

    color = max(color, 0.0) + emissiveData.rgb * emissiveData.a;

    outputTexture[dispatchThreadID.xy] = float4(color, 1.0);
}
