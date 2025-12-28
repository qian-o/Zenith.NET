import "Common/Fullscreen";

float4 PSMain(PSInput input) : SV_Target0
{
    return float4(input.TexCoord, 0.0, 1.0);
}