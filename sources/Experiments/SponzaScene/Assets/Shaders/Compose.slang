import "Common/Fullscreen";

struct ComposeConstants
{
    float AOStrength;

    float BloomIntensity;
};

ConstantBuffer<ComposeConstants> constants;
Texture2D litColorTexture;
Texture2D ssaoTexture;
Texture2D bloomTexture;
SamplerState pointSampler;

float4 PSMain(PSInput input) : SV_Target0
{
    float2 uv = input.TexCoord;

    float3 color = litColorTexture.Sample(pointSampler, uv).rgb;

    color *= lerp(1.0, ssaoTexture.Sample(pointSampler, uv).r, constants.AOStrength);

    color += bloomTexture.Sample(pointSampler, uv).rgb * constants.BloomIntensity;

    return float4(color, 1.0);
}
