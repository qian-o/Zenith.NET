using System.Numerics;
using System.Runtime.InteropServices;
using FluidTank.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank.Passes;

internal unsafe class SurfacePass(uint width, uint height) : Pass(width, height)
{
    private const float SurfaceScale = 0.5f;

    private Buffer constants = null!;
    private Buffer particleConstants = null!;
    private Sampler sampler = null!;
    private ComputePipeline clearPipeline = null!;
    private ComputePipeline depositPipeline = null!;
    private ComputePipeline smoothPipeline = null!;
    private ComputePipeline fieldPipeline = null!;
    private ComputePipeline tracePipeline = null!;
    private GraphicsPipeline particlePipeline = null!;

    private Buffer accumulation = null!;
    private Texture density = null!;
    private Texture field = null!;
    private Texture occupancy = null!;

    private Vector3 origin;
    private float cellSize;

    public Texture Depth { get; private set; } = null!;

    public Texture Thickness { get; private set; } = null!;

    public Texture Normal { get; private set; } = null!;

    protected override void Initialize()
    {
        constants = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(DensityConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
        particleConstants = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(ParticleConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
        sampler = App.Context.CreateSampler(SamplerDesc.LinearClamp());

        using Shader clearShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("DensitySurface.slang"), "ClearCS"));
        using Shader depositShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("DensitySurface.slang"), "DepositCS"));
        using Shader smoothShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("DensitySurface.slang"), "SmoothCS"));
        using Shader fieldShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("DensitySurface.slang"), "BuildFieldCS"));
        using Shader traceShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("DensitySurface.slang"), "TraceCS"));

        clearPipeline = App.Context.CreateComputePipeline(new() { ComputeShader = clearShader });
        depositPipeline = App.Context.CreateComputePipeline(new() { ComputeShader = depositShader });
        smoothPipeline = App.Context.CreateComputePipeline(new() { ComputeShader = smoothShader });
        fieldPipeline = App.Context.CreateComputePipeline(new() { ComputeShader = fieldShader });
        tracePipeline = App.Context.CreateComputePipeline(new() { ComputeShader = traceShader });

        using Shader particleVertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("Particles.slang"), "ParticleVS"));
        using Shader particleFragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("Particles.slang"), "ParticleFS"));

        particlePipeline = App.Context.CreateGraphicsPipeline(new()
        {
            VertexShader = particleVertexShader,
            FragmentShader = particleFragmentShader,
            InputLayouts = [],
            PrimitiveTopology = PrimitiveTopology.TriangleStrip,
            AttachmentFormats = new()
            {
                ColorFormats = [PixelFormat.R16G16B16A16Float],
                DepthStencilFormat = PixelFormat.D32FloatS8UInt,
                SampleCount = SampleCount.Count1
            },
            RenderState = new()
            {
                Rasterizer = RasterizerState.CullNone(),
                DepthStencil = DepthStencilState.DepthReadWrite(),
                Blend = BlendState.Opaque()
            }
        });

        ResizeImpl();
    }

    protected override void RecordImpl(CommandBuffer commandBuffer, in PassArgs args)
    {
        switch (args.ViewMode)
        {
            case FluidViewMode.Water:
                RecordSurface(commandBuffer, in args);
                break;

            case FluidViewMode.Particles:
                RecordParticles(commandBuffer, in args);
                break;
        }
    }

    protected override void ResizeImpl()
    {
        uint width = Math.Max((uint)(Width * SurfaceScale), 1);
        uint height = Math.Max((uint)(Height * SurfaceScale), 1);

        if (Depth is not null && Depth.Desc.Width == width && Depth.Desc.Height == height)
        {
            return;
        }

        DisposeTargets();

        Depth = CreateTexture(width, height, PixelFormat.R32Float);
        Thickness = CreateTexture(width, height, PixelFormat.R16Float);
        Normal = CreateTexture(width, height, PixelFormat.R16G16B16A16Float);
    }

    protected override void Destroy()
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

    private void RecordSurface(CommandBuffer commandBuffer, in PassArgs args)
    {
        ParticleData particles = args.Particles;

        density ??= CreateVolume(particles);

        DensityConstants densityData = new()
        {
            View = args.View,
            InvView = args.InverseView,
            InvProjection = args.InverseProjection,
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
            Width = Depth.Desc.Width,
            Height = Depth.Desc.Height,
            InterpolationAlpha = args.InterpolationAlpha,
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
            SceneDepth = args.SceneDepth.SampledHandle,
            DepthOutput = Depth.StorageHandle,
            ThicknessOutput = Thickness.StorageHandle,
            NormalOutput = Normal.StorageHandle,
            Sampler = sampler.Handle
        };

        constants.Upload(0, new()
        {
            Pointer = (nint)(&densityData),
            SizeInBytes = (uint)sizeof(DensityConstants)
        });

        Dispatch(commandBuffer, clearPipeline, density.Desc.Width * density.Desc.Height * density.Desc.Depth, 1, 1);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
        Dispatch(commandBuffer, depositPipeline, particles.Count, 1, 1);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

        commandBuffer.Transition(density, default, TextureLayout.Undefined, TextureLayout.Storage);
        Dispatch(commandBuffer, smoothPipeline, density.Desc.Width, density.Desc.Height, density.Desc.Depth);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
        commandBuffer.Transition(density, default, TextureLayout.Storage, TextureLayout.Sampled);

        commandBuffer.Transition(field, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.Transition(occupancy, default, TextureLayout.Undefined, TextureLayout.Storage);
        Dispatch(commandBuffer, fieldPipeline, density.Desc.Width, density.Desc.Height, density.Desc.Depth);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
        commandBuffer.Transition(field, default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Transition(occupancy, default, TextureLayout.Storage, TextureLayout.Sampled);

        commandBuffer.Transition(Depth, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.Transition(Thickness, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.Transition(Normal, default, TextureLayout.Undefined, TextureLayout.Storage);
        Dispatch(commandBuffer, tracePipeline, Depth.Desc.Width, Depth.Desc.Height, 1);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading | BarrierStages.FragmentShading);
        commandBuffer.Transition(Depth, default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Transition(Thickness, default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Transition(Normal, default, TextureLayout.Storage, TextureLayout.Sampled);
    }

    private void RecordParticles(CommandBuffer commandBuffer, in PassArgs args)
    {
        ParticleData particles = args.Particles;
        Texture color = args.Color;
        Texture sceneDepthStencil = args.DepthStencil;
        ParticleConstants particleData = new()
        {
            View = args.View,
            Projection = args.Projection,
            CameraRight = args.CameraRight,
            Radius = particles.Radius * 0.78f,
            CameraUp = args.CameraUp,
            InterpolationAlpha = args.InterpolationAlpha,
            Particles = particles.Particles.StorageReadOnlyHandle,
            PreviousPositions = particles.PreviousPositions.StorageReadOnlyHandle
        };

        particleConstants.Upload(0, new()
        {
            Pointer = (nint)(&particleData),
            SizeInBytes = (uint)sizeof(ParticleConstants)
        });

        commandBuffer.Transition(color, default, TextureLayout.Sampled, TextureLayout.ColorAttachment);
        commandBuffer.Transition(sceneDepthStencil, default, TextureLayout.DepthStencilAttachment, TextureLayout.DepthStencilAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.Load(color)], DepthStencilAttachment.Load(sceneDepthStencil));
        commandBuffer.SetPipeline(particlePipeline);
        commandBuffer.SetConstantBuffer(particleConstants, 0);
        commandBuffer.Draw(4, particles.Count, 0, 0);
        commandBuffer.EndRenderPass();
        commandBuffer.Transition(color, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
    }

    private Texture CreateVolume(ParticleData particles)
    {
        cellSize = particles.Spacing * 1.4f;
        origin = particles.Minimum - new Vector3(cellSize * 4.0f);
        Vector3 extent = (particles.Maximum - particles.Minimum) / cellSize;
        uint width = ((uint)MathF.Ceiling(extent.X) + 12) / 4 * 4;
        uint height = ((uint)MathF.Ceiling(extent.Y) + 12) / 4 * 4;
        uint depth = ((uint)MathF.Ceiling(extent.Z) + 12) / 4 * 4;

        accumulation = App.Context.CreateBuffer(new()
        {
            SizeInBytes = width * height * depth * sizeof(int),
            StrideInBytes = sizeof(int),
            Usages = BufferUsages.StorageReadWrite,
            Residency = MemoryResidency.GpuOnly
        });
        Texture density = App.Context.CreateTexture(TextureDesc.Texture3D(PixelFormat.R16Float, width, height, depth, 1) with { Usages = TextureUsages.Sampled | TextureUsages.Storage });
        field = App.Context.CreateTexture(TextureDesc.Texture3D(PixelFormat.R16G16B16A16Float, width, height, depth, 1) with { Usages = TextureUsages.Sampled | TextureUsages.Storage });
        occupancy = App.Context.CreateTexture(TextureDesc.Texture3D(PixelFormat.R16Float, width / 4, height / 4, depth / 4, 1) with { Usages = TextureUsages.Sampled | TextureUsages.Storage });

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
        Normal?.Dispose();
        Thickness?.Dispose();
        Depth?.Dispose();
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
