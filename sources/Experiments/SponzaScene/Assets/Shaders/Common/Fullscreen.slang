public struct PSInput
{
    public float4 Position : SV_POSITION;

    public float2 TexCoord : TEXCOORD0;
};

PSInput VSMain(uint vertexID: SV_VertexID)
{
    PSInput output;
    float2 pos = float2((vertexID << 1) & 2, vertexID & 2);
    output.Position = float4(pos * 2.0 - 1.0, 0.0, 1.0);
    output.TexCoord = float2(pos.x, 1.0 - pos.y);

    return output;
}
