using System.Numerics;
using System.Runtime.InteropServices;

namespace Zenith.NET.Extensions.Upscaling.Passes;

internal unsafe class Sgsr2UpscalePass : DisposableObject
{
    private readonly Buffer buffer;
    private readonly Sampler pointSampler;
    private readonly Sampler linearSampler;
    private readonly ComputePipeline pipeline;

    public Sgsr2UpscalePass(GraphicsContext context, TemporalUpscalerMode mode)
    {
        string source = mode is TemporalUpscalerMode.Quality ? QualitySource : SpeedSource;

        using Shader shader = context.CreateShader(ZenithCompiler.CompileFromSource(context.GraphicsApi, source, "Main"));

        buffer = context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(Constants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
        pointSampler = context.CreateSampler(SamplerDesc.PointClamp());
        linearSampler = context.CreateSampler(SamplerDesc.LinearClamp());
        pipeline = context.CreateComputePipeline(new() { ComputeShader = shader });
    }

    public void Record(CommandBuffer commandBuffer, TemporalUpscalerDesc desc, TemporalUpscalerArgs args, ResourceHandle history, ResourceHandle motion, ResourceHandle yCoCg, ResourceHandle sceneOutput, ResourceHandle historyOutput)
    {
        Constants constants = new(desc, args, history, motion, yCoCg, sceneOutput, historyOutput, linearSampler.Handle, pointSampler.Handle);

        buffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(Constants)
        });

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(buffer, 0);
        commandBuffer.Dispatch((constants.DisplayWidth + 7) / 8, (constants.DisplayHeight + 7) / 8, 1);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.All);
    }

    protected override void Destroy()
    {
        pipeline.Dispose();
        linearSampler.Dispose();
        pointSampler.Dispose();
        buffer.Dispose();
    }

    private const string SpeedSource = """
//============================================================================================================
//
//
//                  Copyright (c) 2024, Qualcomm Innovation Center, Inc. All rights reserved.
//                              SPDX-License-Identifier: BSD-3-Clause
//
//============================================================================================================

struct Constants
{
    uint4 RenderDisplaySize;

    float4 ViewportDisplayRcp;

    float4 JitterPadding;

    float4x4 ClipToPrevClip;

    float4 ExposureFovNearMinLerp;

    uint4 SameCameraReset;

    DescriptorHandle<Texture2D<float4>> History;

    DescriptorHandle<Texture2D<float4>> Motion;

    DescriptorHandle<Texture2D<uint>> YCoCg;

    DescriptorHandle<RWTexture2D<float4>> SceneOutput;

    DescriptorHandle<RWTexture2D<float4>> HistoryOutput;

    DescriptorHandle<SamplerState> LinearSampler;

    DescriptorHandle<SamplerState> PointSampler;

    uint2 Pad;
};

ConstantBuffer<Constants> constants;

float FastLanczos(float base)
{
    float y = base - 1.0;
    float y2 = y * y;
    float y_temp = 0.75 * y + y2;
    return y_temp * y2;
}

float3 DecodeColor(uint sample32)
{
    uint x11 = sample32 >> 21;
    uint y11 = sample32 & (2047 << 10);
    uint z10 = sample32 & 1023;
    float3 samplecolor;
    samplecolor.x = (float(x11) * (1.0 / 2047.5));
    samplecolor.y = (float(y11) * (4.76953602e-7)) - 0.5;
    samplecolor.z = (float(z10) * (1.0 / 1023.5)) - 0.5;

    return samplecolor;
}

[shader("compute")]
[numthreads(8, 8, 1)]
void Main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    float Biasmax_viewportXScale = min(float(constants.RenderDisplaySize.z) / float(constants.RenderDisplaySize.x), 1.99);
    float scalefactor = min(20.0, pow((float(constants.RenderDisplaySize.z) / float(constants.RenderDisplaySize.x)) * (float(constants.RenderDisplaySize.w) / float(constants.RenderDisplaySize.y)), 3.0));
    float2 HistoryInfoViewportSizeInverse = constants.ViewportDisplayRcp.zw;
    float2 HistoryInfoViewportSize = float2(constants.RenderDisplaySize.zw);
    float2 InputJitter = constants.JitterPadding.xy;
    float2 InputInfoViewportSize = float2(constants.RenderDisplaySize.xy);
    float2 Hruv = (float2(dispatchThreadId.xy) + float2(0.5, 0.5)) * HistoryInfoViewportSizeInverse;
    float2 Jitteruv;
    Jitteruv.x = clamp(Hruv.x + (InputJitter.x * constants.ViewportDisplayRcp.x), 0.0, 1.0);
    Jitteruv.y = clamp(Hruv.y + (InputJitter.y * constants.ViewportDisplayRcp.y), 0.0, 1.0);

    int2 InputPos = int2(Jitteruv * InputInfoViewportSize);
    float4 mda = constants.Motion.SampleLevel(constants.LinearSampler, Jitteruv, 0.0).xyzw;
    float2 Motion = mda.xy;

    float2 PrevUV;
    PrevUV.x = clamp(-0.5 * Motion.x + Hruv.x, 0.0, 1.0);
    PrevUV.y = clamp(0.5 * Motion.y + Hruv.y, 0.0, 1.0);

    float depthfactor = mda.z;
    float ColorMax = mda.w;

    float4 History = constants.History.SampleLevel(constants.LinearSampler, PrevUV, 0.0);
    float3 HistoryColor = History.xyz;
    float Historyw = History.w;
    float Wfactor = clamp(abs(Historyw), 0.0, 1.0);

    float4 Upsampledcw = float4(0.0, 0.0, 0.0, 0.0);
    float kernelfactor = clamp(Wfactor + float(constants.SameCameraReset.y), 0.0, 1.0);
    float biasmax = Biasmax_viewportXScale - Biasmax_viewportXScale * kernelfactor;
    float biasmin = max(1.0, 0.3 + 0.3 * biasmax);
    float biasfactor = max(0.25 * depthfactor, kernelfactor);
    float kernelbias = lerp(biasmax, biasmin, biasfactor);
    float motion_viewport_len = length(Motion * HistoryInfoViewportSize);
    float curvebias = lerp(-2.0, -3.0, clamp(motion_viewport_len * 0.02, 0.0, 1.0));

    float3 rectboxcenter = float3(0.0, 0.0, 0.0);
    float3 rectboxvar = float3(0.0, 0.0, 0.0);
    float rectboxweight = 0.0;
    float2 srcpos = float2(InputPos) + float2(0.5, 0.5) - InputJitter;
    float2 srcOutputPos = Hruv * InputInfoViewportSize;

    kernelbias *= 0.5;
    float kernelbias2 = kernelbias * kernelbias;
    float2 srcpos_srcOutputPos = srcpos - srcOutputPos;

    int2 InputPosBtmRight = int2(1, 1) + InputPos;
    float2 gatherCoord = float2(InputPos) * constants.ViewportDisplayRcp.xy;
    uint btmRight = constants.YCoCg.Load(int3(InputPosBtmRight, 0));
    uint4 topleft = constants.YCoCg.Gather(constants.PointSampler, gatherCoord);
    uint2 topRight;
    uint2 bottomLeft;

    uint sameCameraFrmNum = constants.SameCameraReset.x;

    if (sameCameraFrmNum != 0)
    {
        topRight = constants.YCoCg.Gather(constants.PointSampler, gatherCoord + float2(constants.ViewportDisplayRcp.x, 0.0)).yz;
        bottomLeft = constants.YCoCg.Gather(constants.PointSampler, gatherCoord + float2(0.0, constants.ViewportDisplayRcp.y)).xy;
    }
    else
    {
        uint2 btmRightGather = constants.YCoCg.Gather(constants.PointSampler, gatherCoord + float2(constants.ViewportDisplayRcp.x, constants.ViewportDisplayRcp.y)).xz;
        bottomLeft.y = btmRightGather.x;
        topRight.x = btmRightGather.y;
    }

    float3 rectboxmin;
    float3 rectboxmax;
    {
        float3 samplecolor = DecodeColor(bottomLeft.y);
        float2 baseoffset = srcpos_srcOutputPos + float2(0.0, 1.0);
        float baseoffset_dot = dot(baseoffset, baseoffset);
        float base = clamp(baseoffset_dot * kernelbias2, 0.0, 1.0);
        float weight = FastLanczos(base);
        Upsampledcw += float4(samplecolor * weight, weight);
        float boxweight = exp(baseoffset_dot * curvebias);
        rectboxmin = samplecolor;
        rectboxmax = samplecolor;
        float3 wsample = samplecolor * boxweight;
        rectboxcenter += wsample;
        rectboxvar += (samplecolor * wsample);
        rectboxweight += boxweight;
    }
    {
        float3 samplecolor = DecodeColor(topRight.x);
        float2 baseoffset = srcpos_srcOutputPos + float2(1.0, 0.0);
        float baseoffset_dot = dot(baseoffset, baseoffset);
        float base = clamp(baseoffset_dot * kernelbias2, 0.0, 1.0);
        float weight = FastLanczos(base);
        Upsampledcw += float4(samplecolor * weight, weight);
        float boxweight = exp(baseoffset_dot * curvebias);
        rectboxmin = min(rectboxmin, samplecolor);
        rectboxmax = max(rectboxmax, samplecolor);
        float3 wsample = samplecolor * boxweight;
        rectboxcenter += wsample;
        rectboxvar += (samplecolor * wsample);
        rectboxweight += boxweight;
    }
    {
        float3 samplecolor = DecodeColor(topleft.x);
        float2 baseoffset = srcpos_srcOutputPos + float2(-1.0, 0.0);
        float baseoffset_dot = dot(baseoffset, baseoffset);
        float base = clamp(baseoffset_dot * kernelbias2, 0.0, 1.0);
        float weight = FastLanczos(base);
        Upsampledcw += float4(samplecolor * weight, weight);
        float boxweight = exp(baseoffset_dot * curvebias);
        rectboxmin = min(rectboxmin, samplecolor);
        rectboxmax = max(rectboxmax, samplecolor);
        float3 wsample = samplecolor * boxweight;
        rectboxcenter += wsample;
        rectboxvar += (samplecolor * wsample);
        rectboxweight += boxweight;
    }
    {
        float3 samplecolor = DecodeColor(topleft.y);
        float2 baseoffset = srcpos_srcOutputPos;
        float baseoffset_dot = dot(baseoffset, baseoffset);
        float base = clamp(baseoffset_dot * kernelbias2, 0.0, 1.0);
        float weight = FastLanczos(base);
        Upsampledcw += float4(samplecolor * weight, weight);
        float boxweight = exp(baseoffset_dot * curvebias);
        rectboxmin = min(rectboxmin, samplecolor);
        rectboxmax = max(rectboxmax, samplecolor);
        float3 wsample = samplecolor * boxweight;
        rectboxcenter += wsample;
        rectboxvar += (samplecolor * wsample);
        rectboxweight += boxweight;
    }
    {
        float3 samplecolor = DecodeColor(topleft.z);
        float2 baseoffset = srcpos_srcOutputPos + float2(0.0, -1.0);
        float baseoffset_dot = dot(baseoffset, baseoffset);
        float base = clamp(baseoffset_dot * kernelbias2, 0.0, 1.0);
        float weight = FastLanczos(base);
        Upsampledcw += float4(samplecolor * weight, weight);
        float boxweight = exp(baseoffset_dot * curvebias);
        rectboxmin = min(rectboxmin, samplecolor);
        rectboxmax = max(rectboxmax, samplecolor);
        float3 wsample = samplecolor * boxweight;
        rectboxcenter += wsample;
        rectboxvar += (samplecolor * wsample);
        rectboxweight += boxweight;
    }

    if (sameCameraFrmNum != 0)
    {
        {
            float3 samplecolor = DecodeColor(btmRight);
            float2 baseoffset = srcpos_srcOutputPos + float2(1.0, 1.0);
            float baseoffset_dot = dot(baseoffset, baseoffset);
            float base = clamp(baseoffset_dot * kernelbias2, 0.0, 1.0);
            float weight = FastLanczos(base);
            Upsampledcw += float4(samplecolor * weight, weight);
            float boxweight = exp(baseoffset_dot * curvebias);
            rectboxmin = min(rectboxmin, samplecolor);
            rectboxmax = max(rectboxmax, samplecolor);
            float3 wsample = samplecolor * boxweight;
            rectboxcenter += wsample;
            rectboxvar += (samplecolor * wsample);
            rectboxweight += boxweight;
        }
        {
            float3 samplecolor = DecodeColor(bottomLeft.x);
            float2 baseoffset = srcpos_srcOutputPos + float2(-1.0, 1.0);
            float baseoffset_dot = dot(baseoffset, baseoffset);
            float base = clamp(baseoffset_dot * kernelbias2, 0.0, 1.0);
            float weight = FastLanczos(base);
            Upsampledcw += float4(samplecolor * weight, weight);
            float boxweight = exp(baseoffset_dot * curvebias);
            rectboxmin = min(rectboxmin, samplecolor);
            rectboxmax = max(rectboxmax, samplecolor);
            float3 wsample = samplecolor * boxweight;
            rectboxcenter += wsample;
            rectboxvar += (samplecolor * wsample);
            rectboxweight += boxweight;
        }
        {
            float3 samplecolor = DecodeColor(topRight.y);
            float2 baseoffset = srcpos_srcOutputPos + float2(1.0, -1.0);
            float baseoffset_dot = dot(baseoffset, baseoffset);
            float base = clamp(baseoffset_dot * kernelbias2, 0.0, 1.0);
            float weight = FastLanczos(base);
            Upsampledcw += float4(samplecolor * weight, weight);
            float boxweight = exp(baseoffset_dot * curvebias);
            rectboxmin = min(rectboxmin, samplecolor);
            rectboxmax = max(rectboxmax, samplecolor);
            float3 wsample = samplecolor * boxweight;
            rectboxcenter += wsample;
            rectboxvar += (samplecolor * wsample);
            rectboxweight += boxweight;
        }
        {
            float3 samplecolor = DecodeColor(topleft.w);
            float2 baseoffset = srcpos_srcOutputPos + float2(-1.0, -1.0);
            float baseoffset_dot = dot(baseoffset, baseoffset);
            float base = clamp(baseoffset_dot * kernelbias2, 0.0, 1.0);
            float weight = FastLanczos(base);
            Upsampledcw += float4(samplecolor * weight, weight);
            float boxweight = exp(baseoffset_dot * curvebias);
            rectboxmin = min(rectboxmin, samplecolor);
            rectboxmax = max(rectboxmax, samplecolor);
            float3 wsample = samplecolor * boxweight;
            rectboxcenter += wsample;
            rectboxvar += (samplecolor * wsample);
            rectboxweight += boxweight;
        }
    }

    rectboxweight = 1.0 / rectboxweight;
    rectboxcenter *= rectboxweight;
    rectboxvar *= rectboxweight;
    rectboxvar = sqrt(abs(rectboxvar - rectboxcenter * rectboxcenter));

    Upsampledcw.xyz = clamp(Upsampledcw.xyz / Upsampledcw.w, rectboxmin - float3(0.05, 0.05, 0.05), rectboxmax + float3(0.05, 0.05, 0.05));
    Upsampledcw.w = Upsampledcw.w * (1.0 / 3.0);

    float OneMinusWfactor = 1.0 - Wfactor;

    float baseupdate = OneMinusWfactor - OneMinusWfactor * depthfactor;
    baseupdate = min(baseupdate, lerp(baseupdate, Upsampledcw.w * 10.0, clamp(10.0 * motion_viewport_len, 0.0, 1.0)));
    baseupdate = min(baseupdate, lerp(baseupdate, Upsampledcw.w, clamp(motion_viewport_len * 0.05, 0.0, 1.0)));
    float basealpha = baseupdate;

    const float EPSILON = 1.192e-07;
    float boxscale = max(depthfactor, clamp(motion_viewport_len * 0.05, 0.0, 1.0));
    float boxsize = lerp(scalefactor, 1.0, boxscale);
    float3 sboxvar = rectboxvar * boxsize;
    float3 boxmin = rectboxcenter - sboxvar;
    float3 boxmax = rectboxcenter + sboxvar;
    rectboxmax = min(rectboxmax, boxmax);
    rectboxmin = max(rectboxmin, boxmin);

    float3 clampedcolor = clamp(HistoryColor, rectboxmin, rectboxmax);
    float startLerpValue = constants.ExposureFovNearMinLerp.w;
    if ((abs(mda.x) + abs(mda.y)) > 0.000001)
    {
        startLerpValue = 0.0;
    }
    float lerpcontribution = (any(rectboxmin > HistoryColor) || any(HistoryColor > rectboxmax)) ? startLerpValue : 1.0;

    HistoryColor = lerp(clampedcolor, HistoryColor, clamp(lerpcontribution, 0.0, 1.0));
    float basemin = min(basealpha, 0.1);
    basealpha = lerp(basemin, basealpha, clamp(lerpcontribution, 0.0, 1.0));

    float alphasum = max(EPSILON, basealpha + Upsampledcw.w);
    float alpha = clamp(Upsampledcw.w / alphasum + float(constants.SameCameraReset.y), 0.0, 1.0);
    Upsampledcw.xyz = lerp(HistoryColor, Upsampledcw.xyz, alpha);

    constants.HistoryOutput[int2(dispatchThreadId.xy)] = float4(Upsampledcw.xyz, Wfactor);

    float x_z = Upsampledcw.x - Upsampledcw.z;
    Upsampledcw.xyz = float3(
        clamp(x_z + Upsampledcw.y, 0.0, 1.0),
        clamp(Upsampledcw.x + Upsampledcw.z, 0.0, 1.0),
        clamp(x_z - Upsampledcw.y, 0.0, 1.0));

    float compMax = max(Upsampledcw.x, Upsampledcw.y);
    compMax = clamp(max(compMax, Upsampledcw.z), 0.0, 1.0);
    float scale = constants.ExposureFovNearMinLerp.x / ((1.0 + 600.0 / 65504.0) - compMax);

    if (ColorMax > 4000.0)
    {
        scale = ColorMax;
    }
    Upsampledcw.xyz = Upsampledcw.xyz * scale;
    constants.SceneOutput[int2(dispatchThreadId.xy)] = Upsampledcw;
}
""";

    private const string QualitySource = """
//============================================================================================================
//
//
//                  Copyright (c) 2024, Qualcomm Innovation Center, Inc. All rights reserved.
//                              SPDX-License-Identifier: BSD-3-Clause
//
//============================================================================================================

struct Constants
{
    uint4 RenderDisplaySize;

    float4 ViewportDisplayRcp;

    float4 JitterPadding;

    float4x4 ClipToPrevClip;

    float4 ExposureFovNearMinLerp;

    uint4 SameCameraReset;

    DescriptorHandle<Texture2D<float4>> History;

    DescriptorHandle<Texture2D<float4>> Motion;

    DescriptorHandle<Texture2D<uint>> YCoCg;

    DescriptorHandle<RWTexture2D<float4>> SceneOutput;

    DescriptorHandle<RWTexture2D<float4>> HistoryOutput;

    DescriptorHandle<SamplerState> LinearSampler;

    DescriptorHandle<SamplerState> PointSampler;

    uint2 Pad;
};

ConstantBuffer<Constants> constants;

float FastLanczos(float base)
{
    float y = base - 1.0;
    float y2 = y * y;
    float y_temp = 0.75 * y + y2;
    return y_temp * y2;
}

float3 DecodeColor(uint sample32)
{
    uint x11 = sample32 >> 21;
    uint y11 = sample32 & (2047 << 10);
    uint z10 = sample32 & 1023;
    float3 samplecolor;
    samplecolor.x = (float(x11) * (1.0 / 2047.5));
    samplecolor.y = (float(y11) * (4.76953602e-7)) - 0.5;
    samplecolor.z = (float(z10) * (1.0 / 1023.5)) - 0.5;

    return samplecolor;
}

[shader("compute")]
[numthreads(8, 8, 1)]
void Main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    float Biasmax_viewportXScale = min(float(constants.RenderDisplaySize.z) / float(constants.RenderDisplaySize.x), 1.99);
    float scalefactor = min(20.0, pow((float(constants.RenderDisplaySize.z) / float(constants.RenderDisplaySize.x)) * (float(constants.RenderDisplaySize.w) / float(constants.RenderDisplaySize.y)), 3.0));
    float2 HistoryInfoViewportSizeInverse = constants.ViewportDisplayRcp.zw;
    float2 HistoryInfoViewportSize = float2(constants.RenderDisplaySize.zw);
    float2 InputJitter = constants.JitterPadding.xy;
    float2 InputInfoViewportSize = float2(constants.RenderDisplaySize.xy);
    float2 Hruv = (float2(dispatchThreadId.xy) + float2(0.5, 0.5)) * HistoryInfoViewportSizeInverse;
    float2 Jitteruv;
    Jitteruv.x = clamp(Hruv.x + (InputJitter.x * constants.ViewportDisplayRcp.x), 0.0, 1.0);
    Jitteruv.y = clamp(Hruv.y + (InputJitter.y * constants.ViewportDisplayRcp.y), 0.0, 1.0);

    int2 InputPos = int2(Jitteruv * InputInfoViewportSize);

    float alphab = constants.Motion.Load(int3(InputPos, 0)).w;
    float3 mda = constants.Motion.SampleLevel(constants.LinearSampler, Jitteruv, 0.0).xyz;
    float2 Motion = mda.xy;

    float2 PrevUV;
    PrevUV.x = clamp(-0.5 * Motion.x + Hruv.x, 0.0, 1.0);
    PrevUV.y = clamp(0.5 * Motion.y + Hruv.y, 0.0, 1.0);

    float depthfactor = mda.z;
    float history_value = frac(alphab);
    float alphamask = (alphab - history_value) * 0.001;
    history_value *= 2.0;

    float4 History = constants.History.SampleLevel(constants.LinearSampler, PrevUV, 0.0);
    float3 HistoryColor = History.xyz;
    float Historyw = History.w;
    float Wfactor = max(clamp(abs(Historyw), 0.0, 1.0), alphamask);

    float4 Upsampledcw = float4(0.0, 0.0, 0.0, 0.0);
    float kernelfactor = clamp(Wfactor + float(constants.SameCameraReset.y), 0.0, 1.0);
    float biasmax = Biasmax_viewportXScale - Biasmax_viewportXScale * kernelfactor;
    float biasmin = max(1.0, 0.3 + 0.3 * biasmax);
    float biasfactor = max(0.25 * depthfactor, kernelfactor);
    float kernelbias = lerp(biasmax, biasmin, biasfactor);
    float motion_viewport_len = length(Motion * HistoryInfoViewportSize);
    float curvebias = lerp(-2.0, -3.0, clamp(motion_viewport_len * 0.02, 0.0, 1.0));

    float3 rectboxcenter = float3(0.0, 0.0, 0.0);
    float3 rectboxvar = float3(0.0, 0.0, 0.0);
    float rectboxweight = 0.0;
    float2 srcpos = float2(InputPos) + float2(0.5, 0.5) - InputJitter;
    float2 srcOutputPos = Hruv * InputInfoViewportSize;

    kernelbias *= 0.5;
    float kernelbias2 = kernelbias * kernelbias;
    float2 srcpos_srcOutputPos = srcpos - srcOutputPos;

    int2 InputPosBtmRight = int2(1, 1) + InputPos;
    float2 gatherCoord = float2(InputPos) * constants.ViewportDisplayRcp.xy;
    uint btmRight = constants.YCoCg.Load(int3(InputPosBtmRight, 0));
    uint4 topleft = constants.YCoCg.Gather(constants.PointSampler, gatherCoord);
    uint2 topRight = constants.YCoCg.Gather(constants.PointSampler, gatherCoord + float2(constants.ViewportDisplayRcp.x, 0.0)).yz;
    uint2 bottomLeft = constants.YCoCg.Gather(constants.PointSampler, gatherCoord + float2(0.0, constants.ViewportDisplayRcp.y)).xy;

    float3 rectboxmin;
    float3 rectboxmax;
    {
        rectboxmin = DecodeColor(btmRight);
        float2 baseoffset = srcpos_srcOutputPos + float2(1.0, 1.0);
        float baseoffset_dot = dot(baseoffset, baseoffset);
        float base = clamp(baseoffset_dot * kernelbias2, 0.0, 1.0);
        float weight = FastLanczos(base);
        Upsampledcw += float4(rectboxmin * weight, weight);
        float boxweight = exp(baseoffset_dot * curvebias);
        rectboxmax = rectboxmin;
        float3 wsample = rectboxmin * boxweight;
        rectboxcenter = wsample;
        rectboxvar = rectboxmin * wsample;
        rectboxweight = boxweight;
    }
    {
        float3 samplecolor = DecodeColor(bottomLeft.x);
        float2 baseoffset = srcpos_srcOutputPos + float2(-1.0, 1.0);
        float baseoffset_dot = dot(baseoffset, baseoffset);
        float base = clamp(baseoffset_dot * kernelbias2, 0.0, 1.0);
        float weight = FastLanczos(base);
        Upsampledcw += float4(samplecolor * weight, weight);
        float boxweight = exp(baseoffset_dot * curvebias);
        rectboxmin = min(rectboxmin, samplecolor);
        rectboxmax = max(rectboxmax, samplecolor);
        float3 wsample = samplecolor * boxweight;
        rectboxcenter += wsample;
        rectboxvar += (samplecolor * wsample);
        rectboxweight += boxweight;
    }
    {
        float3 samplecolor = DecodeColor(bottomLeft.y);
        float2 baseoffset = srcpos_srcOutputPos + float2(0.0, 1.0);
        float baseoffset_dot = dot(baseoffset, baseoffset);
        float base = clamp(baseoffset_dot * kernelbias2, 0.0, 1.0);
        float weight = FastLanczos(base);
        Upsampledcw += float4(samplecolor * weight, weight);
        float boxweight = exp(baseoffset_dot * curvebias);
        rectboxmin = min(rectboxmin, samplecolor);
        rectboxmax = max(rectboxmax, samplecolor);
        float3 wsample = samplecolor * boxweight;
        rectboxcenter += wsample;
        rectboxvar += (samplecolor * wsample);
        rectboxweight += boxweight;
    }
    {
        float3 samplecolor = DecodeColor(topRight.x);
        float2 baseoffset = srcpos_srcOutputPos + float2(1.0, 0.0);
        float baseoffset_dot = dot(baseoffset, baseoffset);
        float base = clamp(baseoffset_dot * kernelbias2, 0.0, 1.0);
        float weight = FastLanczos(base);
        Upsampledcw += float4(samplecolor * weight, weight);
        float boxweight = exp(baseoffset_dot * curvebias);
        rectboxmin = min(rectboxmin, samplecolor);
        rectboxmax = max(rectboxmax, samplecolor);
        float3 wsample = samplecolor * boxweight;
        rectboxcenter += wsample;
        rectboxvar += (samplecolor * wsample);
        rectboxweight += boxweight;
    }
    {
        float3 samplecolor = DecodeColor(topRight.y);
        float2 baseoffset = srcpos_srcOutputPos + float2(1.0, -1.0);
        float baseoffset_dot = dot(baseoffset, baseoffset);
        float base = clamp(baseoffset_dot * kernelbias2, 0.0, 1.0);
        float weight = FastLanczos(base);
        Upsampledcw += float4(samplecolor * weight, weight);
        float boxweight = exp(baseoffset_dot * curvebias);
        rectboxmin = min(rectboxmin, samplecolor);
        rectboxmax = max(rectboxmax, samplecolor);
        float3 wsample = samplecolor * boxweight;
        rectboxcenter += wsample;
        rectboxvar += (samplecolor * wsample);
        rectboxweight += boxweight;
    }
    {
        float3 samplecolor = DecodeColor(topleft.x);
        float2 baseoffset = srcpos_srcOutputPos + float2(-1.0, 0.0);
        float baseoffset_dot = dot(baseoffset, baseoffset);
        float base = clamp(baseoffset_dot * kernelbias2, 0.0, 1.0);
        float weight = FastLanczos(base);
        Upsampledcw += float4(samplecolor * weight, weight);
        float boxweight = exp(baseoffset_dot * curvebias);
        rectboxmin = min(rectboxmin, samplecolor);
        rectboxmax = max(rectboxmax, samplecolor);
        float3 wsample = samplecolor * boxweight;
        rectboxcenter += wsample;
        rectboxvar += (samplecolor * wsample);
        rectboxweight += boxweight;
    }
    {
        float3 samplecolor = DecodeColor(topleft.y);
        float2 baseoffset = srcpos_srcOutputPos;
        float baseoffset_dot = dot(baseoffset, baseoffset);
        float base = clamp(baseoffset_dot * kernelbias2, 0.0, 1.0);
        float weight = FastLanczos(base);
        Upsampledcw += float4(samplecolor * weight, weight);
        float boxweight = exp(baseoffset_dot * curvebias);
        rectboxmin = min(rectboxmin, samplecolor);
        rectboxmax = max(rectboxmax, samplecolor);
        float3 wsample = samplecolor * boxweight;
        rectboxcenter += wsample;
        rectboxvar += (samplecolor * wsample);
        rectboxweight += boxweight;
    }
    {
        float3 samplecolor = DecodeColor(topleft.z);
        float2 baseoffset = srcpos_srcOutputPos + float2(0.0, -1.0);
        float baseoffset_dot = dot(baseoffset, baseoffset);
        float base = clamp(baseoffset_dot * kernelbias2, 0.0, 1.0);
        float weight = FastLanczos(base);
        Upsampledcw += float4(samplecolor * weight, weight);
        float boxweight = exp(baseoffset_dot * curvebias);
        rectboxmin = min(rectboxmin, samplecolor);
        rectboxmax = max(rectboxmax, samplecolor);
        float3 wsample = samplecolor * boxweight;
        rectboxcenter += wsample;
        rectboxvar += (samplecolor * wsample);
        rectboxweight += boxweight;
    }
    {
        float3 samplecolor = DecodeColor(topleft.w);
        float2 baseoffset = srcpos_srcOutputPos + float2(-1.0, -1.0);
        float baseoffset_dot = dot(baseoffset, baseoffset);
        float base = clamp(baseoffset_dot * kernelbias2, 0.0, 1.0);
        float weight = FastLanczos(base);
        Upsampledcw += float4(samplecolor * weight, weight);
        float boxweight = exp(baseoffset_dot * curvebias);
        rectboxmin = min(rectboxmin, samplecolor);
        rectboxmax = max(rectboxmax, samplecolor);
        float3 wsample = samplecolor * boxweight;
        rectboxcenter += wsample;
        rectboxvar += (samplecolor * wsample);
        rectboxweight += boxweight;
    }

    rectboxweight = 1.0 / rectboxweight;
    rectboxcenter *= rectboxweight;
    rectboxvar *= rectboxweight;
    rectboxvar = sqrt(abs(rectboxvar - rectboxcenter * rectboxcenter));

    Upsampledcw.xyz = clamp(Upsampledcw.xyz / Upsampledcw.w, rectboxmin - float3(0.05, 0.05, 0.05), rectboxmax + float3(0.05, 0.05, 0.05));
    Upsampledcw.w = Upsampledcw.w * (1.0 / 3.0);

    float tcontribute = history_value * clamp(rectboxvar.x * 10.0, 0.0, 1.0);
    float OneMinusWfactor = 1.0 - Wfactor;
    tcontribute = tcontribute * OneMinusWfactor;

    float baseupdate = OneMinusWfactor - OneMinusWfactor * depthfactor;
    baseupdate = min(baseupdate, lerp(baseupdate, Upsampledcw.w * 10.0, clamp(10.0 * motion_viewport_len, 0.0, 1.0)));
    baseupdate = min(baseupdate, lerp(baseupdate, Upsampledcw.w, clamp(motion_viewport_len * 0.05, 0.0, 1.0)));
    float basealpha = baseupdate;

    const float EPSILON = 1.192e-07;
    float boxscale = max(depthfactor, clamp(motion_viewport_len * 0.05, 0.0, 1.0));
    float boxsize = lerp(scalefactor, 1.0, boxscale);
    float3 sboxvar = rectboxvar * boxsize;
    float3 boxmin = rectboxcenter - sboxvar;
    float3 boxmax = rectboxcenter + sboxvar;
    rectboxmax = min(rectboxmax, boxmax);
    rectboxmin = max(rectboxmin, boxmin);

    float3 clampedcolor = clamp(HistoryColor, rectboxmin, rectboxmax);
    float lerpcontribution = (any(rectboxmin > HistoryColor) || any(HistoryColor > rectboxmax)) ? tcontribute : 1.0;
    lerpcontribution = lerpcontribution - lerpcontribution * sqrt(alphamask);
    HistoryColor = lerp(clampedcolor, HistoryColor, clamp(lerpcontribution, 0.0, 1.0));
    float basemin = min(basealpha, 0.1);
    basealpha = lerp(basemin, basealpha, clamp(lerpcontribution, 0.0, 1.0));

    float alphasum = max(EPSILON, basealpha + Upsampledcw.w);
    float alpha = clamp(Upsampledcw.w / alphasum + float(constants.SameCameraReset.y), 0.0, 1.0);
    Upsampledcw.xyz = lerp(HistoryColor, Upsampledcw.xyz, alpha);

    constants.HistoryOutput[int2(dispatchThreadId.xy)] = float4(Upsampledcw.xyz, Wfactor);

    float x_z = Upsampledcw.x - Upsampledcw.z;
    Upsampledcw.xyz = float3(
        x_z + Upsampledcw.y,
        Upsampledcw.x + Upsampledcw.z,
        x_z - Upsampledcw.y);

    float compMax = max(Upsampledcw.x, Upsampledcw.y);
    compMax = max(compMax, Upsampledcw.z);
    float scale = constants.ExposureFovNearMinLerp.x / ((1.0 + 1.0 / 65504.0) - compMax);

    Upsampledcw.xyz = Upsampledcw.xyz * scale;
    constants.SceneOutput[int2(dispatchThreadId.xy)] = Upsampledcw;
}
""";
}

[StructLayout(LayoutKind.Explicit, Size = 208)]
file struct Constants(TemporalUpscalerDesc desc, TemporalUpscalerArgs args, ResourceHandle history, ResourceHandle motion, ResourceHandle yCoCg, ResourceHandle sceneOutput, ResourceHandle historyOutput, ResourceHandle linearSampler, ResourceHandle pointSampler)
{
    [FieldOffset(0)]
    public uint RenderWidth = desc.InputWidth;

    [FieldOffset(4)]
    public uint RenderHeight = desc.InputHeight;

    [FieldOffset(8)]
    public uint DisplayWidth = desc.OutputWidth;

    [FieldOffset(12)]
    public uint DisplayHeight = desc.OutputHeight;

    [FieldOffset(16)]
    public float RenderRcpX = 1.0f / desc.InputWidth;

    [FieldOffset(20)]
    public float RenderRcpY = 1.0f / desc.InputHeight;

    [FieldOffset(24)]
    public float DisplayRcpX = 1.0f / desc.OutputWidth;

    [FieldOffset(28)]
    public float DisplayRcpY = 1.0f / desc.OutputHeight;

    [FieldOffset(32)]
    public float JitterOffsetX = args.JitterOffsetX;

    [FieldOffset(36)]
    public float JitterOffsetY = args.JitterOffsetY;

    [FieldOffset(40)]
    public float PaddingX = 0.0f;

    [FieldOffset(44)]
    public float PaddingY = 0.0f;

    [FieldOffset(48)]
    public Matrix4x4 ClipToPrevClip = args.ClipToPrevClip;

    [FieldOffset(112)]
    public float PreExposure = args.PreExposure;

    [FieldOffset(116)]
    public float CameraFovAngleHor = args.CameraFovAngleHor;

    [FieldOffset(120)]
    public float CameraNear = 0.0f;

    [FieldOffset(124)]
    public float MinLerpContribution = args.MinLerpContribution;

    [FieldOffset(128)]
    public uint SameCamera = args.SameCamera ? 1u : 0u;

    [FieldOffset(132)]
    public uint Reset = args.Reset ? 1u : 0u;

    [FieldOffset(136)]
    public uint SameCameraResetZ = 0;

    [FieldOffset(140)]
    public uint SameCameraResetW = 0;

    [FieldOffset(144)]
    public ResourceHandle History = history;

    [FieldOffset(152)]
    public ResourceHandle Motion = motion;

    [FieldOffset(160)]
    public ResourceHandle YCoCg = yCoCg;

    [FieldOffset(168)]
    public ResourceHandle SceneOutput = sceneOutput;

    [FieldOffset(176)]
    public ResourceHandle HistoryOutput = historyOutput;

    [FieldOffset(184)]
    public ResourceHandle LinearSampler = linearSampler;

    [FieldOffset(192)]
    public ResourceHandle PointSampler = pointSampler;
}
