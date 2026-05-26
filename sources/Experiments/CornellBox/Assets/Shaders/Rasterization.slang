static const float PI = 3.14159265;

struct Material
{
    private float4 AlbedoAndEmission;

    private float4 MetallicRoughnessAndPadding;

    property float3 Albedo
    {
        get {
            return AlbedoAndEmission.xyz;
        }
    }

    property float Emission
    {
        get {
            return AlbedoAndEmission.w;
        }
    }

    property float Metallic
    {
        get {
            return MetallicRoughnessAndPadding.x;
        }
    }

    property float Roughness
    {
        get {
            return MetallicRoughnessAndPadding.y;
        }
    }
};

struct RasterizationConstants
{
    float4x4 Model;

    float4x4 View;

    float4x4 Projection;

    private float4 LightPositionAndPadding;

    private float4 LightColorAndPadding;

    private float4 CameraPositionAndPadding;

    DescriptorHandle<StructuredBuffer<Material>> Materials;

    property float3 LightPosition
    {
        get {
            return LightPositionAndPadding.xyz;
        }
    }

    property float3 LightColor
    {
        get {
            return LightColorAndPadding.xyz;
        }
    }

    property float3 CameraPosition
    {
        get {
            return CameraPositionAndPadding.xyz;
        }
    }
};

struct VSInput
{
    private float4 PositionAndPadding : POSITION0;

    private float4 NormalAndMaterialId : NORMAL0;

    property float3 Position
    {
        get {
            return PositionAndPadding.xyz;
        }
    }

    property float3 Normal
    {
        get {
            return NormalAndMaterialId.xyz;
        }
    }

    property uint MaterialId
    {
        get {
            return asuint(NormalAndMaterialId.w);
        }
    }
};

struct FSInput
{
    float4 Position : SV_POSITION;

    float3 WorldPos : TEXCOORD0;

    float3 Normal : TEXCOORD1;

    nointerpolation uint MaterialId : TEXCOORD2;
};

uniform RasterizationConstants rasterization;

float3 ACESFilm(float3 x)
{
    return saturate((x * (x * 2.51 + 0.03)) / (x * (x * 2.43 + 0.59) + 0.14));
}

float3 ToSRGB(float3 linear)
{
    return pow(linear, 1.0 / 2.2);
}

float DistributionGGX(float NdotH, float roughness)
{
    float a = roughness * roughness;
    float a2 = a * a;
    float denom = NdotH * NdotH * (a2 - 1.0) + 1.0;

    return a2 / (PI * denom * denom);
}

float GeometrySchlickGGX(float NdotX, float roughness)
{
    float r = roughness + 1.0;
    float k = (r * r) / 8.0;

    return NdotX / (NdotX * (1.0 - k) + k);
}

float GeometrySmith(float NdotV, float NdotL, float roughness)
{
    return GeometrySchlickGGX(NdotV, roughness) * GeometrySchlickGGX(NdotL, roughness);
}

float3 FresnelSchlick(float cosTheta, float3 F0)
{
    return F0 + (1.0 - F0) * pow(saturate(1.0 - cosTheta), 5.0);
}

FSInput VSMain(VSInput input)
{
    float4 worldPos = mul(float4(input.Position, 1.0), rasterization.Model);

    FSInput output;
    output.Position = mul(mul(worldPos, rasterization.View), rasterization.Projection);
    output.WorldPos = worldPos.xyz;
    output.Normal = normalize(mul(float4(input.Normal, 0.0), rasterization.Model).xyz);
    output.MaterialId = input.MaterialId;

    return output;
}

float4 FSMain(FSInput input) : SV_TARGET
{
    Material mat = (*rasterization.Materials)[input.MaterialId];

    if (mat.Emission > 0.0)
    {
        float3 emissive = mat.Albedo * mat.Emission;
        float3 mapped = emissive / (emissive + 1.0);

        return float4(ToSRGB(mapped), 1.0);
    }

    float3 N = normalize(input.Normal);
    float3 worldPos = input.WorldPos;
    float3 V = normalize(rasterization.CameraPosition - worldPos);
    float3 toLight = rasterization.LightPosition - worldPos;
    float dist = length(toLight);
    float3 L = toLight / dist;
    float3 H = normalize(L + V);

    float NdotL = max(dot(N, L), 0.0);
    float NdotH = max(dot(N, H), 0.0);
    float NdotV = max(dot(N, V), 0.001);
    float HdotV = max(dot(H, V), 0.0);

    float roughness = max(mat.Roughness, 0.04);
    float metallic = mat.Metallic;
    float3 F0 = lerp(float3(0.04), mat.Albedo, metallic);

    float D = DistributionGGX(NdotH, roughness);
    float G = GeometrySmith(NdotV, NdotL, roughness);
    float3 F = FresnelSchlick(HdotV, F0);

    float3 specular = (D * G * F) / (4.0 * NdotV * NdotL + 0.0001);

    float3 kD = (1.0 - F) * (1.0 - metallic);
    float3 diffuse = kD * mat.Albedo / PI;

    float atten = 1.0 / (dist * dist) * 80000.0;
    float3 Lo = (diffuse + specular) * rasterization.LightColor * NdotL * atten;

    float hemiFactor = N.y * 0.5 + 0.5;
    float3 ambient = mat.Albedo * lerp(0.03, 0.10, hemiFactor) * (1.0 - metallic * 0.7);

    float3 color = ambient + Lo;

    color = ToSRGB(ACESFilm(color));

    return float4(color, 1.0);
}
