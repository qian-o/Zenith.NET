struct CSMData
{
    float4x4 View;

    float4x4 Projection;

    float NearPlane;

    float FarPlane;

    private float padding0;

    private float padding1;
}

ConstantBuffer<CSMData> data;

struct VSInput
{
    float3 Position : POSITION0;
};

struct PSInput
{
    float4 Position : SV_POSITION;
};

PSInput VSMain(VSInput input)
{
    PSInput output;
    output.Position = mul(mul(float4(input.Position, 1.0), data.View), data.Projection);

    return output;
}

void PSMain(PSInput input)
{
}
