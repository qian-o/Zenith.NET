struct Material
{
    private float4 AlbedoAndEmission;

    float Metallic;

    float Roughness;

    private float padding0;

    private float padding1;

    property float3 Albedo
    {
        get {
            return AlbedoAndEmission.xyz;
        }
    }

    property float Emission
    {
        get {
            return AlbedoAndEmission.w;
        }
    }
};

struct RasterConstants
{
    float4x4 Model;

    float4x4 View;

    float4x4 Projection;

    private float4 LightPosAndPadding;

    private float4 LightColorAndPadding;

    private float4 CameraPosAndPadding;

    property float3 LightPos
    {
        get {
            return LightPosAndPadding.xyz;
        }
    }

    property float3 LightColor
    {
        get {
            return LightColorAndPadding.xyz;
        }
    }

    property float3 CameraPos
    {
        get {
            return CameraPosAndPadding.xyz;
        }
    }
};

struct VSInput
{
    private float4 PositionAndPadding : POSITION0;

    private float4 NormalAndMaterialID : NORMAL0;

    property float3 Position
    {
        get {
            return PositionAndPadding.xyz;
        }
    }

    property float3 Normal
    {
        get {
            return NormalAndMaterialID.xyz;
        }
    }

    property uint MaterialID
    {
        get {
            return asuint(NormalAndMaterialID.w);
        }
    }
};

struct PSInput
{
    float4 Position : SV_POSITION;

    float3 WorldPos : TEXCOORD0;

    float3 Normal : TEXCOORD1;

    nointerpolation uint MaterialID : TEXCOORD2;
};

ConstantBuffer<RasterConstants> raster;
StructuredBuffer<Material> materials;

PSInput VSMain(VSInput input)
{
    float4 worldPos = mul(float4(input.Position, 1.0), raster.Model);

    PSInput output;
    output.Position = mul(mul(worldPos, raster.View), raster.Projection);
    output.WorldPos = worldPos.xyz;
    output.Normal = normalize(mul(float4(input.Normal, 0.0), raster.Model).xyz);
    output.MaterialID = input.MaterialID;

    return output;
}

float4 PSMain(PSInput input) : SV_TARGET
{
    Material mat = materials[input.MaterialID];

    if (mat.Emission > 0.0)
    {
        float3 emissive = mat.Albedo * mat.Emission;
        float3 mapped = emissive / (emissive + 1.0);

        return float4(pow(mapped, 1.0 / 2.2), 1.0);
    }

    float3 N = normalize(input.Normal);
    float3 worldPos = input.WorldPos;
    float3 L = normalize(raster.LightPos - worldPos);
    float3 V = normalize(raster.CameraPos - worldPos);
    float3 H = normalize(L + V);

    float NdotL = max(dot(N, L), 0.0);
    float NdotH = max(dot(N, H), 0.0);
    float spec = pow(NdotH, 64.0);

    float dist = length(raster.LightPos - worldPos);
    float atten = 1.0 / (1.0 + 0.000005 * dist * dist);

    float hemiFactor = N.y * 0.5 + 0.5;
    float3 ambient = mat.Albedo * lerp(0.06, 0.15, hemiFactor);
    float3 diffuse = mat.Albedo * raster.LightColor * NdotL * atten;
    float3 specular = raster.LightColor * spec * atten * 0.1;

    float3 color = ambient + diffuse + specular;

    // ACES tonemapping
    float3 a = color * (color * 2.51 + 0.03);
    float3 b = color * (color * 2.43 + 0.59) + 0.14;
    color = saturate(a / b);

    color = pow(color, 1.0 / 2.2);

    return float4(color, 1.0);
}
