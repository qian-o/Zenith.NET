import "Common/VSInput";

enum TextureFlags : int
{
    HasBaseColorTexture = 1 << 0,

    HasNormalTexture = 1 << 1
};

struct CameraConstants
{
    float4x4 View;

    float4x4 Projection;
};

struct MaterialConstants
{
    float4 BaseColorFactor;

    TextureFlags Flags;
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
};

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
    if ((material.Flags & TextureFlags.HasBaseColorTexture) != 0)
    {
        baseColor *= baseColorTexture.Sample(linearSampler, input.TexCoord);
    }
    baseColor *= input.Color;

    float3 normal = input.WorldNormal;
    if ((material.Flags & TextureFlags.HasNormalTexture) != 0)
    {
        float3 normalMap = normalTexture.Sample(linearSampler, input.TexCoord).xyz * 2.0 - 1.0;
        normal = normalize(normalMap);
    }

    output.Albedo = float4(baseColor.rgb, 1.0);
    output.Normal = float4(normalize(normal), 1.0);
    output.Position = float4(input.WorldPosition, 1.0);

    return output;
}
