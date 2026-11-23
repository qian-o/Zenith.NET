struct OutputConstants
{
    private uint4 Config0;

    private float4 Config1;

    DescriptorHandle<Texture2D<float4>> PrimaryTexture;

    DescriptorHandle<Texture2D<float4>> SecondaryTexture;

    DescriptorHandle<WTexture2D<float4>> Destination;

    DescriptorHandle<SamplerState> LinearSampler;

    property uint Width
    {
        get
        {
            return Config0.x;
        }
    }

    property uint Height
    {
        get
        {
            return Config0.y;
        }
    }

    property uint FrameOrFirstMip
    {
        get
        {
            return Config0.z;
        }
    }

    property float FilterRadius
    {
        get
        {
            return asfloat(Config0.w);
        }
    }

    property float2 InvSourceSize
    {
        get
        {
            return Config1.xy;
        }
    }

    property float ParamA
    {
        get
        {
            return Config1.z;
        }
    }

    property float ParamB
    {
        get
        {
            return Config1.w;
        }
    }
};

ConstantBuffer<OutputConstants> output;

uint Hash(uint value)
{
    uint state = value * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;

    return (word >> 22u) ^ word;
}

float Luminance(float3 color)
{
    return dot(color, float3(0.2126, 0.7152, 0.0722));
}

float3 SoftThreshold(float3 color, float threshold, float knee)
{
    float3 clamped = min(max(color, 0.0), float3(64.0));
    float brightness = max(clamped.r, max(clamped.g, clamped.b));
    float soft = clamp(brightness - (threshold - knee), 0.0, 2.0 * knee);
    soft = (soft * soft) / max(4.0 * knee, 1e-4);
    float contribution = max(soft, brightness - threshold) / max(brightness, 1e-4);

    return clamped * max(contribution, 0.0);
}

float KarisWeight(float3 boxAverage)
{
    return 1.0 / (1.0 + Luminance(boxAverage));
}

float3 SampleBloomSource(float2 uv)
{
    float3 sampleColor = max(output.PrimaryTexture.SampleLevel(output.LinearSampler, uv, 0).rgb, 0.0);
    if (output.FrameOrFirstMip != 0u)
    {
        return SoftThreshold(sampleColor, output.ParamA, output.ParamB);
    }

    return sampleColor;
}

[shader("compute")]
[numthreads(8, 8, 1)]
void BloomDownsampleCS(uint3 dispatchThreadId: SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadId.xy;
    if (pixel.x >= output.Width || pixel.y >= output.Height)
    {
        return;
    }

    float2 uv = (float2(pixel) + 0.5) / float2(output.Width, output.Height);
    float2 texel = output.InvSourceSize;

    float3 tl = SampleBloomSource(uv + float2(-2.0, -2.0) * texel);
    float3 t = SampleBloomSource(uv + float2(0.0, -2.0) * texel);
    float3 tr = SampleBloomSource(uv + float2(2.0, -2.0) * texel);
    float3 l = SampleBloomSource(uv + float2(-2.0, 0.0) * texel);
    float3 c = SampleBloomSource(uv);
    float3 r = SampleBloomSource(uv + float2(2.0, 0.0) * texel);
    float3 bl = SampleBloomSource(uv + float2(-2.0, 2.0) * texel);
    float3 b = SampleBloomSource(uv + float2(0.0, 2.0) * texel);
    float3 br = SampleBloomSource(uv + float2(2.0, 2.0) * texel);

    float3 itl = SampleBloomSource(uv + float2(-1.0, -1.0) * texel);
    float3 itr = SampleBloomSource(uv + float2(1.0, -1.0) * texel);
    float3 ibl = SampleBloomSource(uv + float2(-1.0, 1.0) * texel);
    float3 ibr = SampleBloomSource(uv + float2(1.0, 1.0) * texel);

    float3 grpCenter = 0.25 * (itl + itr + ibl + ibr);
    float3 grpTL = 0.25 * (tl + t + l + c);
    float3 grpTR = 0.25 * (t + tr + c + r);
    float3 grpBL = 0.25 * (l + c + bl + b);
    float3 grpBR = 0.25 * (c + r + b + br);

    float3 result;
    if (output.FrameOrFirstMip != 0u)
    {
        float wCenter = 0.500 * KarisWeight(grpCenter);
        float wTL = 0.125 * KarisWeight(grpTL);
        float wTR = 0.125 * KarisWeight(grpTR);
        float wBL = 0.125 * KarisWeight(grpBL);
        float wBR = 0.125 * KarisWeight(grpBR);
        float wSum = max(wCenter + wTL + wTR + wBL + wBR, 1e-5);
        result = (grpCenter * wCenter + grpTL * wTL + grpTR * wTR + grpBL * wBL + grpBR * wBR) / wSum;
    }
    else
    {
        result = grpCenter * 0.5 + (grpTL + grpTR + grpBL + grpBR) * 0.125;
    }

    output.Destination.Store(pixel, float4(result, 1.0));
}

[shader("compute")]
[numthreads(8, 8, 1)]
void BloomUpsampleCS(uint3 dispatchThreadId: SV_DispatchThreadID)
{
    uint2 pixel = dispatchThreadId.xy;
    if (pixel.x >= output.Width || pixel.y >= output.Height)
    {
        return;
    }

    float2 uv = (float2(pixel) + 0.5) / float2(output.Width, output.Height);
    float2 d = output.InvSourceSize * output.FilterRadius;

    float3 s0 = output.PrimaryTexture.SampleLevel(output.LinearSampler, uv + float2(-d.x, -d.y), 0).rgb;
    float3 s1 = output.PrimaryTexture.SampleLevel(output.LinearSampler, uv + float2(0.0, -d.y), 0).rgb * 2.0;
    float3 s2 = output.PrimaryTexture.SampleLevel(output.LinearSampler, uv + float2(d.x, -d.y), 0).rgb;
    float3 s3 = output.PrimaryTexture.SampleLevel(output.LinearSampler, uv + float2(-d.x, 0.0), 0).rgb * 2.0;
    float3 s4 = output.PrimaryTexture.SampleLevel(output.LinearSampler, uv, 0).rgb * 4.0;
    float3 s5 = output.PrimaryTexture.SampleLevel(output.LinearSampler, uv + float2(d.x, 0.0), 0).rgb * 2.0;
    float3 s6 = output.PrimaryTexture.SampleLevel(output.LinearSampler, uv + float2(-d.x, d.y), 0).rgb;
    float3 s7 = output.PrimaryTexture.SampleLevel(output.LinearSampler, uv + float2(0.0, d.y), 0).rgb * 2.0;
    float3 s8 = output.PrimaryTexture.SampleLevel(output.LinearSampler, uv + float2(d.x, d.y), 0).rgb;

    float3 upsampled = (s0 + s1 + s2 + s3 + s4 + s5 + s6 + s7 + s8) * (1.0 / 16.0);
    float3 detail = output.SecondaryTexture.Load(int3(pixel, 0)).rgb;

    output.Destination.Store(pixel, float4(detail + upsampled, 1.0));
}

float3 AcesFilm(float3 x)
{
    float3 numerator = x * (x * 2.51 + 0.03);
    float3 denominator = x * (x * 2.43 + 0.59) + 0.14;

    return saturate(numerator / denominator);
}

float3 LinearToSrgb(float3 linearColor)
{
    float3 clamped = max(linearColor, 0.0);

    return select(clamped <= 0.0031308, clamped * 12.92, 1.055 * pow(clamped, 1.0 / 2.4) - 0.055);
}

[shader("vertex")]
float4 FullscreenVS(uint vertexId: SV_VertexID) : SV_POSITION
{
    float2 uv = float2((vertexId << 1u) & 2u, vertexId & 2u);

    return float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
}

[shader("fragment")]
float4 ToneMappingFS(float4 position: SV_POSITION) : SV_TARGET
{
    uint2 pixel = uint2(position.xy);
    float exposure = output.ParamA;
    float bloomIntensity = output.ParamB;
    float3 hdr = max(output.PrimaryTexture.Load(int3(pixel, 0)).rgb, 0.0) * exposure;

    float2 uv = (float2(pixel) + 0.5) / float2(max(output.Width, 1u), max(output.Height, 1u));
    float3 bloomColor = max(output.SecondaryTexture.SampleLevel(output.LinearSampler, uv, 0).rgb, 0.0);
    hdr += bloomColor * (bloomIntensity * exposure);

    float3 srgbColor = LinearToSrgb(AcesFilm(hdr));

    uint seed = Hash(pixel.x + pixel.y * max(output.Width, 1u) + output.FrameOrFirstMip * 1664525u);
    float ditherA = float(seed >> 8u) * (1.0 / 16777216.0);
    float ditherB = float(Hash(seed) >> 8u) * (1.0 / 16777216.0);
    srgbColor += (ditherA - ditherB) * (1.0 / 255.0);

    return float4(saturate(srgbColor), 1.0);
}
