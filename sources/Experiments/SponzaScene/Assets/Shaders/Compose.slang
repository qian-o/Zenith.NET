static const float EXPOSURE = 1.2;
static const float CONTRAST = 1.1;
static const float SATURATION = 1.1;
static const float3 LUMA_COEFFICIENTS = float3(0.2126, 0.7152, 0.0722);

struct ComposeConstants
{
    float AOStrength;

    float BloomIntensity;

    float VolumetricIntensity;
};

ConstantBuffer<ComposeConstants> constants;
Texture2D litColorTexture;
Texture2D gtaoTexture;
Texture2D bloomTexture;
Texture2D volumetricTexture;
RWTexture2D outputTexture;
SamplerState pointSampler;

float3 ACESFilm(float3 x)
{
    const float a = 2.51;
    const float b = 0.03;
    const float c = 2.43;
    const float d = 0.59;
    const float e = 0.14;

    return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
}

float3 ColorGrade(float3 color)
{
    const float midGray = 0.18;

    color = (color - midGray) * CONTRAST + midGray;

    float luma = dot(color, LUMA_COEFFICIENTS);
    color = lerp(luma, color, SATURATION);

    return max(color, 0.0);
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
    float ao = gtaoTexture.SampleLevel(pointSampler, uv, 0).r;
    float3 bloom = bloomTexture.SampleLevel(pointSampler, uv, 0).rgb;
    float volumetric = volumetricTexture.SampleLevel(pointSampler, uv, 0).r;

    color *= 1.0 - (1.0 - ao) * constants.AOStrength;
    color += bloom * constants.BloomIntensity + volumetric * constants.VolumetricIntensity;

    color = ACESFilm(color * EXPOSURE);
    color = ColorGrade(color);

    outputTexture[dispatchThreadID.xy] = float4(color, 1.0);
}
