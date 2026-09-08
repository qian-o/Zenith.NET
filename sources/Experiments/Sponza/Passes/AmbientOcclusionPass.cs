using System.Numerics;
using System.Runtime.InteropServices;
using Sponza.Helpers;
using Sponza.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace Sponza.Passes;

internal class AmbientOcclusionPass : IDisposable
{
    private readonly GraphicsContext context;
    private readonly Buffer solveConstants;
    private readonly Buffer filterConstants;
    private readonly Buffer compositeConstants;
    private readonly ComputePipeline solvePipeline;
    private readonly ComputePipeline filterPipeline;
    private readonly ComputePipeline compositePipeline;

    private Texture occlusion = null!;
    private Texture filteredOcclusion = null!;
    private Texture hdrColor = null!;
    private TextureLayout layout;

    public AmbientOcclusionPass(GraphicsContext context)
    {
        this.context = context;
        solveConstants = GraphicsHelper.CreateConstantBuffer<AmbientOcclusionConstants>(context);
        filterConstants = GraphicsHelper.CreateConstantBuffer<AmbientOcclusionConstants>(context);
        compositeConstants = GraphicsHelper.CreateConstantBuffer<AmbientOcclusionConstants>(context);
        solvePipeline = GraphicsHelper.CreateComputePipeline(context, "AmbientOcclusion.slang", "Solve");
        filterPipeline = GraphicsHelper.CreateComputePipeline(context, "AmbientOcclusion.slang", "Filter");
        compositePipeline = GraphicsHelper.CreateComputePipeline(context, "AmbientOcclusion.slang", "Composite");
    }

    public void Resize(uint width, uint height)
    {
        DisposeTargets();

        uint halfWidth = Math.Max(1, (width + 1) / 2);
        uint halfHeight = Math.Max(1, (height + 1) / 2);
        TextureUsages usages = TextureUsages.Sampled | TextureUsages.Storage;
        occlusion = GraphicsHelper.CreateTexture(context, PixelFormat.R16Float, halfWidth, halfHeight, usages);
        filteredOcclusion = GraphicsHelper.CreateTexture(context, PixelFormat.R16Float, halfWidth, halfHeight, usages);
        hdrColor = GraphicsHelper.CreateTexture(context, PixelFormat.R16G16B16A16Float, width, height, usages);
        layout = TextureLayout.Undefined;
    }

    public Texture Record(CommandBuffer commandBuffer, AmbientOcclusionPassArgs args)
    {
        AmbientOcclusionConstants data = new()
        {
            Projection = args.Frame.Projection,
            InverseProjection = args.Frame.InverseProjection,
            DeviceDepth = args.DeviceDepth.SampledHandle,
            HdrColor = args.HdrColor.SampledHandle,
            IndirectDiffuse = args.IndirectDiffuse.SampledHandle,
            Occlusion = occlusion.SampledHandle,
            OcclusionOutput = occlusion.StorageHandle,
            HdrOutput = hdrColor.StorageHandle,
            RenderWidth = args.DeviceDepth.Desc.Width,
            RenderHeight = args.DeviceDepth.Desc.Height,
            HalfWidth = occlusion.Desc.Width,
            HalfHeight = occlusion.Desc.Height,
            RadiusInMeters = args.RadiusInMeters,
            Strength = args.Strength,
            Padding = default
        };
        GraphicsHelper.Upload(solveConstants, data);

        commandBuffer.Transition(occlusion, default, layout, TextureLayout.Storage);
        commandBuffer.SetPipeline(solvePipeline);
        commandBuffer.SetConstantBuffer(solveConstants, 0);
        commandBuffer.Dispatch((occlusion.Desc.Width + 7) / 8, (occlusion.Desc.Height + 7) / 8, 1);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
        commandBuffer.Transition(occlusion, default, TextureLayout.Storage, TextureLayout.Sampled);

        data.OcclusionOutput = filteredOcclusion.StorageHandle;
        GraphicsHelper.Upload(filterConstants, data);

        commandBuffer.Transition(filteredOcclusion, default, layout, TextureLayout.Storage);
        commandBuffer.SetPipeline(filterPipeline);
        commandBuffer.SetConstantBuffer(filterConstants, 0);
        commandBuffer.Dispatch((filteredOcclusion.Desc.Width + 7) / 8, (filteredOcclusion.Desc.Height + 7) / 8, 1);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
        commandBuffer.Transition(filteredOcclusion, default, TextureLayout.Storage, TextureLayout.Sampled);

        data.Occlusion = filteredOcclusion.SampledHandle;
        GraphicsHelper.Upload(compositeConstants, data);

        commandBuffer.Transition(hdrColor, default, layout, TextureLayout.Storage);
        commandBuffer.SetPipeline(compositePipeline);
        commandBuffer.SetConstantBuffer(compositeConstants, 0);
        commandBuffer.Dispatch((hdrColor.Desc.Width + 7) / 8, (hdrColor.Desc.Height + 7) / 8, 1);
        commandBuffer.Transition(hdrColor, default, TextureLayout.Storage, TextureLayout.Sampled);
        layout = TextureLayout.Sampled;

        return hdrColor;
    }

    public void Dispose()
    {
        DisposeTargets();
        compositePipeline.Dispose();
        filterPipeline.Dispose();
        solvePipeline.Dispose();
        compositeConstants.Dispose();
        filterConstants.Dispose();
        solveConstants.Dispose();
    }

    private void DisposeTargets()
    {
        hdrColor?.Dispose();
        filteredOcclusion?.Dispose();
        occlusion?.Dispose();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 208)]
file struct AmbientOcclusionConstants
{
    [FieldOffset(0)]
    public Matrix4x4 Projection;

    [FieldOffset(64)]
    public Matrix4x4 InverseProjection;

    [FieldOffset(128)]
    public ResourceHandle DeviceDepth;

    [FieldOffset(136)]
    public ResourceHandle HdrColor;

    [FieldOffset(144)]
    public ResourceHandle IndirectDiffuse;

    [FieldOffset(152)]
    public ResourceHandle Occlusion;

    [FieldOffset(160)]
    public ResourceHandle OcclusionOutput;

    [FieldOffset(168)]
    public ResourceHandle HdrOutput;

    [FieldOffset(176)]
    public uint RenderWidth;

    [FieldOffset(180)]
    public uint RenderHeight;

    [FieldOffset(184)]
    public uint HalfWidth;

    [FieldOffset(188)]
    public uint HalfHeight;

    [FieldOffset(192)]
    public float RadiusInMeters;

    [FieldOffset(196)]
    public float Strength;

    [FieldOffset(200)]
    public Vector2 Padding;
}
