#include "SceneCommon.slang"

static const uint FrameMaterialId = 4;

struct SceneConstants
{
    float4x4 View;

    float4x4 Projection;

    private float4 CameraPositionAndTime;

    private float4 LightDirectionAndIntensity;

    DescriptorHandle<StructuredBuffer<SceneMaterial>> Materials;

    property float3 CameraPosition
    {
        get {
            return CameraPositionAndTime.xyz;
        }
    }

    property float Time
    {
        get {
            return CameraPositionAndTime.w;
        }
    }

    property float3 LightDirection
    {
        get {
            return LightDirectionAndIntensity.xyz;
        }
    }

    property float LightIntensity
    {
        get {
            return LightDirectionAndIntensity.w;
        }
    }
};

struct VSInput
{
    float4 Position : POSITION;

    float4 NormalAndMaterial : NORMAL;
};

struct VSOutput
{
    float4 Position : SV_POSITION;

    float3 WorldPosition : TEXCOORD0;

    float3 Normal : TEXCOORD1;

    float ViewDepth : TEXCOORD2;

    nointerpolation uint MaterialId : TEXCOORD3;
};

struct FSOutput
{
    float4 Color : SV_TARGET0;

    float LinearDepth : SV_TARGET1;
};

ConstantBuffer<SceneConstants> scene;

[shader("vertex")]
VSOutput VSMain(VSInput input)
{
    float4 worldPosition = float4(input.Position.xyz, 1.0);
    float4 viewPosition = mul(worldPosition, scene.View);

    VSOutput output;
    output.Position = mul(viewPosition, scene.Projection);
    output.WorldPosition = input.Position.xyz;
    output.Normal = input.NormalAndMaterial.xyz;
    output.ViewDepth = -viewPosition.z;
    output.MaterialId = asuint(input.NormalAndMaterial.w);

    return output;
}

[shader("fragment")]
FSOutput FSMain(VSOutput input)
{
    SceneMaterial material = scene.Materials[input.MaterialId];
    float3 normal = normalize(input.Normal);
    float3 viewDirection = normalize(scene.CameraPosition - input.WorldPosition);
    float3 lightDirection = normalize(-scene.LightDirection);
    float3 halfVector = normalize(viewDirection + lightDirection);

    float ndotl = saturate(dot(normal, lightDirection));
    float ndoth = saturate(dot(normal, halfVector));
    float roughness = max(material.Roughness, 0.035);
    float exponent = max(2.0 / (roughness * roughness) - 2.0, 1.0);
    float3 f0 = lerp(float3(0.035, 0.038, 0.04), material.Albedo, material.Metallic);
    float3 fresnel = FresnelSchlick(saturate(dot(halfVector, viewDirection)), f0);
    float3 diffuse = material.Albedo * (1.0 - material.Metallic) * (0.20 + ndotl * scene.LightIntensity);
    float3 specular = fresnel * pow(ndoth, exponent) * (0.18 + scene.LightIntensity * 1.8);
    float3 environment = SkyColor(reflect(-viewDirection, normal)) * fresnel * (0.18 + 0.82 * (1.0 - roughness));

    if (input.MaterialId == 0)
    {
        float gridX = smoothstep(0.42, 0.49, abs(frac(input.WorldPosition.x * 0.5) - 0.5));
        float gridZ = smoothstep(0.42, 0.49, abs(frac(input.WorldPosition.z * 0.5) - 0.5));
        diffuse *= lerp(0.84, 1.07, max(gridX, gridZ));
    }

    float3 color = diffuse + specular + environment + material.Albedo * material.Emission;

    FSOutput output;
    output.Color = float4(color, input.MaterialId == FrameMaterialId ? 1.0 : 0.0);
    output.LinearDepth = input.ViewDepth;

    return output;
}
