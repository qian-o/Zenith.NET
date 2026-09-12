#include "SceneCommon.slang"
#include "SurfaceCommon.slang"

struct BackgroundConstants
{
    float4x4 InvView;

    float4x4 InvProjection;

    float3 SunDirection;
};

struct FullscreenOutput
{
    float4 Position : SV_POSITION;

    float2 UV : TEXCOORD0;
};

struct BackgroundOutput
{
    float4 Color : SV_TARGET0;

    float LinearDepth : SV_TARGET1;
};

ConstantBuffer<BackgroundConstants> background;

[shader("vertex")]
FullscreenOutput FullscreenVS(uint vertexId : SV_VertexID)
{
    float2 uv = float2((vertexId << 1) & 2, vertexId & 2);

    FullscreenOutput output;
    output.Position = float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    output.UV = uv;

    return output;
}

[shader("fragment")]
BackgroundOutput BackgroundFS(FullscreenOutput input)
{
    float3 viewDirection = SurfaceViewRay(input.UV, background.InvProjection);
    float3 worldDirection = normalize(mul(float4(viewDirection, 0.0), background.InvView).xyz);

    BackgroundOutput output;
    output.Color = float4(SkyColor(worldDirection, background.SunDirection), 0.0);
    output.LinearDepth = 0.0;

    return output;
}
