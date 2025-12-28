import "Common/VSInput";

enum MaterialFlags : int
{
    None = 0,

    UseAlphaCutoff = 1 << 0,

    HasBaseColorTexture = 1 << 1,

    HasNormalTexture = 1 << 2
};

struct CameraConstants
{
    float4x4 View;

    float4x4 Projection;

    float NearPlane;

    float FarPlane;
};

struct MaterialConstants
{
    float AlphaCutoff;

    float4 BaseColorFactor;

    MaterialFlags Flags;
};

ConstantBuffer<CameraConstants> camera;
ConstantBuffer<MaterialConstants> material;
Texture2D baseColorTexture;
Texture2D normalTexture;
SamplerState linearSampler;

struct PSInput
{
    float4 Position : SV_POSITION;

    float3 WorldPosition : TEXCOORD0;

    float3 WorldNormal : TEXCOORD1;

    float2 TexCoord : TEXCOORD2;

    float4 Color : COLOR0;
};

struct PSOutput
{
    // RGB: Base Color, A: Unused
    float4 Albedo : SV_Target0;

    // RGB: World Normal, A: Unused
    float4 Normal : SV_Target1;

    // RGB: World Position, A: Unused
    float4 Position : SV_Target2;

    // RGB: Linear Depth Visualization, A: Unused
    float4 LinearDepth : SV_Target3;
};

float LinearizeDepth(float depth, float nearPlane, float farPlane)
{
    return nearPlane * farPlane / (farPlane - depth * (farPlane - nearPlane));
}

PSInput VSMain(VSInput input)
{
    PSInput output;
    output.Position = mul(mul(float4(input.Position, 1.0), camera.View), camera.Projection);
    output.WorldPosition = input.Position.xyz;
    output.WorldNormal = normalize(input.Normal);
    output.TexCoord = input.TexCoord;
    output.Color = input.Color;

    return output;
}

PSOutput PSMain(PSInput input)
{
    PSOutput output;

    float4 baseColor = material.BaseColorFactor;
    if ((material.Flags & MaterialFlags.HasBaseColorTexture) != 0)
    {
        baseColor *= baseColorTexture.Sample(linearSampler, input.TexCoord);
    }
    baseColor *= input.Color;

    if ((material.Flags & MaterialFlags.UseAlphaCutoff) != 0)
    {
        clip(baseColor.a - material.AlphaCutoff);
    }

    float3 normal = input.WorldNormal;
    if ((material.Flags & MaterialFlags.HasNormalTexture) != 0)
    {
        float3 normalMap = normalTexture.Sample(linearSampler, input.TexCoord).xyz * 2.0 - 1.0;
        normal = normalize(normalMap);
    }

    float linearDepth = LinearizeDepth(input.Position.z, camera.NearPlane, camera.FarPlane);
    float normalizedDepth = (linearDepth - camera.NearPlane) / (camera.FarPlane - camera.NearPlane);

    output.Albedo = float4(baseColor.rgb, 1.0);
    output.Normal = float4(normalize(normal), 1.0);
    output.Position = float4(input.WorldPosition, 1.0);
    output.LinearDepth = float4(normalizedDepth, normalizedDepth, normalizedDepth, 1.0);

    return output;
}
