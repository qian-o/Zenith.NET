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

    float3 WorldTangent : TEXCOORD2;

    float3 WorldBitangent : TEXCOORD3;

    float2 TexCoord : TEXCOORD4;

    float4 Color : COLOR0;
};

struct PSOutput
{
    float4 Albedo : SV_Target0;

    float4 Normal : SV_Target1;

    float4 Position : SV_Target2;

    float4 NormalizedDepth : SV_Target3;
};

float LinearizeDepth(float depth, float nearPlane, float farPlane)
{
    float z = depth * 2.0 - 1.0;
    return (2.0 * nearPlane * farPlane) / (farPlane + nearPlane - z * (farPlane - nearPlane));
}

PSInput VSMain(VSInput input)
{
    PSInput output;
    output.Position = mul(mul(float4(input.Position, 1.0), camera.View), camera.Projection);
    output.WorldPosition = input.Position.xyz;
    output.WorldNormal = normalize(input.Normal);
    output.WorldTangent = normalize(cross(abs(input.Normal.y) < 0.999 ? float3(0, 1, 0) : float3(1, 0, 0), input.Normal));
    output.WorldBitangent = cross(input.Normal, output.WorldTangent);
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

    float3 normal = normalize(input.WorldNormal);
    if ((material.Flags & MaterialFlags.HasNormalTexture) != 0)
    {
        float3 tangentNormal = normalTexture.Sample(linearSampler, input.TexCoord).xyz * 2.0 - 1.0;
        
        float3 T = normalize(input.WorldTangent);
        float3 B = normalize(input.WorldBitangent);
        float3 N = normalize(input.WorldNormal);
        float3x3 TBN = float3x3(T, B, N);
        
        normal = normalize(mul(tangentNormal, TBN));
    }

    float normalizedDepth = LinearizeDepth(input.Position.z, camera.NearPlane, camera.FarPlane) / camera.FarPlane;

    output.Albedo = float4(baseColor.rgb, 1.0);
    output.Normal = float4(normal, 1.0);
    output.Position = float4(input.WorldPosition, 1.0);
    output.NormalizedDepth = float4(normalizedDepth, normalizedDepth, normalizedDepth, 1.0);
    
    return output;
}