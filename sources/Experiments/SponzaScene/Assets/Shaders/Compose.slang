import "Common/Fullscreen";

Texture2D albedoTexture;
Texture2D ssaoTexture;
SamplerState pointSampler;

float4 PSMain(PSInput input) : SV_Target0
{
    float2 uv = input.TexCoord;

    float3 albedo = albedoTexture.Sample(pointSampler, uv).rgb;
    float ao = ssaoTexture.Sample(pointSampler, uv).r;

    return float4(albedo * ao, 1.0);
}