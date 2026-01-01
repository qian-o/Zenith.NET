static const float EXPOSURE = 1.2;
static const float CONTRAST = 1.1;
static const float SATURATION = 1.1;

static const float3 LUMA_COEFFICIENTS = float3(0.2126, 0.7152, 0.0722);
static const float MID_GRAY = 0.18;

struct ComposeConstants
{
    float AOStrength;

    float BloomIntensity;
};

ConstantBuffer<ComposeConstants> constants;
Texture2D litColorTexture;
Texture2D ssaoTexture;
Texture2D bloomTexture;
RWTexture2D outputTexture;
SamplerState pointSampler;

float Luminance(float3 color)
{
    return dot(color, LUMA_COEFFICIENTS);
}

float3 ACESFilm(float3 x)
{
    const float a = 2.51;
    const float b = 0.03;
    const float c = 2.43;
    const float d = 0.59;
    const float e = 0.14;

    return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
}

float3 AdjustContrast(float3 color, float contrast)
{
    return max((color - MID_GRAY) * contrast + MID_GRAY, 0.0);
}

float3 AdjustSaturation(float3 color, float saturation)
{
    float luma = Luminance(color);

    return max(lerp(luma, color, saturation), 0.0);
}

[numthreads(16, 16, 1)]
void CSMain(uint3 dispatchThreadID: SV_DispatchThreadID)
{
    uint width, height;
    outputTexture.GetDimensions(width, height);

    if (dispatchThreadID.x >= width || dispatchThreadID.y >= height)
    {
        return;
    }

    float2 uv = (float2(dispatchThreadID.xy) + 0.5) / float2(width, height);

    float3 color = litColorTexture.SampleLevel(pointSampler, uv, 0).rgb;
    float ao = ssaoTexture.SampleLevel(pointSampler, uv, 0).r;
    float3 bloom = bloomTexture.SampleLevel(pointSampler, uv, 0).rgb;

    color *= lerp(1.0, ao, constants.AOStrength);
    color += bloom * constants.BloomIntensity;

    color *= EXPOSURE;
    color = ACESFilm(color);
    color = AdjustContrast(color, CONTRAST);
    color = AdjustSaturation(color, SATURATION);

    outputTexture[dispatchThreadID.xy] = float4(color, 1.0);
}
