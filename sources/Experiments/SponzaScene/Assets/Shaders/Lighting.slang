static const float PI = 3.14159265359;
static const float INV_PI = 0.31830988618;
static const float EPSILON = 0.0001;

static const float MIN_AMBIENT = 0.015;

static const float SHADOW_BASE_OFFSET = 0.045;
static const float SHADOW_BASE_RADIUS = 3.5;
static const float SHADOW_NORMAL_SMOOTH = 0.5;
static const int SHADOW_SAMPLE_COUNT = 32;

struct DirectionalLight
{
    float4 DirectionAndIntensity;

    float4 ColorAndPadding;
};

struct PointLight
{
    float4 PositionAndRadius;

    float4 ColorAndIntensity;
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

    float _pad0;

    float _pad1;
};

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
Texture2D gtaoTexture;
Texture2D rtgiTexture;
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

float InterleavedGradientNoise(float2 pos)
{
    const float3 magic = float3(0.06711056, 0.00583715, 52.9829189);

    return frac(magic.z * frac(dot(pos, magic.xy)));
}

float2 VogelDiskSample(int sampleIndex, int samplesCount, int phi)
{
    const float goldenAngle = 2.4;

    float r = sqrt(float(sampleIndex) + 0.5) / sqrt(float(samplesCount));
    float theta = float(sampleIndex) * goldenAngle + phi;

    return float2(r * cos(theta), r * sin(theta));
}

// ============================================
// Sky
// ============================================

SkyParams CalculateSkyParams(float3 sunDirection)
{
    SkyParams params;

    params.SunDir = SafeNormalize(-sunDirection);

    float sunHeight = params.SunDir.y;

    params.DayFactor = saturate(sunHeight * 2.0);
    params.SunsetFactor = (1.0 - params.DayFactor) * (1.0 - params.DayFactor);
    params.NightFactor = saturate(-sunHeight * 3.0);
    params.SunVis = smoothstep(-0.1, 0.15, sunHeight);

    return params;
}

float3 BlendSkyColors(float3 dayColor, float3 sunsetColor, float3 nightColor, SkyParams params)
{
    return lerp(lerp(dayColor, sunsetColor, params.SunsetFactor), nightColor, params.NightFactor);
}

float3 SampleSkyColor(float3 direction, float3 sunDirection, float3 sunColor, float sunIntensity)
{
    SkyParams params = CalculateSkyParams(sunDirection);

    float3 zenithColor = BlendSkyColors(float3(0.1, 0.4, 1.0), float3(0.38, 0.22, 0.58) + sunColor * 0.32, float3(0.005, 0.005, 0.015), params);

    float3 horizonColor = BlendSkyColors(float3(0.5, 0.7, 1.0), sunColor * float3(1.0, 0.8, 0.55), float3(0.015, 0.015, 0.04), params);

    float t = pow(saturate(direction.y * 0.5 + 0.5), 0.35);

    return lerp(horizonColor, zenithColor, t) * (0.6 + params.DayFactor * 0.4) * sunIntensity * 0.32;
}

float3 CalculateSkybox(float3 viewDir, float3 sunDirection, float3 sunColor, float sunIntensity)
{
    viewDir = SafeNormalize(viewDir);

    SkyParams params = CalculateSkyParams(sunDirection);
    float elevation = viewDir.y;

    float3 zenithColor = BlendSkyColors(float3(0.08, 0.35, 1.0), float3(0.35, 0.2, 0.55) + sunColor * 0.3, float3(0.005, 0.005, 0.02), params);

    float3 horizonColor = BlendSkyColors(float3(0.25, 0.48, 0.95), sunColor * float3(1.0, 0.85, 0.6), float3(0.015, 0.015, 0.04), params);

    float3 groundColor = BlendSkyColors(float3(0.18, 0.35, 0.7), sunColor * float3(0.7, 0.5, 0.35), float3(0.01, 0.01, 0.025), params);

    float skyBlend = pow(saturate(elevation + 0.1), 0.18);
    float3 skyColor = lerp(horizonColor, zenithColor, skyBlend);

    float groundBlend = 1.0 - saturate((elevation + 0.3) / 0.5);
    groundBlend = groundBlend * groundBlend * (3.0 - 2.0 * groundBlend);
    skyColor = lerp(skyColor, groundColor, groundBlend);

    float sunDot = dot(viewDir, params.SunDir);
    float glowIntensity = sunIntensity * 0.2;

    float glow1 = pow(max(sunDot * 0.5 + 0.5, 0.0), 8.0 + params.DayFactor * 4.0) * 0.05 * (1.0 + params.SunsetFactor * 0.5);
    float glow2 = pow(max(sunDot, 0.0), 32.0 + params.DayFactor * 16.0) * 0.15 * glowIntensity;
    float glow3 = pow(max(sunDot, 0.0), 128.0 + params.DayFactor * 64.0) * 0.3 * glowIntensity;
    float glow4 = pow(max(sunDot, 0.0), 512.0 + params.DayFactor * 256.0) * 0.8 * glowIntensity;

    float discThreshold = 0.9998 + params.DayFactor * 0.0001;
    float sunDisc = smoothstep(discThreshold, discThreshold + 0.0001, sunDot) * 2.0 * glowIntensity;

    float3 glowColor = lerp(sunColor * float3(1.0, 0.6, 0.3), sunColor, params.DayFactor) * params.SunVis;
    float3 discColor = lerp(float3(1.0, 0.85, 0.6), float3(1.0, 0.98, 0.94), params.DayFactor) * params.SunVis;

    skyColor += glowColor * (glow1 + glow2) + sunColor * params.SunVis * (glow3 + glow4) + discColor * sunDisc;

    float horizonGlow = exp(-elevation * elevation * (30.0 + params.DayFactor * 20.0));
    float3 viewHoriz = SafeNormalize(float3(viewDir.x, 0.0, viewDir.z));
    float3 sunHoriz = SafeNormalize(float3(params.SunDir.x, 0.0, params.SunDir.z));
    float horizSunDot = max(dot(viewHoriz, sunHoriz), 0.0);

    float3 warmGlow = lerp(float3(0.15, 0.1, 0.15), sunColor * 0.5, horizSunDot * params.SunsetFactor);
    skyColor += warmGlow * horizonGlow * (0.06 + params.SunsetFactor * 0.1) * (0.3 + horizSunDot * 0.5) * params.SunVis;

    if (params.NightFactor > 0.1 && elevation > 0.0)
    {
        float starNoise = frac(sin(dot(viewDir.xz * 100.0, float2(12.9898, 78.233))) * 43758.5453);

        skyColor += smoothstep(0.98, 1.0, starNoise) * params.NightFactor * elevation * 0.5;
    }

    return skyColor;
}

float3 GetViewDirection(float2 uv)
{
    float2 ndc = float2(uv.x * 2.0 - 1.0, 1.0 - uv.y * 2.0);
    float4 worldPos = mul(float4(ndc, 1.0, 1.0), constants.InverseViewProjection);

    return SafeNormalize(worldPos.xyz / worldPos.w - constants.CameraPosition.xyz);
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
        {
            return int(i);
        }
    }

    return int(cascadeCount - 1);
}

float3 WorldToShadowCoord(float3 worldPos, int cascadeIndex)
{
    float4 lightSpacePos = mul(mul(float4(worldPos, 1.0), csmDatas[cascadeIndex].View), csmDatas[cascadeIndex].Projection);
    lightSpacePos.xyz /= lightSpacePos.w;

    return float3(lightSpacePos.x * 0.5 + 0.5, -lightSpacePos.y * 0.5 + 0.5, lightSpacePos.z);
}

float SampleShadowPCF(float3 shadowCoord, int cascadeIndex, float2 texelSize, float2 screenPos, float filterRadius)
{
    int rotationAngle = int(InterleavedGradientNoise(screenPos) * PI * 2.0);
    float shadow = 0.0;

    for (int i = 0; i < SHADOW_SAMPLE_COUNT; i++)
    {
        float2 offset = VogelDiskSample(i, SHADOW_SAMPLE_COUNT, rotationAngle) * filterRadius * texelSize;

        shadow += csmTextures.SampleCmpLevelZero(shadowSampler,
                                                 float3(shadowCoord.xy + offset, cascadeIndex),
                                                 shadowCoord.z);
    }

    return shadow / float(SHADOW_SAMPLE_COUNT);
}

float CalculateShadow(float3 worldPos, float3 cameraPos, float3 normal, float3 lightDir, float2 screenPos)
{
    uint cascadeCount = csmDatas.getCount();

    if (cascadeCount == 0)
    {
        return 1.0;
    }

    float3 L = SafeNormalize(-lightDir);
    float3 V = SafeNormalize(cameraPos - worldPos);

    float3 shadowNormal = SafeNormalize(lerp(normal, SafeNormalize(normal + L * 0.3), SHADOW_NORMAL_SMOOTH));

    float NdotL = dot(shadowNormal, L);

    if (NdotL <= 0.0)
    {
        return 0.0;
    }

    float NdotV = abs(dot(shadowNormal, V));
    float grazingView = 1.0 - NdotV;
    float grazingLight = 1.0 - NdotL;
    float combinedGrazing = max(grazingView * grazingView, grazingLight * grazingLight);

    float distanceToCamera = length(worldPos - cameraPos);
    int cascadeIndex = GetCascadeIndex(distanceToCamera);
    float cascadeScale = 1.0 + cascadeIndex * 0.6;

    float3 biasNormal = SafeNormalize(lerp(shadowNormal, L, combinedGrazing * 0.7));
    float sinTheta = sqrt(1.0 - NdotL * NdotL);
    float slopeBias = min(sinTheta / max(NdotL, 0.01), 8.0) * 0.005;
    float grazingBias = combinedGrazing * combinedGrazing * 0.15;

    float totalBias = (SHADOW_BASE_OFFSET + slopeBias + grazingBias) * cascadeScale;
    float3 biasedPos = worldPos + biasNormal * totalBias + L * totalBias * 0.5;
    float3 shadowCoord = WorldToShadowCoord(biasedPos, cascadeIndex);

    const float margin = 0.01;

    if (any(shadowCoord.xy < margin) || any(shadowCoord.xy > 1.0 - margin) || shadowCoord.z < 0.0 || shadowCoord.z > 1.0)
    {
        return 1.0;
    }

    uint width, height, elements;
    csmTextures.GetDimensions(width, height, elements);
    float2 texelSize = 1.0 / float2(width, height);

    float cascadeFar = csmDatas[cascadeIndex].FarPlane;
    float cascadeNear = csmDatas[cascadeIndex].NearPlane;
    float blendRegion = (cascadeFar - cascadeNear) * 0.25;
    float blendStart = cascadeFar - blendRegion;

    float blendFactor = 0.0;

    if (distanceToCamera > blendStart && cascadeIndex < int(cascadeCount - 1))
    {
        blendFactor = smoothstep(0.0, 1.0, (distanceToCamera - blendStart) / blendRegion);
    }

    float grazingRadius = combinedGrazing * combinedGrazing * 3.5;
    float baseRadius = SHADOW_BASE_RADIUS + cascadeIndex * 0.5 + grazingRadius;
    float filterRadius = lerp(baseRadius, baseRadius + 0.5, blendFactor);

    float shadow = SampleShadowPCF(shadowCoord, cascadeIndex, texelSize, screenPos, filterRadius);

    if (blendFactor > 0.0)
    {
        int nextCascade = cascadeIndex + 1;
        float nextCascadeScale = 1.0 + nextCascade * 0.6;
        float nextTotalBias = (SHADOW_BASE_OFFSET + slopeBias + grazingBias) * nextCascadeScale;

        float3 nextBiasedPos = worldPos + biasNormal * nextTotalBias + L * nextTotalBias * 0.5;
        float3 nextShadowCoord = WorldToShadowCoord(nextBiasedPos, nextCascade);

        if (all(nextShadowCoord.xy > margin) && all(nextShadowCoord.xy < 1.0 - margin) && nextShadowCoord.z > 0.0 &&
            nextShadowCoord.z < 1.0)
        {
            float nextFilterRadius = baseRadius + 0.5 + nextCascade * 0.5;
            float nextShadow = SampleShadowPCF(nextShadowCoord, nextCascade, texelSize, screenPos, nextFilterRadius);

            shadow = lerp(shadow, nextShadow, blendFactor);
        }
    }

    return shadow;
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
    float ggxV = NdotV / (NdotV * (1.0 - k) + k);
    float ggxL = NdotL / (NdotL * (1.0 - k) + k);

    return ggxV * ggxL;
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
    float HdotV = max(dot(H, V), 0.0);

    float D = DistributionGGX(NdotH, roughness);
    float G = GeometrySmith(NdotV, NdotL, roughness);
    float3 F = FresnelSchlick(HdotV, F0);

    float3 specular = (D * G * F) / (4.0 * NdotV * NdotL + EPSILON);
    float3 kD = (1.0 - F) * (1.0 - metallic);

    return (kD * albedo * INV_PI + specular) * radiance * NdotL;
}

// ============================================
// Lighting
// ============================================

float3 CalculateMinimalAmbient(float3 N, float3 V, float3 albedo, float metallic, float roughness, float ao,
                               float3 sunDirection, float3 sunColor, float sunIntensity)
{
    float3 F0 = lerp(0.04, albedo, metallic);
    float NdotV = max(dot(N, V), EPSILON);

    float3 F = FresnelSchlickRoughness(NdotV, F0, roughness);
    float3 kD = (1.0 - F) * (1.0 - metallic);

    float3 skyColor = SampleSkyColor(N, sunDirection, sunColor, sunIntensity);
    float3 ambient = kD * albedo * skyColor * 0.05;

    float3 R = reflect(-V, N);
    float3 skyReflection = SampleSkyColor(R, sunDirection, sunColor, sunIntensity);
    float horizonOcclusion = saturate(1.0 + dot(R, N));
    float roughnessAttenuation = 1.0 - roughness * 0.7;
    float3 specularAmbient = skyReflection * F * horizonOcclusion * roughnessAttenuation * 0.2;

    return (ambient + specularAmbient) * ao;
}

float3 CalculateDirectionalLight(DirectionalLight light, float3 V, float3 N, float3 albedo, float metallic, float roughness, float shadow)
{
    float shadowFactor = shadow * shadow * shadow;
    float3 radiance = light.ColorAndPadding.xyz * light.DirectionAndIntensity.w * shadowFactor;

    return CalculatePBRLight(SafeNormalize(-light.DirectionAndIntensity.xyz), V, N, albedo, metallic, roughness, radiance);
}

float3 CalculatePointLight(PointLight light, float3 worldPos, float3 V, float3 N, float3 albedo, float metallic, float roughness)
{
    float3 toLight = light.PositionAndRadius.xyz - worldPos;
    float distance = length(toLight);

    if (distance > light.PositionAndRadius.w)
    {
        return 0.0;
    }

    float3 L = toLight / distance;

    float distanceRatio = distance / light.PositionAndRadius.w;
    float distanceRatio2 = distanceRatio * distanceRatio;
    float radiusAttenuation = saturate(1.0 - distanceRatio2 * distanceRatio2);
    radiusAttenuation *= radiusAttenuation;

    float physicalFalloff = 1.0 / max(distance * distance, EPSILON);
    float attenuation = radiusAttenuation * physicalFalloff;

    return CalculatePBRLight(L, V, N, albedo, metallic, roughness, light.ColorAndIntensity.xyz * light.ColorAndIntensity.w * attenuation);
}

float3 CalculateGlobalIllumination(float3 N, float3 albedo, float3 giRadiance, float metallic, float ao,
                                   float3 sunDirection, float3 sunColor, float sunIntensity, float shadow)
{
    float diffuseWeight = 1.0 - metallic;
    float giLuminance = dot(giRadiance, float3(0.299, 0.587, 0.114));
    bool hasValidGI = giLuminance > 0.001;

    float3 sunDir = SafeNormalize(-sunDirection);
    float sunHeight = max(sunDir.y, 0.0);
    float NdotL = max(dot(N, sunDir), 0.0);

    float3 giContribution = float3(0.0);

    if (hasValidGI)
    {
        float directLightStrength = NdotL * shadow;
        float giScale = lerp(1.8, 0.8, directLightStrength);

        giContribution = albedo * giRadiance * diffuseWeight * giScale;
    }
    else
    {
        float3 skyUp = float3(0.25, 0.4, 0.65) * sunIntensity * 0.06;
        float3 skyHorizon = float3(0.35, 0.4, 0.45) * sunIntensity * 0.05;
        float3 skyDown = float3(0.12, 0.11, 0.1) * sunIntensity * 0.03;

        float upWeight = max(N.y, 0.0);
        float downWeight = max(-N.y, 0.0);
        float sideWeight = 1.0 - abs(N.y);

        float3 ambient = skyUp * upWeight + skyDown * downWeight + skyHorizon * sideWeight;

        float shadowAmbientBoost = lerp(1.5, 1.0, shadow);

        giContribution = albedo * ambient * diffuseWeight * shadowAmbientBoost;
    }

    float darknessLevel = 1.0 - max(NdotL * shadow, giLuminance * 6.0);
    float3 minAmbient = albedo * MIN_AMBIENT * sunIntensity * (0.2 + sunHeight * 0.8);
    giContribution += minAmbient * saturate(darknessLevel) * 0.3;

    if (!hasValidGI)
    {
        float3 fallbackAmbient = albedo * 0.015 * sunIntensity * (0.3 + sunHeight * 0.7);
        giContribution += fallbackAmbient * diffuseWeight;
    }

    float aoFactor = lerp(0.4, 1.0, ao);

    return giContribution * aoFactor;
}

[numthreads(16, 16, 1)]
void CSMain(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint width, height;
    outputTexture.GetDimensions(width, height);

    if (dispatchThreadID.x >= width || dispatchThreadID.y >= height)
    {
        return;
    }

    float2 uv = (float2(dispatchThreadID.xy) + 0.5) / float2(width, height);

    float3 worldPos = positionTexture.SampleLevel(pointSampler, uv, 0).xyz;
    float3 normalData = normalTexture.SampleLevel(pointSampler, uv, 0).xyz;

    if (dot(worldPos, worldPos) < EPSILON || dot(normalData, normalData) < EPSILON)
    {
        outputTexture[dispatchThreadID.xy] = float4(CalculateSkybox(GetViewDirection(uv),
                                                                    constants.DirectionalLight.DirectionAndIntensity.xyz,
                                                                    constants.DirectionalLight.ColorAndPadding.xyz,
                                                                    constants.DirectionalLight.DirectionAndIntensity.w),
                                                    1.0);
        return;
    }

    float3 albedo = albedoTexture.SampleLevel(pointSampler, uv, 0).rgb;
    float3 N = SafeNormalize(normalData);
    float2 metallicRoughness = metallicRoughnessTexture.SampleLevel(pointSampler, uv, 0).xy;
    metallicRoughness.y = max(metallicRoughness.y, 0.04);

    float4 emissiveData = emissiveTexture.SampleLevel(pointSampler, uv, 0);
    float ao = max(gtaoTexture.SampleLevel(pointSampler, uv, 0).r, 0.1);
    float3 giRadiance = rtgiTexture.SampleLevel(pointSampler, uv, 0).rgb;
    float3 V = SafeNormalize(constants.CameraPosition.xyz - worldPos);

    float3 color;

    if (dot(emissiveData.rgb, float3(0.299, 0.587, 0.114)) > 0.01)
    {
        color = emissiveData.rgb * emissiveData.a;
    }
    else
    {
        float shadow = CalculateShadow(worldPos, constants.CameraPosition.xyz, N, constants.DirectionalLight.DirectionAndIntensity.xyz, uv);

        color = CalculateMinimalAmbient(N,
                                        V,
                                        albedo,
                                        metallicRoughness.x,
                                        metallicRoughness.y,
                                        ao,
                                        constants.DirectionalLight.DirectionAndIntensity.xyz,
                                        constants.DirectionalLight.ColorAndPadding.xyz,
                                        constants.DirectionalLight.DirectionAndIntensity.w);

        color += CalculateDirectionalLight(constants.DirectionalLight,
                                           V,
                                           N,
                                           albedo,
                                           metallicRoughness.x,
                                           metallicRoughness.y,
                                           shadow);

        uint pointLightCount = pointLights.getCount();

        for (uint i = 0; i < pointLightCount; i++)
        {
            color += CalculatePointLight(pointLights[i], worldPos, V, N, albedo, metallicRoughness.x, metallicRoughness.y);
        }

        color += CalculateGlobalIllumination(N,
                                             albedo,
                                             giRadiance,
                                             metallicRoughness.x,
                                             ao,
                                             constants.DirectionalLight.DirectionAndIntensity.xyz,
                                             constants.DirectionalLight.ColorAndPadding.xyz,
                                             constants.DirectionalLight.DirectionAndIntensity.w,
                                             shadow);
    }

    outputTexture[dispatchThreadID.xy] = float4(max(color, 0.0), 1.0);
}
