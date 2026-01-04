struct CSMData
{
    float4x4 View;

    float4x4 Projection;

    float NearPlane;

    float FarPlane;
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

float LinearizeDepth(float depth, float nearPlane, float farPlane)
{
    float z = depth * 2.0 - 1.0;

    return (2.0 * nearPlane * farPlane) / (farPlane + nearPlane - z * (farPlane - nearPlane));
}

PSInput VSMain(VSInput input)
{
    PSInput output;
    output.Position = mul(mul(float4(input.Position, 1.0), data.View), data.Projection);

    return output;
}

float4 PSMain(PSInput input) : SV_Target0
{
    float normalizedDepth = LinearizeDepth(input.Position.z, data.NearPlane, data.FarPlane) / data.FarPlane;

    return float4(normalizedDepth, normalizedDepth, normalizedDepth, 1.0);
}
