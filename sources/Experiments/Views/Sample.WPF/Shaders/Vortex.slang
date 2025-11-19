// https://www.shadertoy.com/view/DlVczV
struct VSInput
{
    float2 Position : POSITION0;

    float2 UV : TEXCOORD0;
};

struct VSOutput
{
    float4 Position : SV_POSITION;

    float2 UV : TEXCOORD0;
};

struct Constants
{
    float2 Resolution;

    float TotalTime;
};

uniform Constants constants;

float mod(float x, float y)
{
    return x - y * floor(x / y);
}

VSOutput VSMain(VSInput input)
{
    VSOutput output;

    output.Position = float4(input.Position, 0.0, 1.0);
    output.UV = input.UV;

    return output;
}

float4 PSMain(VSOutput input) : SV_TARGET
{
    float4 color = float4(0.0, 0.0, 0.0, 0.0);

    float2 v = constants.Resolution;
    float2 p = (input.UV * constants.Resolution * 2.0 - v) / v.y;

    for (float i = 0.2, l; i < 1.0; i += 0.05)
    {
        color += (cos(i * 5.0 + float4(0.0, 1.0, 2.0, 3.0)) + 1.0) * (1.0 + v.y / (l = length(v) + 0.003)) / l;
        v = float2(mod(atan2(p.x, p.y) + i + i * constants.TotalTime, 6.28318530718) - 3.14159265359, 1.0) * length(p) - i;
        v.x -= clamp(v.x += i, -i, i);
    }

    color = tanh(color / 1e2);

    return color;
}