using System.Numerics;
using System.Runtime.InteropServices;

namespace Zenith.NET.Extensions.Upscaling.Passes;

internal unsafe partial class Sgsr2ActivatePass : DisposableObject
{
    private readonly Buffer buffer;
    private readonly Sampler sampler;
    private readonly ComputePipeline pipeline;

    public Sgsr2ActivatePass(GraphicsContext context)
    {
        ShaderDesc shaderDesc;
        switch (context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                shaderDesc = DirectX12Main;
                break;

            case GraphicsApi.Metal:
                shaderDesc = MetalMain;
                break;

            case GraphicsApi.Vulkan:
                shaderDesc = VulkanMain;
                break;

            default:
                throw new NotSupportedException();
        }

        using Shader shader = context.CreateShader(shaderDesc);

        buffer = context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(Constants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
        sampler = context.CreateSampler(SamplerDesc.PointClamp());
        pipeline = context.CreateComputePipeline(new() { ComputeShader = shader });
    }

    public void Record(CommandBuffer commandBuffer, TemporalUpscalerDesc desc, TemporalUpscalerArgs args, ResourceHandle prevLumaHistory, ResourceHandle motionDepthAlpha, ResourceHandle yCoCg, ResourceHandle motionDepthClipAlpha, ResourceHandle lumaHistory)
    {
        Constants constants = new(desc, args, prevLumaHistory, motionDepthAlpha, yCoCg, motionDepthClipAlpha, lumaHistory, sampler.Handle);

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

[StructLayout(LayoutKind.Explicit, Size = 192)]
file struct Constants(TemporalUpscalerDesc desc, TemporalUpscalerArgs args, ResourceHandle prevLumaHistory, ResourceHandle motionDepthAlpha, ResourceHandle yCoCg, ResourceHandle motionDepthClipAlpha, ResourceHandle lumaHistory, ResourceHandle sampler)
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
    public ResourceHandle PrevLumaHistory = prevLumaHistory;

    [FieldOffset(152)]
    public ResourceHandle MotionDepthAlpha = motionDepthAlpha;

    [FieldOffset(160)]
    public ResourceHandle YCoCg = yCoCg;

    [FieldOffset(168)]
    public ResourceHandle MotionDepthClipAlpha = motionDepthClipAlpha;

    [FieldOffset(176)]
    public ResourceHandle LumaHistory = lumaHistory;

    [FieldOffset(184)]
    public ResourceHandle Sampler = sampler;
}
