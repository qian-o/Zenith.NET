#include "Common.slang"

static const uint FlagFirstMip = 1u << 0;
static const uint FlagOutputLuminance = 1u << 1;

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
        get {
            return Config0.x;
        }
    }

    property uint Height
    {
        get {
            return Config0.y;
        }
    }

    property uint FrameIndex
    {
        get {
            return Config0.z;
        }
    }

    property uint Flags
    {
        get {
            return Config0.w;
        }
    }

    property float2 TexelSize
    {
        get {
            return Config1.xy;
        }
    }

    property float ParameterA
    {
        get {
            return Config1.z;
        }
    }

    property float ParameterB
    {
        get {
            return Config1.w;
        }
    }
};

struct FullscreenVSOutput
{
    float4 Position : SV_POSITION;

    float2 UV : TEXCOORD0;
};

ConstantBuffer<OutputConstants> output;

float LinearLuminance(float3 color)
{
    return dot(color, float3(0.2126, 0.7152, 0.0722));
}

float PerceptualLuminance(float3 color)
{
    return dot(color, float3(0.299, 0.587, 0.114));
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
    return 1.0 / (1.0 + LinearLuminance(boxAverage));
}

float3 SampleBloomSource(float2 uv)
{
    float3 sampleColor = max(output.PrimaryTexture.SampleLevel(output.LinearSampler, uv, 0).rgb, 0.0);
    if ((output.Flags & FlagFirstMip) != 0u)
    {
        return SoftThreshold(sampleColor, output.ParameterA, output.ParameterB);
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
    float2 texel = output.TexelSize;

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
    if ((output.Flags & FlagFirstMip) != 0u)
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
    float2 d = output.TexelSize * output.ParameterA;

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

float3 AcesFilm(float3 color)
{
    static const float3x3 AcesInput =
        float3x3(0.59719, 0.35458, 0.04823, 0.07600, 0.90834, 0.01566, 0.02840, 0.13383, 0.83777);

    static const float3x3 AcesOutput =
        float3x3(1.60475, -0.53108, -0.07367, -0.10208, 1.10813, -0.00605, -0.00327, -0.07276, 1.07602);

    float3 v = mul(AcesInput, max(color, 0.0) * 1.9);
    float3 a = v * (v + 0.0245786) - 0.000090537;
    float3 b = v * (0.983729 * v + 0.4329510) + 0.238081;

    return saturate(mul(AcesOutput, a / b));
}

float3 LinearToSrgb(float3 linearColor)
{
    float3 clamped = max(linearColor, 0.0);

    return select(clamped <= 0.0031308, clamped * 12.92, 1.055 * pow(clamped, 1.0 / 2.4) - 0.055);
}

float3 ApplyTriangularDither(float3 srgbColor, uint2 pixel, uint width, uint frameIndex)
{
    uint seed = PCGHash(pixel.x + pixel.y * max(width, 1u) + frameIndex * 1664525u);
    float ditherA = float(seed >> 8u) * (1.0 / 16777216.0);
    float ditherB = float(PCGHash(seed) >> 8u) * (1.0 / 16777216.0);

    return saturate(srgbColor + (ditherA - ditherB) * (1.0 / 255.0));
}

[shader("vertex")]
FullscreenVSOutput FullscreenVS(uint vertexId: SV_VertexID)
{
    float2 uv = float2((vertexId << 1u) & 2u, vertexId & 2u);

    FullscreenVSOutput result;
    result.Position = float4(uv * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    result.UV = uv;

    return result;
}

[shader("fragment")]
float4 ToneMappingFS(FullscreenVSOutput input) : SV_TARGET
{
    uint2 pixel = uint2(input.Position.xy);
    float exposure = output.ParameterA;
    float bloomIntensity = output.ParameterB;
    float3 hdr = max(output.PrimaryTexture.Load(int3(pixel, 0)).rgb, 0.0) * exposure;

    float3 bloomColor = max(output.SecondaryTexture.SampleLevel(output.LinearSampler, input.UV, 0).rgb, 0.0);
    hdr += bloomColor * (bloomIntensity * exposure);

    float3 srgbColor = LinearToSrgb(AcesFilm(hdr));
    bool outputLuminance = (output.Flags & FlagOutputLuminance) != 0u;
    if (!outputLuminance)
    {
        srgbColor = ApplyTriangularDither(srgbColor, pixel, output.Width, output.FrameIndex);
    }

    float alpha = outputLuminance ? PerceptualLuminance(srgbColor) : 1.0;

    return float4(srgbColor, alpha);
}

[shader("fragment")]
float4 AntialiasingFS(FullscreenVSOutput input) : SV_TARGET
{
    uint2 pixel = uint2(input.Position.xy);
    float2 texel = output.TexelSize;
    float4 rgbyM = output.PrimaryTexture.SampleLevel(output.LinearSampler, input.UV, 0);
    float lumaM = rgbyM.a;
    float lumaS = output.PrimaryTexture.SampleLevel(output.LinearSampler, input.UV + float2(0.0, texel.y), 0).a;
    float lumaE = output.PrimaryTexture.SampleLevel(output.LinearSampler, input.UV + float2(texel.x, 0.0), 0).a;
    float lumaN = output.PrimaryTexture.SampleLevel(output.LinearSampler, input.UV - float2(0.0, texel.y), 0).a;
    float lumaW = output.PrimaryTexture.SampleLevel(output.LinearSampler, input.UV - float2(texel.x, 0.0), 0).a;

    float rangeMax = max(max(lumaN, lumaW), max(max(lumaE, lumaS), lumaM));
    float rangeMin = min(min(lumaN, lumaW), min(min(lumaE, lumaS), lumaM));
    float range = rangeMax - rangeMin;
    if (range < max(0.0312, rangeMax * 0.125))
    {
        return float4(ApplyTriangularDither(rgbyM.rgb, pixel, output.Width, output.FrameIndex), 1.0);
    }

    float lumaNW = output.PrimaryTexture.SampleLevel(output.LinearSampler, input.UV + float2(-texel.x, -texel.y), 0).a;
    float lumaSE = output.PrimaryTexture.SampleLevel(output.LinearSampler, input.UV + float2(texel.x, texel.y), 0).a;
    float lumaNE = output.PrimaryTexture.SampleLevel(output.LinearSampler, input.UV + float2(texel.x, -texel.y), 0).a;
    float lumaSW = output.PrimaryTexture.SampleLevel(output.LinearSampler, input.UV + float2(-texel.x, texel.y), 0).a;

    float lumaNS = lumaN + lumaS;
    float lumaWE = lumaW + lumaE;
    float subpixNSWE = lumaNS + lumaWE;
    float subpixNWSW = lumaNW + lumaSW;
    float subpixNESE = lumaNE + lumaSE;
    float subpixNWNE = lumaNW + lumaNE;
    float subpixSWSE = lumaSW + lumaSE;

    float subpixRcpRange = 1.0 / range;
    float subpixA = subpixNSWE * 2.0 + subpixNWNE + subpixSWSE;
    float subpixB = (subpixA * (1.0 / 12.0)) - lumaM;
    float subpixC = saturate(abs(subpixB) * subpixRcpRange);
    float subpixD = ((-2.0 * subpixC) + 3.0) * (subpixC * subpixC);
    float subpixE = subpixD * subpixD * 0.55;

    float edgeHorz = abs(-2.0 * lumaW + subpixNWSW) + abs(-2.0 * lumaM + lumaNS) * 2.0 + abs(-2.0 * lumaE + subpixNESE);
    float edgeVert = abs(-2.0 * lumaN + subpixNWNE) + abs(-2.0 * lumaM + lumaWE) * 2.0 + abs(-2.0 * lumaS + subpixSWSE);
    bool horzSpan = edgeHorz >= edgeVert;

    float luma1 = horzSpan ? lumaN : lumaW;
    float luma2 = horzSpan ? lumaS : lumaE;
    float gradient1 = luma1 - lumaM;
    float gradient2 = luma2 - lumaM;
    bool pair1Steepest = abs(gradient1) >= abs(gradient2);
    float gradientScaled = 0.25 * max(abs(gradient1), abs(gradient2));

    float lengthSign = horzSpan ? -texel.y : -texel.x;
    if (!pair1Steepest)
    {
        lengthSign = -lengthSign;
    }

    float lumaLocalAvg = pair1Steepest ? 0.5 * (luma1 + lumaM) : 0.5 * (luma2 + lumaM);
    float2 posB = input.UV;
    if (horzSpan)
    {
        posB.y += lengthSign * 0.5;
    }
    else
    {
        posB.x += lengthSign * 0.5;
    }

    float2 offNP = horzSpan ? float2(texel.x, 0.0) : float2(0.0, texel.y);
    static const float StepSizes[6] = { 1.0, 2.0, 3.5, 5.5, 8.0, 12.0 };
    float2 posN = posB;
    float2 posP = posB;
    float lumaEndN = lumaLocalAvg;
    float lumaEndP = lumaLocalAvg;
    bool doneN = false;
    bool doneP = false;

    [unroll]
    for (int i = 0; i < 6; i++)
    {
        float2 stepVec = offNP * StepSizes[i];
        if (!doneN)
        {
            posN -= stepVec;
            lumaEndN = output.PrimaryTexture.SampleLevel(output.LinearSampler, posN, 0).a - lumaLocalAvg;
            doneN = abs(lumaEndN) >= gradientScaled;
        }

        if (!doneP)
        {
            posP += stepVec;
            lumaEndP = output.PrimaryTexture.SampleLevel(output.LinearSampler, posP, 0).a - lumaLocalAvg;
            doneP = abs(lumaEndP) >= gradientScaled;
        }
    }

    float dstN = horzSpan ? (input.UV.x - posN.x) : (input.UV.y - posN.y);
    float dstP = horzSpan ? (posP.x - input.UV.x) : (posP.y - input.UV.y);
    bool directionN = dstN < dstP;
    float dst = min(dstN, dstP);
    float spanLength = max(dstN + dstP, 1e-5);
    float lumaEndMin = directionN ? lumaEndN : lumaEndP;
    bool goodSpan = (lumaEndMin < 0.0) != ((lumaM - lumaLocalAvg) < 0.0);
    float pixelOffset = goodSpan ? (0.5 - dst / spanLength) : 0.0;
    float finalOffset = max(pixelOffset, subpixE);

    float2 finalUv = input.UV;
    if (horzSpan)
    {
        finalUv.y += finalOffset * lengthSign;
    }
    else
    {
        finalUv.x += finalOffset * lengthSign;
    }

    float3 result = output.PrimaryTexture.SampleLevel(output.LinearSampler, finalUv, 0).rgb;

    return float4(ApplyTriangularDither(result, pixel, output.Width, output.FrameIndex), 1.0);
}
