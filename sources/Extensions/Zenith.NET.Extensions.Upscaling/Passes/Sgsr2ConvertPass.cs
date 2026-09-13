using System.Numerics;
using System.Runtime.InteropServices;

namespace Zenith.NET.Extensions.Upscaling.Passes;

internal unsafe partial class Sgsr2ConvertPass : DisposableObject
{
    private readonly Buffer buffer;
    private readonly Sampler sampler;
    private readonly ComputePipeline pipeline;

    public Sgsr2ConvertPass(GraphicsContext context, TemporalUpscalerMode mode)
    {
        using Shader shader = context.CreateShader(context.GraphicsApi switch
        {
            GraphicsApi.DirectX12 => mode is TemporalUpscalerMode.Speed ? DirectX12SpeedMain : DirectX12QualityMain,
            GraphicsApi.Metal => mode is TemporalUpscalerMode.Speed ? MetalSpeedMain : MetalQualityMain,
            GraphicsApi.Vulkan => mode is TemporalUpscalerMode.Speed ? VulkanSpeedMain : VulkanQualityMain,
            _ => default
        });

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

    public void Record(CommandBuffer commandBuffer, TemporalUpscalerDesc desc, TemporalUpscalerArgs args, ResourceHandle yCoCg, ResourceHandle motion)
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
}

[StructLayout(LayoutKind.Explicit, Size = 208)]
file struct Constants(TemporalUpscalerDesc desc, TemporalUpscalerArgs args, ResourceHandle yCoCg, ResourceHandle motion, ResourceHandle sampler)
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
    public float ViewportInvX = 1.0f / desc.InputWidth;

    [FieldOffset(20)]
    public float ViewportInvY = 1.0f / desc.InputHeight;

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
    public ResourceHandle Input = args.Input;

    [FieldOffset(152)]
    public ResourceHandle OpaqueInput = args.OpaqueInput;

    [FieldOffset(160)]
    public ResourceHandle Depth = args.Depth;

    [FieldOffset(168)]
    public ResourceHandle MotionVectors = args.MotionVectors;

    [FieldOffset(176)]
    public ResourceHandle YCoCg = yCoCg;

    [FieldOffset(184)]
    public ResourceHandle Motion = motion;

    [FieldOffset(192)]
    public ResourceHandle Sampler = sampler;
}
