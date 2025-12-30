static const float PI = 3.14159265359;

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

ConstantBuffer<LightingConstants> constants;
StructuredBuffer<PointLight> pointLights;
Texture2D albedoTexture;
Texture2D normalTexture;
Texture2D positionTexture;
Texture2D metallicRoughnessTexture;
Texture2D emissiveTexture;
Texture2D ssaoTexture;
RWTexture2D outputTexture;
SamplerState pointSampler;

// ============================================
// Dynamic Procedural Skybox
// ============================================

float3 CalculateSkybox(float3 viewDir, float3 sunDirection, float3 sunColor, float sunIntensity)
{
    viewDir = normalize(viewDir);

    float3 sunDir = normalize(-sunDirection);
    float sunHeight = sunDir.y; // -1 (below) to 1 (above)

    float elevation = viewDir.y;

    // ============================================
    // Dynamic sky colors based on sun height
    // ============================================

    // Sun height factor: 0 = horizon/sunset, 1 = noon
    float dayFactor = saturate(sunHeight * 2.0);
    float sunsetFactor = 1.0 - dayFactor;
    sunsetFactor = sunsetFactor * sunsetFactor;

    // Night factor for when sun is below horizon
    float nightFactor = saturate(-sunHeight * 3.0);

    // Base daytime colors
    float3 dayZenith = float3(0.15, 0.35, 0.75);
    float3 dayHorizon = float3(0.5, 0.7, 0.9);

    // Sunset colors - influenced by sun color
    float3 sunsetZenith = float3(0.12, 0.08, 0.25) + sunColor * 0.1;
    float3 sunsetHorizon = sunColor * float3(1.0, 0.6, 0.3);

    // Night colors
    float3 nightZenith = float3(0.02, 0.02, 0.06);
    float3 nightHorizon = float3(0.05, 0.05, 0.1);

    // Blend zenith color: day -> sunset -> night
    float3 zenithColor = lerp(dayZenith, sunsetZenith, sunsetFactor);
    zenithColor = lerp(zenithColor, nightZenith, nightFactor);

    // Blend horizon color: day -> sunset -> night
    float3 horizonColor = lerp(dayHorizon, sunsetHorizon, sunsetFactor);
    horizonColor = lerp(horizonColor, nightHorizon, nightFactor);

    // Ground color - also affected by sun
    float3 groundColor = float3(0.15, 0.12, 0.1) * (0.3 + dayFactor * 0.7);
    groundColor = lerp(groundColor, groundColor * sunColor, sunsetFactor * 0.5);

    // ============================================
    // Sky gradient
    // ============================================

    float3 skyColor;

    float skyBlend = pow(saturate(elevation), 0.4 + dayFactor * 0.2);
    float3 skyGradient = lerp(horizonColor, zenithColor, skyBlend);

    float groundBlend = smoothstep(0.0, -0.5, elevation);
    groundBlend = groundBlend * groundBlend;

    skyColor = lerp(skyGradient, groundColor, groundBlend);

    // Horizon softening
    float horizonSoftness = exp(-abs(elevation) * (8.0 + dayFactor * 4.0)) * 0.15;
    skyColor = lerp(skyColor, horizonColor, horizonSoftness);

    // ============================================
    // Sun rendering - size and glow based on height
    // ============================================

    float sunDot = dot(viewDir, sunDir);

    // Sun size varies: larger at horizon, smaller at noon
    float sunSizeMultiplier = 1.0 + sunsetFactor * 0.5;

    // Glow intensity based on sun intensity
    float glowIntensity = sunIntensity / 4.0; // Normalize around intensity 4

    // Wide glow - stronger at sunset
    float glow1 = pow(max(sunDot * 0.5 + 0.5, 0.0), 3.0 + dayFactor * 3.0) * 0.1 * (1.0 + sunsetFactor);

    // Medium glow
    float glow2 = pow(max(sunDot, 0.0), 6.0 + dayFactor * 6.0) * 0.3 * glowIntensity;

    // Inner glow
    float glow3 = pow(max(sunDot, 0.0), 32.0 + dayFactor * 32.0) * 0.5 * glowIntensity;

    // Bright core
    float glow4 = pow(max(sunDot, 0.0), 128.0 + dayFactor * 128.0) * 1.0 * glowIntensity;

    // Sun disc
    float discThreshold = 0.9985 + dayFactor * 0.001;
    float sunDisc = smoothstep(discThreshold, discThreshold + 0.0008, sunDot) * 2.5 * glowIntensity;

    // Sun visibility (fade when below horizon)
    float sunVis = smoothstep(-0.1, 0.15, sunDir.y);

    // Sun glow color - blend between sun color and warm sunset
    float3 glowColor = lerp(sunColor * float3(1.0, 0.6, 0.3), sunColor, dayFactor) * sunVis;
    float3 discColor = lerp(float3(1.0, 0.85, 0.6), float3(1.0, 0.98, 0.94), dayFactor) * sunVis;

    skyColor += glowColor * glow1;
    skyColor += glowColor * glow2;
    skyColor += sunColor * sunVis * glow3;
    skyColor += sunColor * sunVis * glow4;
    skyColor += discColor * sunDisc;

    // ============================================
    // Horizon atmospheric glow
    // ============================================

    float horizonGlow = exp(-elevation * elevation * (15.0 + dayFactor * 10.0));

    float3 viewHoriz = normalize(float3(viewDir.x, 0.0, viewDir.z) + float3(0.0001, 0.0, 0.0001));
    float3 sunHoriz = normalize(float3(sunDir.x, 0.0, sunDir.z) + float3(0.0001, 0.0, 0.0001));
    float horizSunDot = max(dot(viewHoriz, sunHoriz), 0.0);

    // Warm glow color based on sun color
    float3 warmGlow = lerp(float3(0.3, 0.25, 0.3), sunColor * 0.8, horizSunDot * sunsetFactor);
    float glowStrength = 0.1 + sunsetFactor * 0.15;
    skyColor += warmGlow * horizonGlow * glowStrength * (0.5 + horizSunDot * 0.8) * sunVis;

    // ============================================
    // Stars at night (optional)
    // ============================================

    if (nightFactor > 0.1 && elevation > 0.0)
    {
        // Simple noise-based stars
        float starNoise = frac(sin(dot(viewDir.xz * 100.0, float2(12.9898, 78.233))) * 43758.5453);
        float stars = smoothstep(0.98, 1.0, starNoise) * nightFactor * elevation;
        skyColor += float3(1.0, 1.0, 1.0) * stars * 0.5;
    }

    return skyColor;
}

float3 SampleSkyColor(float3 direction, float3 sunDirection, float3 sunColor, float sunIntensity)
{
    float3 sunDir = normalize(-sunDirection);
    float sunHeight = sunDir.y;
    float elevation = direction.y;

    // Dynamic ambient based on sun
    float dayFactor = saturate(sunHeight * 2.0);
    float sunsetFactor = 1.0 - dayFactor;
    sunsetFactor = sunsetFactor * sunsetFactor;
    float nightFactor = saturate(-sunHeight * 3.0);

    // Ambient colors
    float3 dayZenith = float3(0.2, 0.4, 0.8);
    float3 dayHorizon = float3(0.5, 0.7, 0.9);

    float3 sunsetZenith = float3(0.15, 0.1, 0.3) + sunColor * 0.15;
    float3 sunsetHorizon = sunColor * float3(0.8, 0.5, 0.3);

    float3 nightZenith = float3(0.02, 0.02, 0.05);
    float3 nightHorizon = float3(0.03, 0.03, 0.06);

    float3 zenithColor = lerp(dayZenith, sunsetZenith, sunsetFactor);
    zenithColor = lerp(zenithColor, nightZenith, nightFactor);

    float3 horizonColor = lerp(dayHorizon, sunsetHorizon, sunsetFactor);
    horizonColor = lerp(horizonColor, nightHorizon, nightFactor);

    float t = pow(saturate(elevation * 0.5 + 0.5), 0.5);
    float3 skyAmbient = lerp(horizonColor, zenithColor, t);

    // Scale by sun intensity
    float intensityScale = (0.4 + dayFactor * 0.6) * (sunIntensity / 4.0);

    return skyAmbient * intensityScale;
}

float3 GetViewDirection(float2 uv)
{
    float2 ndc;
    ndc.x = uv.x * 2.0 - 1.0;
    ndc.y = 1.0 - uv.y * 2.0;

    float4 worldPos = mul(float4(ndc, 1.0, 1.0), constants.InverseViewProjection);
    worldPos.xyz /= worldPos.w;

    return normalize(worldPos.xyz - constants.CameraPosition.xyz);
}

// ============================================
// PBR Functions
// ============================================

float DistributionGGX(float3 N, float3 H, float roughness)
{
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;

    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;

    return a2 / max(denom, 0.0001);
}

float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = roughness + 1.0;
    float k = (r * r) / 8.0;

    return NdotV / (NdotV * (1.0 - k) + k);
}

float GeometrySmith(float3 N, float3 V, float3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx1 = GeometrySchlickGGX(NdotV, roughness);
    float ggx2 = GeometrySchlickGGX(NdotL, roughness);

    return ggx1 * ggx2;
}

float3 FresnelSchlick(float cosTheta, float3 F0)
{
    return F0 + (1.0 - F0) * pow(saturate(1.0 - cosTheta), 5.0);
}

float3 FresnelSchlickRoughness(float cosTheta, float3 F0, float roughness)
{
    return F0 + (max(float3(1.0 - roughness, 1.0 - roughness, 1.0 - roughness), F0) - F0) *
                    pow(saturate(1.0 - cosTheta), 5.0);
}

float3 CalculatePBRLight(float3 L, float3 V, float3 N, float3 albedo, float metallic, float roughness, float3 radiance)
{
    float3 H = normalize(V + L);

    float3 F0 = lerp(float3(0.04, 0.04, 0.04), albedo, metallic);

    float D = DistributionGGX(N, H, roughness);
    float G = GeometrySmith(N, V, L, roughness);
    float3 F = FresnelSchlick(max(dot(H, V), 0.0), F0);

    float3 numerator = D * G * F;
    float denominator = 4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0) + 0.0001;
    float3 specular = numerator / denominator;

    float3 kS = F;
    float3 kD = (1.0 - kS) * (1.0 - metallic);

    float NdotL = max(dot(N, L), 0.0);

    return (kD * albedo / PI + specular) * radiance * NdotL;
}

float3 CalculateAmbient(float3 N, float3 V, float3 albedo, float metallic, float roughness, float ao,
                        float3 sunDirection, float3 sunColor, float sunIntensity)
{
    float3 F0 = lerp(float3(0.04, 0.04, 0.04), albedo, metallic);
    float NdotV = max(dot(N, V), 0.0);

    float3 F = FresnelSchlickRoughness(NdotV, F0, roughness);

    float3 kS = F;
    float3 kD = (1.0 - kS) * (1.0 - metallic);

    float3 sunDir = normalize(-sunDirection);
    float sunHeight = saturate(sunDir.y);

    float3 skyIrradiance = SampleSkyColor(N, sunDirection, sunColor, sunIntensity);

    float3 groundBounce = sunColor * sunIntensity * 0.05 * sunHeight;

    float hemiFactor = N.y * 0.5 + 0.5;
    float3 ambientDiffuse = lerp(groundBounce, skyIrradiance, hemiFactor);

    float NdotL = max(dot(N, sunDir), 0.0);
    float3 indirectSun = sunColor * sunIntensity * 0.06 * pow(NdotL * 0.5 + 0.5, 2.0) * sunHeight;
    ambientDiffuse += indirectSun;

    float3 diffuse = kD * albedo * ambientDiffuse;

    float3 R = reflect(-V, N);
    float3 skySpecular = SampleSkyColor(R, sunDirection, sunColor, sunIntensity);

    float sunReflect = pow(max(dot(R, sunDir), 0.0), 16.0 / max(roughness * roughness * 16.0, 1.0));
    float3 sunHighlight = sunColor * sunIntensity * sunReflect * (1.0 - roughness) * 0.2;

    float horizonFade = saturate(1.0 + dot(R, N));
    float3 specularAmbient = (skySpecular * 0.3 + sunHighlight) * F * (1.0 - roughness * 0.4) * horizonFade;

    return (diffuse + specularAmbient) * ao;
}

float3 CalculateDirectionalLight(DirectionalLight light, float3 V, float3 N, float3 albedo, float metallic,
                                 float roughness)
{
    float3 L = normalize(-light.Direction);
    float3 radiance = light.Color * light.Intensity;

    return CalculatePBRLight(L, V, N, albedo, metallic, roughness, radiance);
}

float3 CalculatePointLight(PointLight light, float3 worldPos, float3 V, float3 N, float3 albedo, float metallic,
                           float roughness)
{
    float3 L = light.Position - worldPos;
    float distance = length(L);

    if (distance > light.Radius)
    {
        return float3(0.0, 0.0, 0.0);
    }

    L = L / distance;

    float distanceRatio = distance / light.Radius;
    float attenuation = saturate(1.0 - distanceRatio * distanceRatio);
    attenuation *= attenuation;

    float physicalAttenuation = 1.0 / (distance * distance + 1.0);
    attenuation *= physicalAttenuation;

    float3 radiance = light.Color * light.Intensity * attenuation;

    return CalculatePBRLight(L, V, N, albedo, metallic, roughness, radiance);
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

    float4 albedoData = albedoTexture.SampleLevel(pointSampler, uv, 0);
    float4 normalData = normalTexture.SampleLevel(pointSampler, uv, 0);
    float3 worldPos = positionTexture.SampleLevel(pointSampler, uv, 0).xyz;

    float2 metallicRoughnessData = metallicRoughnessTexture.SampleLevel(pointSampler, uv, 0).xy;
    metallicRoughnessData.y = clamp(metallicRoughnessData.y, 0.04, 1.0);

    float4 emissiveData = emissiveTexture.SampleLevel(pointSampler, uv, 0);
    float3 emissiveColor = emissiveData.rgb;
    float emissiveStrength = emissiveData.a;

    float3 albedo = albedoData.rgb;
    float3 N = normalize(normalData.xyz);

    float ao = ssaoTexture.SampleLevel(pointSampler, uv, 0).r;

    if (length(worldPos) < 0.001)
    {
        float3 viewDir = GetViewDirection(uv);
        float3 skyColor = CalculateSkybox(viewDir, constants.DirectionalLight.Direction,
                                          constants.DirectionalLight.Color, constants.DirectionalLight.Intensity);
        outputTexture[dispatchThreadID.xy] = float4(skyColor, 1.0);
        return;
    }

    float3 V = normalize(constants.CameraPosition.xyz - worldPos);

    float3 ambient = CalculateAmbient(N, V, albedo, metallicRoughnessData.x, metallicRoughnessData.y, ao,
                                      constants.DirectionalLight.Direction, constants.DirectionalLight.Color,
                                      constants.DirectionalLight.Intensity);

    float3 Lo = float3(0.0, 0.0, 0.0);

    Lo += CalculateDirectionalLight(constants.DirectionalLight, V, N, albedo, metallicRoughnessData.x,
                                    metallicRoughnessData.y);

    uint pointLightCount = pointLights.getCount();
    for (uint i = 0; i < pointLightCount; i++)
    {
        Lo += CalculatePointLight(pointLights[i], worldPos, V, N, albedo, metallicRoughnessData.x,
                                  metallicRoughnessData.y);
    }

    float3 color = ambient + Lo;

    float3 emissive = emissiveColor * emissiveStrength;
    color += emissive;

    outputTexture[dispatchThreadID.xy] = float4(color, 1.0);
}
