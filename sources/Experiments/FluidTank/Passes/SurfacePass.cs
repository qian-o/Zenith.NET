using System.Numerics;
using System.Runtime.InteropServices;
using FluidTank.Helpers;
using FluidTank.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank.Passes;

internal unsafe class SurfacePass : IDisposable
{
    private readonly GraphicsContext context;

    private readonly Buffer constants;

    private readonly Buffer particleConstants;

    private readonly Sampler sampler;

    private readonly ComputePipeline clearPipeline;

    private readonly ComputePipeline depositPipeline;

    private readonly ComputePipeline smoothPipeline;

    private readonly ComputePipeline fieldPipeline;

    private readonly ComputePipeline tracePipeline;

    private readonly GraphicsPipeline particlePipeline;

    private Buffer accumulation = null!;

    private Texture density = null!;

    private Texture field = null!;

    private Texture occupancy = null!;

    private Texture depth = null!;

    private Texture thickness = null!;

    private Texture normal = null!;

    private Vector3 origin;

    private float cellSize;

    private ulong particleVersion;

    private float interpolationAlpha;

    private bool fieldReady;

    public SurfacePass(GraphicsContext context)
    {
        this.context = context;

        constants = GraphicsHelper.CreateConstantBuffer<DensityConstants>(context);
        particleConstants = GraphicsHelper.CreateConstantBuffer<ParticleConstants>(context);
        sampler = context.CreateSampler(SamplerDesc.LinearClamp());
        clearPipeline = GraphicsHelper.CreateComputePipeline(context, "DensitySurface.slang", "ClearCS");
        depositPipeline = GraphicsHelper.CreateComputePipeline(context, "DensitySurface.slang", "DepositCS");
        smoothPipeline = GraphicsHelper.CreateComputePipeline(context, "DensitySurface.slang", "SmoothCS");
        fieldPipeline = GraphicsHelper.CreateComputePipeline(context, "DensitySurface.slang", "BuildFieldCS");
        tracePipeline = GraphicsHelper.CreateComputePipeline(context, "DensitySurface.slang", "TraceCS");

        particlePipeline = GraphicsHelper.CreateGraphicsPipeline(context, "Particles.slang", "ParticleVS", "ParticleFS", [], new()
        {
            ColorFormats = [PixelFormat.R16G16B16A16Float],
            DepthStencilFormat = PixelFormat.D32FloatS8UInt,
            SampleCount = SampleCount.Count1
        }, RasterizerState.CullNone(), DepthStencilState.DepthReadWrite(), BlendState.Opaque(), PrimitiveTopology.TriangleStrip);
    }

    public SurfaceData Output => new() { Depth = depth, Thickness = thickness, Normal = normal };

    public void Resize(uint width, uint height)
    {
        if (depth is not null && depth.Desc.Width == width && depth.Desc.Height == height)
        {
            return;
        }

        DisposeTargets();

        depth = GraphicsHelper.CreateTexture(context, PixelFormat.R32Float, width, height, TextureUsages.Sampled | TextureUsages.Storage);
        thickness = GraphicsHelper.CreateTexture(context, PixelFormat.R16Float, width, height, TextureUsages.Sampled | TextureUsages.Storage);
        normal = GraphicsHelper.CreateTexture(context, PixelFormat.R16G16B16A16Float, width, height, TextureUsages.Sampled | TextureUsages.Storage);
    }

    public void Render(CommandBuffer commandBuffer, FrameData frame, ParticleData particles, Texture sceneDepth)
    {
        if (density is null)
        {
            density = CreateVolume(particles);
        }

        DensityConstants parameters = new()
        {
            View = frame.View,
            InvView = frame.InvView,
            InvProjection = frame.InvProjection,
            Origin = origin,
            CellSize = cellSize,
            Minimum = particles.Minimum,
            InverseCellSize = 1.0f / cellSize,
            Maximum = particles.Maximum,
            IsoValue = 0.5f,
            GridX = density.Desc.Width,
            GridY = density.Desc.Height,
            GridZ = density.Desc.Depth,
            ParticleCount = particles.Count,
            Width = depth.Desc.Width,
            Height = depth.Desc.Height,
            InterpolationAlpha = frame.InterpolationAlpha,
            VolumeScale = particles.Spacing * particles.Spacing * particles.Spacing / (cellSize * cellSize * cellSize),
            Particles = particles.Particles.StorageReadOnlyHandle,
            PreviousPositions = particles.PreviousPositions.StorageReadOnlyHandle,
            Accumulation = accumulation.StorageReadWriteHandle,
            DensityWrite = density.StorageHandle,
            DensityRead = density.SampledHandle,
            FieldWrite = field.StorageHandle,
            FieldRead = field.SampledHandle,
            OccupancyWrite = occupancy.StorageHandle,
            OccupancyRead = occupancy.SampledHandle,
            SceneDepth = sceneDepth.SampledHandle,
            DepthOutput = depth.StorageHandle,
            ThicknessOutput = thickness.StorageHandle,
            NormalOutput = normal.StorageHandle,
            Sampler = sampler.Handle
        };
        GraphicsHelper.Upload(constants, 0, &parameters, (uint)sizeof(DensityConstants));

        if (!fieldReady || particleVersion != particles.Version || interpolationAlpha != frame.InterpolationAlpha)
        {
            Dispatch(commandBuffer, clearPipeline, density.Desc.Width * density.Desc.Height * density.Desc.Depth, 1, 1);
            commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
            Dispatch(commandBuffer, depositPipeline, particles.Count, 1, 1);
            commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

            TextureLayout previousLayout = fieldReady ? TextureLayout.Sampled : TextureLayout.Undefined;
            commandBuffer.Transition(density, default, previousLayout, TextureLayout.Storage);
            Dispatch(commandBuffer, smoothPipeline, density.Desc.Width, density.Desc.Height, density.Desc.Depth);
            commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
            commandBuffer.Transition(density, default, TextureLayout.Storage, TextureLayout.Sampled);

            commandBuffer.Transition(field, default, previousLayout, TextureLayout.Storage);
            commandBuffer.Transition(occupancy, default, previousLayout, TextureLayout.Storage);
            Dispatch(commandBuffer, fieldPipeline, density.Desc.Width, density.Desc.Height, density.Desc.Depth);
            commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
            commandBuffer.Transition(field, default, TextureLayout.Storage, TextureLayout.Sampled);
            commandBuffer.Transition(occupancy, default, TextureLayout.Storage, TextureLayout.Sampled);

            particleVersion = particles.Version;
            interpolationAlpha = frame.InterpolationAlpha;
            fieldReady = true;
        }

        commandBuffer.Transition(depth, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.Transition(thickness, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.Transition(normal, default, TextureLayout.Undefined, TextureLayout.Storage);
        Dispatch(commandBuffer, tracePipeline, depth.Desc.Width, depth.Desc.Height, 1);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading | BarrierStages.FragmentShading);
        commandBuffer.Transition(depth, default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Transition(thickness, default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Transition(normal, default, TextureLayout.Storage, TextureLayout.Sampled);
    }

    public void RenderParticles(CommandBuffer commandBuffer, FrameData frame, ParticleData particles, Texture color, Texture sceneDepthStencil)
    {
        ParticleConstants parameters = new()
        {
            View = frame.View,
            Projection = frame.Projection,
            CameraRight = frame.Right,
            Radius = particles.Radius * 0.78f,
            CameraUp = frame.Up,
            InterpolationAlpha = frame.InterpolationAlpha,
            Particles = particles.Particles.StorageReadOnlyHandle,
            PreviousPositions = particles.PreviousPositions.StorageReadOnlyHandle
        };
        GraphicsHelper.Upload(particleConstants, 0, &parameters, (uint)sizeof(ParticleConstants));

        commandBuffer.Transition(color, default, TextureLayout.Sampled, TextureLayout.ColorAttachment);
        commandBuffer.Transition(sceneDepthStencil, default, TextureLayout.DepthStencilAttachment, TextureLayout.DepthStencilAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.Load(color)], DepthStencilAttachment.Load(sceneDepthStencil));
        commandBuffer.SetPipeline(particlePipeline);
        commandBuffer.SetConstantBuffer(particleConstants, 0);
        commandBuffer.Draw(4, particles.Count, 0, 0);
        commandBuffer.EndRenderPass();
        commandBuffer.Transition(color, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
    }

    public void Dispose()
    {
        DisposeTargets();

        occupancy?.Dispose();
        field?.Dispose();
        density?.Dispose();
        accumulation?.Dispose();
        particlePipeline.Dispose();
        tracePipeline.Dispose();
        fieldPipeline.Dispose();
        smoothPipeline.Dispose();
        depositPipeline.Dispose();
        clearPipeline.Dispose();
        sampler.Dispose();
        particleConstants.Dispose();
        constants.Dispose();
    }

    private Texture CreateVolume(ParticleData particles)
    {
        cellSize = particles.Spacing * 1.4f;
        origin = particles.Minimum - new Vector3(cellSize * 4.0f);
        Vector3 extent = (particles.Maximum - particles.Minimum) / cellSize;
        uint width = ((uint)MathF.Ceiling(extent.X) + 12) / 4 * 4;
        uint height = ((uint)MathF.Ceiling(extent.Y) + 12) / 4 * 4;
        uint depth = ((uint)MathF.Ceiling(extent.Z) + 12) / 4 * 4;

        accumulation = GraphicsHelper.CreateBuffer(context, width * height * depth, sizeof(int), BufferUsages.StorageReadWrite);
        Texture density = context.CreateTexture(TextureDesc.Texture3D(PixelFormat.R16Float, width, height, depth, 1) with { Usages = TextureUsages.Sampled | TextureUsages.Storage });
        field = context.CreateTexture(TextureDesc.Texture3D(PixelFormat.R16G16B16A16Float, width, height, depth, 1) with { Usages = TextureUsages.Sampled | TextureUsages.Storage });
        occupancy = context.CreateTexture(TextureDesc.Texture3D(PixelFormat.R16Float, width / 4, height / 4, depth / 4, 1) with { Usages = TextureUsages.Sampled | TextureUsages.Storage });

        return density;
    }

    private void Dispatch(CommandBuffer commandBuffer, ComputePipeline pipeline, uint width, uint height, uint depth)
    {
        ThreadGroupSize group = pipeline.Desc.ComputeShader.Desc.ThreadGroupSize;

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(constants, 0);
        commandBuffer.Dispatch((width + group.X - 1) / group.X, (height + group.Y - 1) / group.Y, (depth + group.Z - 1) / group.Z);
    }

    private void DisposeTargets()
    {
        normal?.Dispose();
        thickness?.Dispose();
        depth?.Dispose();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 384)]
file struct DensityConstants
{
    [FieldOffset(0)]
    public Matrix4x4 View;

    [FieldOffset(64)]
    public Matrix4x4 InvView;

    [FieldOffset(128)]
    public Matrix4x4 InvProjection;

    [FieldOffset(192)]
    public Vector3 Origin;

    [FieldOffset(204)]
    public float CellSize;

    [FieldOffset(208)]
    public Vector3 Minimum;

    [FieldOffset(220)]
    public float InverseCellSize;

    [FieldOffset(224)]
    public Vector3 Maximum;

    [FieldOffset(236)]
    public float IsoValue;

    [FieldOffset(240)]
    public uint GridX;

    [FieldOffset(244)]
    public uint GridY;

    [FieldOffset(248)]
    public uint GridZ;

    [FieldOffset(252)]
    public uint ParticleCount;

    [FieldOffset(256)]
    public uint Width;

    [FieldOffset(260)]
    public uint Height;

    [FieldOffset(264)]
    public float InterpolationAlpha;

    [FieldOffset(268)]
    public float VolumeScale;

    [FieldOffset(272)]
    public ResourceHandle Particles;

    [FieldOffset(280)]
    public ResourceHandle PreviousPositions;

    [FieldOffset(288)]
    public ResourceHandle Accumulation;

    [FieldOffset(296)]
    public ResourceHandle DensityWrite;

    [FieldOffset(304)]
    public ResourceHandle DensityRead;

    [FieldOffset(312)]
    public ResourceHandle FieldWrite;

    [FieldOffset(320)]
    public ResourceHandle FieldRead;

    [FieldOffset(328)]
    public ResourceHandle OccupancyWrite;

    [FieldOffset(336)]
    public ResourceHandle OccupancyRead;

    [FieldOffset(344)]
    public ResourceHandle SceneDepth;

    [FieldOffset(352)]
    public ResourceHandle DepthOutput;

    [FieldOffset(360)]
    public ResourceHandle ThicknessOutput;

    [FieldOffset(368)]
    public ResourceHandle NormalOutput;

    [FieldOffset(376)]
    public ResourceHandle Sampler;
}

[StructLayout(LayoutKind.Explicit, Size = 176)]
file struct ParticleConstants
{
    [FieldOffset(0)]
    public Matrix4x4 View;

    [FieldOffset(64)]
    public Matrix4x4 Projection;

    [FieldOffset(128)]
    public Vector3 CameraRight;

    [FieldOffset(140)]
    public float Radius;

    [FieldOffset(144)]
    public Vector3 CameraUp;

    [FieldOffset(156)]
    public float InterpolationAlpha;

    [FieldOffset(160)]
    public ResourceHandle Particles;

    [FieldOffset(168)]
    public ResourceHandle PreviousPositions;
}
