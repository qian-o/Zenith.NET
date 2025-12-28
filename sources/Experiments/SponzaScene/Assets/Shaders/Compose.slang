import "Common/Fullscreen";

struct ComposeConstants
{
    float AOStrength;
};

ConstantBuffer<ComposeConstants> constants;
Texture2D albedoTexture;
Texture2D ssaoTexture;
SamplerState pointSampler;

float4 PSMain(PSInput input) : SV_Target0
{
    float2 uv = input.TexCoord;

    float3 albedo = albedoTexture.Sample(pointSampler, uv).rgb;
    float ao = lerp(1.0, ssaoTexture.Sample(pointSampler, uv).r, constants.AOStrength);

    return float4(albedo * ao, 1.0);
}