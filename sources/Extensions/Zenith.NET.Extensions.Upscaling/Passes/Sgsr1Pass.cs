using System.Runtime.InteropServices;

namespace Zenith.NET.Extensions.Upscaling.Passes;

internal unsafe partial class Sgsr1Pass : DisposableObject
{
    private readonly Buffer buffer;
    private readonly Sampler sampler;
    private readonly ComputePipeline pipeline;

    public Sgsr1Pass(GraphicsContext context)
    {
        using Shader shader = context.CreateShader(context.GraphicsApi switch
        {
            GraphicsApi.DirectX12 => DirectX12Main,
            GraphicsApi.Metal => MetalMain,
            GraphicsApi.Vulkan => VulkanMain,
            _ => default
        });

        buffer = context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(Constants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
        sampler = context.CreateSampler(SamplerDesc.PointClamp());
        pipeline = context.CreateComputePipeline(new() { ComputeShader = shader });
    }

    public void Record(CommandBuffer commandBuffer, SpatialUpscalerDesc desc, SpatialUpscalerArgs args)
    {
        Constants constants = new(desc, args, sampler.Handle);

        buffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(Constants)
        });

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(buffer, 0);
        commandBuffer.Dispatch((constants.OutputWidth + 7) / 8, (constants.OutputHeight + 7) / 8, 1);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.All);
    }

    protected override void Destroy()
    {
        pipeline.Dispose();
        sampler.Dispose();
        buffer.Dispose();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 64)]
file struct Constants(SpatialUpscalerDesc desc, SpatialUpscalerArgs args, ResourceHandle sampler)
{
    [FieldOffset(0)]
    public float ViewportInfoX = 1.0f / desc.InputWidth;

    [FieldOffset(4)]
    public float ViewportInfoY = 1.0f / desc.InputHeight;

    [FieldOffset(8)]
    public float ViewportInfoZ = desc.InputWidth;

    [FieldOffset(12)]
    public float ViewportInfoW = desc.InputHeight;

    [FieldOffset(16)]
    public uint InputWidth = desc.InputWidth;

    [FieldOffset(20)]
    public uint InputHeight = desc.InputHeight;

    [FieldOffset(24)]
    public uint OutputWidth = desc.OutputWidth;

    [FieldOffset(28)]
    public uint OutputHeight = desc.OutputHeight;

    [FieldOffset(32)]
    public ResourceHandle Input = args.Input;

    [FieldOffset(40)]
    public ResourceHandle Output = args.Output;

    [FieldOffset(48)]
    public ResourceHandle Sampler = sampler;
}
