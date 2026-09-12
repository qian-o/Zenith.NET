#include "SceneCommon.slang"

struct GlassConstants
{
    float4x4 View;

    float4x4 Projection;

    private float4 CameraPositionAndTime;

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
};

ConstantBuffer<GlassConstants> glass;

[shader("vertex")]
VSOutput GlassVS(VSInput input)
{
    VSOutput output;
    output.Position = mul(mul(float4(input.Position.xyz, 1.0), glass.View), glass.Projection);
    output.WorldPosition = input.Position.xyz;
    output.Normal = input.NormalAndMaterial.xyz;

    return output;
}

[shader("fragment")]
float4 GlassFS(VSOutput input) : SV_TARGET
{
    float3 normal = normalize(input.Normal);
    float3 viewDirection = normalize(glass.CameraPosition - input.WorldPosition);
    float facing = abs(dot(normal, viewDirection));
    float fresnel = FresnelSchlick(facing, float3(0.04)).x;
    float3 reflected = EnvironmentColor(reflect(-viewDirection, normal));
    float alpha = fresnel;

    return float4(reflected * alpha, alpha);
}
