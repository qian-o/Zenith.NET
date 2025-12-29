import "Common/Fullscreen";

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
    float3 CameraPosition;

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
SamplerState pointSampler;

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
                        float3 ambientColor)
{
    float3 F0 = lerp(float3(0.04, 0.04, 0.04), albedo, metallic);
    float NdotV = max(dot(N, V), 0.0);

    float3 F = FresnelSchlickRoughness(NdotV, F0, roughness);

    float3 kS = F;
    float3 kD = (1.0 - kS) * (1.0 - metallic);

    // Diffuse ambient with hemisphere approximation
    float3 skyColor = ambientColor * 1.2;
    float3 groundColor = ambientColor * 0.3;
    float hemiFactor = N.y * 0.5 + 0.5;
    float3 ambientDiffuse = lerp(groundColor, skyColor, hemiFactor);
    float3 diffuse = kD * albedo * ambientDiffuse;

    // Approximate specular ambient for metallic surfaces
    float3 R = reflect(-V, N);
    float horizonFade = saturate(1.0 + dot(R, N));
    float3 specularAmbient = ambientColor * F * (1.0 - roughness * 0.7) * horizonFade;

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

    // Add inverse square falloff for more realistic light decay
    float physicalAttenuation = 1.0 / (distance * distance + 1.0);
    attenuation *= physicalAttenuation;

    float3 radiance = light.Color * light.Intensity * attenuation;

    return CalculatePBRLight(L, V, N, albedo, metallic, roughness, radiance);
}

float4 PSMain(PSInput input) : SV_Target0
{
    float2 uv = input.TexCoord;

    float4 albedoData = albedoTexture.Sample(pointSampler, uv);
    float4 normalData = normalTexture.Sample(pointSampler, uv);
    float3 worldPos = positionTexture.Sample(pointSampler, uv).xyz;

    float2 metallicRoughnessData = metallicRoughnessTexture.Sample(pointSampler, uv).xy;
    metallicRoughnessData.y = clamp(metallicRoughnessData.y, 0.04, 1.0);

    float4 emissiveData = emissiveTexture.Sample(pointSampler, uv);
    float3 emissiveColor = emissiveData.rgb;
    float emissiveStrength = emissiveData.a;

    float3 albedo = albedoData.rgb;
    float3 N = normalize(normalData.xyz);

    float ao = ssaoTexture.Sample(pointSampler, uv).r;

    if (length(worldPos) < 0.001)
    {
        return float4(0.0, 0.0, 0.0, 1.0);
    }

    float3 V = normalize(constants.CameraPosition - worldPos);

    float3 ambientColor = float3(0.5, 0.6, 0.8) * 0.3;
    float3 ambient = CalculateAmbient(N, V, albedo, metallicRoughnessData.x, metallicRoughnessData.y, ao, ambientColor);

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

    return float4(color, 1.0);
}
