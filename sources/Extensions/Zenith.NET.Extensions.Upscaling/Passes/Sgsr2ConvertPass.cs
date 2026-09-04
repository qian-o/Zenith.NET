using System.Numerics;
using System.Runtime.InteropServices;

namespace Zenith.NET.Extensions.Upscaling.Passes;

internal unsafe class Sgsr2ConvertPass : DisposableObject
{
    private readonly Buffer buffer;
    private readonly Sampler sampler;
    private readonly ComputePipeline pipeline;

    public Sgsr2ConvertPass(GraphicsContext context, TemporalUpscalerMode mode)
    {
        string source = mode is TemporalUpscalerMode.Quality ? QualitySource : SpeedSource;

        using Shader shader = context.CreateShader(ZenithCompiler.CompileFromSource(context.GraphicsApi, source, "Main"));

        buffer = context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(Constants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
        sampler = context.CreateSampler(new()
        {
            MinFilter = FilterMode.Point,
            MagFilter = FilterMode.Point,
            MipFilter = FilterMode.Point,
            AddressU = AddressMode.Border,
            AddressV = AddressMode.Border,
            AddressW = AddressMode.Border,
            CompareOp = CompareOp.Never,
            MaxAnisotropy = 1,
            LodBias = 0.0f,
            MinLod = 0.0f,
            MaxLod = float.MaxValue,
            BorderColor = BorderColor.TransparentBlack
        });
        pipeline = context.CreateComputePipeline(new() { ComputeShader = shader });
    }

    public void Record(CommandBuffer commandBuffer,
                       TemporalUpscalerDesc desc,
                       TemporalUpscalerArgs args,
                       ResourceHandle yCoCg,
                       ResourceHandle motion)
    {
        Constants constants = new(desc, args, yCoCg, motion, sampler.Handle);

        buffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(Constants)
        });

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(buffer, 0);
        commandBuffer.Dispatch((constants.RenderWidth + 7) / 8, (constants.RenderHeight + 7) / 8, 1);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
    }

    protected override void Destroy()
    {
        pipeline.Dispose();
        sampler.Dispose();
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

    DescriptorHandle<Texture2D<float4>> Input;

    DescriptorHandle<Texture2D<float4>> OpaqueInput;

    DescriptorHandle<Texture2D<float>> Depth;

    DescriptorHandle<Texture2D<float4>> MotionVectors;

    DescriptorHandle<RWTexture2D<uint>> YCoCg;

    DescriptorHandle<RWTexture2D<float4>> Motion;

    DescriptorHandle<SamplerState> Sampler;

    uint2 Pad;
};

ConstantBuffer<Constants> constants;

float2 DecodeVelocityFromTexture(float2 ev)
{
    const float inv_div = 1.0 / (0.499 * 0.5);
    float2 dv;
    dv.xy = ev.xy * inv_div - 32767.0 / 65535.0 * inv_div;
    //dv.z = uintBitsToFloat((uint(round(ev.z * 65535.0f)) << 16) | uint(round(ev.w * 65535.0f)));
    return dv;
}

[shader("compute")]
[numthreads(8, 8, 1)]
void Main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    if (dispatchThreadId.x >= constants.RenderDisplaySize.x || dispatchThreadId.y >= constants.RenderDisplaySize.y)
    {
        return;
    }

    float Exposure_co_rcp = constants.ExposureFovNearMinLerp.x;
    float2 ViewportSizeInverse = constants.ViewportDisplayRcp.xy;
    uint2 InputPos = dispatchThreadId.xy;

    float2 gatherCoord = float2(dispatchThreadId.xy) * ViewportSizeInverse;
    float2 ViewportUV = gatherCoord + float2(0.5, 0.5) * ViewportSizeInverse;

    //derived from ffx_fsr2_reconstruct_dilated_velocity_and_previous_depth.h
    //FindNearestDepth

    float4 topleft = constants.Depth.Gather(constants.Sampler, gatherCoord);
    float2 v10 = float2(ViewportSizeInverse.x * 2.0, 0.0);
    float4 topRight = constants.Depth.Gather(constants.Sampler, (gatherCoord + v10));
    float2 v12 = float2(0.0, ViewportSizeInverse.y * 2.0);
    float4 bottomLeft = constants.Depth.Gather(constants.Sampler, (gatherCoord + v12));
    float2 v14 = float2(ViewportSizeInverse.x * 2.0, ViewportSizeInverse.y * 2.0);
    float4 bottomRight = constants.Depth.Gather(constants.Sampler, (gatherCoord + v14));
    float maxC = min(min(min(topleft.y, topRight.x), bottomLeft.z), bottomRight.w);
    float topleft4 = min(min(min(topleft.y, topleft.x), topleft.z), topleft.w);
    float topLeftMax9 = min(bottomLeft.w, min(min(maxC, topleft4), topRight.w));

    float depthclip = 0.0;
    if (maxC < 1.0 - 1.0e-05)
    {
        float topRight4 = min(min(min(topRight.y, topRight.x), topRight.z), topRight.w);
        float bottomLeft4 = min(min(min(bottomLeft.y, bottomLeft.x), bottomLeft.z), bottomLeft.w);
        float bottomRight4 = min(min(min(bottomRight.y, bottomRight.x), bottomRight.z), bottomRight.w);

        float Wdepth = 0.0;
        float Ksep = 1.37e-05;
        float Kfov = constants.ExposureFovNearMinLerp.y;
        float diagonal_length = length(float2(constants.RenderDisplaySize.xy));
        float Ksep_Kfov_diagonal = Ksep * Kfov * diagonal_length;

        float Depthsep = Ksep_Kfov_diagonal * (1.0 - maxC);
        float EPSILON = 1.19e-07;
        Wdepth += clamp((Depthsep / (abs(maxC - topleft4) + EPSILON)), 0.0, 1.0);
        Wdepth += clamp((Depthsep / (abs(maxC - topRight4) + EPSILON)), 0.0, 1.0);
        Wdepth += clamp((Depthsep / (abs(maxC - bottomLeft4) + EPSILON)), 0.0, 1.0);
        Wdepth += clamp((Depthsep / (abs(maxC - bottomRight4) + EPSILON)), 0.0, 1.0);
        depthclip = clamp(1.0 - Wdepth * 0.25, 0.0, 1.0);
    }

    //refer to ue/fsr2 PostProcessFFX_FSR2ConvertVelocity.usf, and using nearest depth for dilated motion

    float4 EncodedVelocity = constants.MotionVectors.Load(int3(int2(dispatchThreadId.xy), 0));

    float2 motion;
    if (EncodedVelocity.x > 0.0)
    {
        motion = DecodeVelocityFromTexture(EncodedVelocity.xy);
    }
    else
    {
        float2 ScreenPos = float2(2.0 * ViewportUV.x - 1.0, 1.0 - 2.0 * ViewportUV.y);
        float4 Position = float4(ScreenPos, topLeftMax9, 1.0);
        float4 PreClip = mul(Position, constants.ClipToPrevClip);
        float2 PreScreen = PreClip.xy / PreClip.w;
        motion = Position.xy - PreScreen;
    }

    ////////////compute luma
    float3 Colorrgb = constants.Input.Load(int3(int2(InputPos), 0)).xyz;

    ///simple tonemap
    float ColorMax = max(max(Colorrgb.x, Colorrgb.y), Colorrgb.z) + Exposure_co_rcp;
    Colorrgb /= float3(ColorMax, ColorMax, ColorMax);

    float3 Colorycocg;
    Colorycocg.x = 0.25 * (Colorrgb.x + 2.0 * Colorrgb.y + Colorrgb.z);
    Colorycocg.y = clamp(0.5 * Colorrgb.x + 0.5 - 0.5 * Colorrgb.z, 0.0, 1.0);
    Colorycocg.z = clamp(Colorycocg.x + Colorycocg.y - Colorrgb.x, 0.0, 1.0);

    //now color YCoCG all in the range of [0,1]
    uint x11 = uint(Colorycocg.x * 2047.5);
    uint y11 = uint(Colorycocg.y * 2047.5);
    uint z10 = uint(Colorycocg.z * 1023.5);

    constants.YCoCg[int2(dispatchThreadId.xy)] = ((x11 << 21) | (y11 << 10)) | z10;

    float4 v29 = float4(motion, depthclip, ColorMax);
    constants.Motion[int2(dispatchThreadId.xy)] = v29;
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

    DescriptorHandle<Texture2D<float4>> Input;

    DescriptorHandle<Texture2D<float4>> OpaqueInput;

    DescriptorHandle<Texture2D<float>> Depth;

    DescriptorHandle<Texture2D<float4>> MotionVectors;

    DescriptorHandle<RWTexture2D<uint>> YCoCg;

    DescriptorHandle<RWTexture2D<float4>> Motion;

    DescriptorHandle<SamplerState> Sampler;

    uint2 Pad;
};

ConstantBuffer<Constants> constants;

float2 DecodeVelocityFromTexture(float2 ev)
{
    const float inv_div = 1.0 / (0.499 * 0.5);
    float2 dv;
    dv.xy = ev.xy * inv_div - 32767.0 / 65535.0 * inv_div;
    //dv.z = uintBitsToFloat((uint(round(ev.z * 65535.0f)) << 16) | uint(round(ev.w * 65535.0f)));
    return dv;
}

[shader("compute")]
[numthreads(8, 8, 1)]
void Main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    if (dispatchThreadId.x >= constants.RenderDisplaySize.x || dispatchThreadId.y >= constants.RenderDisplaySize.y)
    {
        return;
    }

    float h0 = constants.ExposureFovNearMinLerp.x;
    float2 ViewportSizeInverse = constants.ViewportDisplayRcp.xy;
    uint2 InputPos = dispatchThreadId.xy;

    float2 gatherCoord = float2(dispatchThreadId.xy) * ViewportSizeInverse;
    float2 ViewportUV = gatherCoord + float2(0.5, 0.5) * ViewportSizeInverse;

    //derived from ffx_fsr2_reconstruct_dilated_velocity_and_previous_depth.h
    //FindNearestDepth

    int2 InputPosBtmRight = int2(1, 1) + int2(dispatchThreadId.xy);
    float NearestZ = constants.Depth.Load(int3(InputPosBtmRight, 0)).x;
    float4 topleft = constants.Depth.Gather(constants.Sampler, gatherCoord);

    NearestZ = min(topleft.x, NearestZ);
    NearestZ = min(topleft.y, NearestZ);
    NearestZ = min(topleft.z, NearestZ);
    NearestZ = min(topleft.w, NearestZ);

    float2 v11 = float2(ViewportSizeInverse.x, 0.0);
    float2 topRight = constants.Depth.Gather(constants.Sampler, (gatherCoord + v11)).yz;

    NearestZ = min(topRight.x, NearestZ);
    NearestZ = min(topRight.y, NearestZ);

    float2 v13 = float2(0.0, ViewportSizeInverse.y);
    float2 bottomLeft = constants.Depth.Gather(constants.Sampler, (gatherCoord + v13)).xy;

    NearestZ = min(bottomLeft.x, NearestZ);
    NearestZ = min(bottomLeft.y, NearestZ);

    //refer to ue/fsr2 PostProcessFFX_FSR2ConvertVelocity.usf, and using nearest depth for dilated motion

    float4 EncodedVelocity = constants.MotionVectors.Load(int3(int2(dispatchThreadId.xy), 0));

    float2 motion;
    if (EncodedVelocity.x > 0.0)
    {
        motion = DecodeVelocityFromTexture(EncodedVelocity.xy);
    }
    else
    {
        float2 ScreenPos = float2(2.0 * ViewportUV.x - 1.0, 1.0 - 2.0 * ViewportUV.y);
        float4 Position = float4(ScreenPos, NearestZ, 1.0);
        float4 PreClip = mul(Position, constants.ClipToPrevClip);
        float2 PreScreen = PreClip.xy / PreClip.w;
        motion = Position.xy - PreScreen;
    }

    ////////////compute luma
    float3 Colorrgb = constants.Input.Load(int3(int2(InputPos), 0)).xyz;

    ///simple tonemap
    Colorrgb /= max(max(Colorrgb.x, Colorrgb.y), Colorrgb.z) + h0;

    float3 Colorycocg;
    Colorycocg.x = 0.25 * (Colorrgb.x + 2.0 * Colorrgb.y + Colorrgb.z);
    Colorycocg.y = clamp(0.5 * Colorrgb.x + 0.5 - 0.5 * Colorrgb.z, 0.0, 1.0);
    Colorycocg.z = clamp(Colorycocg.x + Colorycocg.y - Colorrgb.x, 0.0, 1.0);

    //now color YCoCG all in the range of [0,1]
    uint x11 = uint(Colorycocg.x * 2047.5);
    uint y11 = uint(Colorycocg.y * 2047.5);
    uint z10 = uint(Colorycocg.z * 1023.5);

    float3 Colorprergb = constants.OpaqueInput.Load(int3(int2(InputPos), 0)).xyz;

    ///simple tonemap
    Colorprergb /= max(max(Colorprergb.x, Colorprergb.y), Colorprergb.z) + h0;
    float3 delta = abs(Colorrgb - Colorprergb);
    float alpha_mask = max(delta.x, max(delta.y, delta.z));
    alpha_mask = (0.35 * 1000.0) * alpha_mask;

    constants.YCoCg[int2(dispatchThreadId.xy)] = ((x11 << 21) | (y11 << 10)) | z10;

    float4 v29 = float4(motion, NearestZ, alpha_mask);
    constants.Motion[int2(dispatchThreadId.xy)] = v29;
}
""";
}

[StructLayout(LayoutKind.Explicit, Size = 208)]
file struct Constants
{
    [FieldOffset(0)]
    public uint RenderWidth;

    [FieldOffset(4)]
    public uint RenderHeight;

    [FieldOffset(8)]
    public uint DisplayWidth;

    [FieldOffset(12)]
    public uint DisplayHeight;

    [FieldOffset(16)]
    public float ViewportInvX;

    [FieldOffset(20)]
    public float ViewportInvY;

    [FieldOffset(24)]
    public float DisplayRcpX;

    [FieldOffset(28)]
    public float DisplayRcpY;

    [FieldOffset(32)]
    public float JitterOffsetX;

    [FieldOffset(36)]
    public float JitterOffsetY;

    [FieldOffset(40)]
    public float PaddingX;

    [FieldOffset(44)]
    public float PaddingY;

    [FieldOffset(48)]
    public Matrix4x4 ClipToPrevClip;

    [FieldOffset(112)]
    public float PreExposure;

    [FieldOffset(116)]
    public float CameraFovAngleHor;

    [FieldOffset(120)]
    public float CameraNear;

    [FieldOffset(124)]
    public float MinLerpContribution;

    [FieldOffset(128)]
    public uint SameCamera;

    [FieldOffset(132)]
    public uint Reset;

    [FieldOffset(136)]
    public uint SameCameraResetZ;

    [FieldOffset(140)]
    public uint SameCameraResetW;

    [FieldOffset(144)]
    public ResourceHandle Input;

    [FieldOffset(152)]
    public ResourceHandle OpaqueInput;

    [FieldOffset(160)]
    public ResourceHandle Depth;

    [FieldOffset(168)]
    public ResourceHandle MotionVectors;

    [FieldOffset(176)]
    public ResourceHandle YCoCg;

    [FieldOffset(184)]
    public ResourceHandle Motion;

    [FieldOffset(192)]
    public ResourceHandle Sampler;

    public Constants(TemporalUpscalerDesc desc, TemporalUpscalerArgs args, ResourceHandle yCoCg, ResourceHandle motion, ResourceHandle sampler)
    {
        RenderWidth = args.InputWidth;
        RenderHeight = args.InputHeight;
        DisplayWidth = desc.OutputWidth;
        DisplayHeight = desc.OutputHeight;
        ViewportInvX = 1.0f / args.InputWidth;
        ViewportInvY = 1.0f / args.InputHeight;
        DisplayRcpX = 1.0f / desc.OutputWidth;
        DisplayRcpY = 1.0f / desc.OutputHeight;
        JitterOffsetX = args.JitterOffsetX;
        JitterOffsetY = args.JitterOffsetY;
        PaddingX = 0.0f;
        PaddingY = 0.0f;
        ClipToPrevClip = args.ClipToPrevClip;
        PreExposure = args.PreExposure;
        CameraFovAngleHor = args.CameraFovAngleHor;
        CameraNear = 0.0f;
        MinLerpContribution = args.MinLerpContribution;
        SameCamera = args.SameCamera ? 1u : 0u;
        Reset = args.Reset ? 1u : 0u;
        SameCameraResetZ = 0;
        SameCameraResetW = 0;
        Input = args.Input;
        OpaqueInput = args.OpaqueInput;
        Depth = args.Depth;
        MotionVectors = args.MotionVectors;
        YCoCg = yCoCg;
        Motion = motion;
        Sampler = sampler;
    }
}
