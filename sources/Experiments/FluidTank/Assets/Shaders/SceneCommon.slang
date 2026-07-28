struct SceneVertex
{
    private float4 PositionAndPadding;

    private float4 NormalAndMaterial;

    property float3 Position
    {
        get {
            return PositionAndPadding.xyz;
        }
    }

    property float3 Normal
    {
        get {
            return NormalAndMaterial.xyz;
        }
    }

    property uint MaterialId
    {
        get {
            return asuint(NormalAndMaterial.w);
        }
    }
};

struct SceneMaterial
{
    private float4 AlbedoAndRoughness;

    private float4 MetallicEmissionAndPadding;

    property float3 Albedo
    {
        get {
            return AlbedoAndRoughness.xyz;
        }
    }

    property float Roughness
    {
        get {
            return AlbedoAndRoughness.w;
        }
    }

    property float Metallic
    {
        get {
            return MetallicEmissionAndPadding.x;
        }
    }

    property float Emission
    {
        get {
            return MetallicEmissionAndPadding.y;
        }
    }
};

float3 SkyColor(float3 direction)
{
    float horizon = saturate(direction.y * 0.5 + 0.5);
    float3 sky = lerp(float3(0.62, 0.76, 0.82), float3(0.055, 0.18, 0.34), pow(horizon, 0.62));
    float3 sunDirection = normalize(float3(-0.38, 0.83, -0.42));
    float sunAlignment = saturate(dot(direction, sunDirection));
    float sun = pow(sunAlignment, 720.0);
    float halo = pow(sunAlignment, 18.0);

    return sky + float3(10.0, 8.2, 5.3) * sun + float3(0.55, 0.42, 0.25) * halo;
}

float3 FresnelSchlick(float cosine, float3 f0)
{
    return f0 + (1.0 - f0) * pow(saturate(1.0 - cosine), 5.0);
}

float3 ACESFilm(float3 color)
{
    return saturate((color * (color * 2.51 + 0.03)) / (color * (color * 2.43 + 0.59) + 0.14));
}

float3 ToSRGB(float3 color)
{
    return pow(max(color, 0.0), 1.0 / 2.2);
}
