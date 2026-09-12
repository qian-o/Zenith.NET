struct SceneVertex
{
    private float4 PositionAndPadding;

    private float4 NormalAndMaterial;

    property float3 Position
    {
        get
        {
            return PositionAndPadding.xyz;
        }
    }

    property float3 Normal
    {
        get
        {
            return NormalAndMaterial.xyz;
        }
    }

    property uint MaterialId
    {
        get
        {
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
        get
        {
            return AlbedoAndRoughness.xyz;
        }
    }

    property float Roughness
    {
        get
        {
            return AlbedoAndRoughness.w;
        }
    }

    property float Metallic
    {
        get
        {
            return MetallicEmissionAndPadding.x;
        }
    }

    property float Emission
    {
        get
        {
            return MetallicEmissionAndPadding.y;
        }
    }
};

static const float ScenePi = 3.14159265359;

static const float3 SunColor = float3(1.0, 0.96, 0.88);

float3 EnvironmentColor(float3 direction)
{
    float3 sky = lerp(float3(0.23, 0.32, 0.40), float3(0.055, 0.13, 0.24), sqrt(saturate(direction.y)));

    return lerp(sky, float3(0.03, 0.028, 0.025), saturate(-direction.y));
}

float3 SkyColor(float3 direction, float3 sunDirection)
{
    float sunAlignment = saturate(dot(direction, -sunDirection));
    float sun = pow(sunAlignment, 1600.0);

    return EnvironmentColor(direction) + SunColor * sun * 12.0;
}

float3 FresnelSchlick(float cosine, float3 f0)
{
    return f0 + (1.0 - f0) * pow(saturate(1.0 - cosine), 5.0);
}

float DistributionGGX(float ndoth, float alpha)
{
    float alphaSquared = alpha * alpha;
    float denominator = ndoth * ndoth * (alphaSquared - 1.0) + 1.0;

    return alphaSquared / (ScenePi * denominator * denominator);
}

float VisibilitySmithGGX(float ndotv, float ndotl, float alpha)
{
    float alphaSquared = alpha * alpha;
    float viewMasking = ndotl * sqrt(ndotv * ndotv * (1.0 - alphaSquared) + alphaSquared);
    float lightMasking = ndotv * sqrt(ndotl * ndotl * (1.0 - alphaSquared) + alphaSquared);

    return 0.5 / (viewMasking + lightMasking);
}

float3 DirectSpecular(float3 normal, float3 viewDirection, float3 lightDirection, float roughness, float3 f0)
{
    float ndotv = saturate(dot(normal, viewDirection));
    float ndotl = saturate(dot(normal, lightDirection));

    if (ndotv <= 0.0 || ndotl <= 0.0)
    {
        return float3(0.0);
    }

    float3 halfVector = normalize(viewDirection + lightDirection);
    float alpha = roughness * roughness;
    float distribution = DistributionGGX(saturate(dot(normal, halfVector)), alpha);
    float visibility = VisibilitySmithGGX(ndotv, ndotl, alpha);
    float3 fresnel = FresnelSchlick(saturate(dot(viewDirection, halfVector)), f0);

    return distribution * visibility * fresnel * ndotl;
}

float3 ShadeSceneMaterial(SceneMaterial material, uint materialId, float3 worldPosition, float3 normal, float3 viewDirection, float3 lightDirection, float lightIntensity)
{
    float3 albedo = material.Albedo;

    if (materialId == 0)
    {
        float gridX = smoothstep(0.42, 0.49, abs(frac(worldPosition.x * 0.5) - 0.5));
        float gridZ = smoothstep(0.42, 0.49, abs(frac(worldPosition.z * 0.5) - 0.5));
        albedo *= lerp(0.84, 1.07, max(gridX, gridZ));
    }

    float roughness = max(material.Roughness, 0.08);
    float3 f0 = lerp(float3(0.04), albedo, material.Metallic);
    float ndotv = saturate(dot(normal, viewDirection));
    float ndotl = saturate(dot(normal, lightDirection));
    float3 viewFresnel = FresnelSchlick(ndotv, f0);
    float3 diffuseColor = albedo * (1.0 - material.Metallic);
    float3 direct = DirectSpecular(normal, viewDirection, lightDirection, roughness, f0);

    if (ndotl > 0.0 && ndotv > 0.0)
    {
        float3 halfVector = normalize(viewDirection + lightDirection);
        float3 lightFresnel = FresnelSchlick(saturate(dot(viewDirection, halfVector)), f0);
        direct += (1.0 - lightFresnel) * diffuseColor * ndotl / ScenePi;
    }

    float3 reflectedDirection = reflect(-viewDirection, normal);
    float3 ambientDiffuse = EnvironmentColor(normal) * diffuseColor * (1.0 - viewFresnel) * 0.65;
    float3 ambientSpecular = lerp(EnvironmentColor(reflectedDirection), EnvironmentColor(normal), roughness * roughness) * viewFresnel;

    return direct * SunColor * lightIntensity + ambientDiffuse + ambientSpecular + albedo * material.Emission;
}
