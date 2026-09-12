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
        get
        {
            return CameraPositionAndTime.xyz;
        }
    }

    property float Time
    {
        get
        {
            return CameraPositionAndTime.w;
        }
    }

    property float3 LightDirection
    {
        get
        {
            return LightDirectionAndIntensity.xyz;
        }
    }

    property float LightIntensity
    {
        get
        {
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
    float3 color = ShadeSceneMaterial(material, input.MaterialId, input.WorldPosition, normal, viewDirection, lightDirection, scene.LightIntensity);

    FSOutput output;
    output.Color = float4(color, input.MaterialId == FrameMaterialId ? 1.0 : 0.0);
    output.LinearDepth = input.ViewDepth;

    return output;
}
