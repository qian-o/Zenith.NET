#include "SceneCommon.slang"

struct GlassConstants
{
    float4x4 View;

    float4x4 Projection;

    private float4 CameraPositionAndTime;

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
    float fresnel = 0.028 + 0.972 * pow(1.0 - facing, 5.0);
    float verticalEdge = pow(saturate(abs(input.WorldPosition.y - 2.6) / 2.6), 18.0);
    float micro = 0.5 + 0.5 * sin(input.WorldPosition.x * 31.0 + input.WorldPosition.y * 17.0 + glass.Time * 0.1);
    float alpha = saturate(0.018 + fresnel * 0.34 + verticalEdge * 0.04 + micro * 0.004);
    float3 tint = lerp(float3(0.42, 0.74, 0.78), float3(0.83, 0.96, 1.0), fresnel);
    float3 color = ToSRGB(ACESFilm(tint * (0.42 + fresnel * 0.75)));

    return float4(color * alpha, alpha);
}
