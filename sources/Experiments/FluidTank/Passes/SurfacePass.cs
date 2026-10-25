using System.Numerics;
using System.Runtime.InteropServices;
using FluidTank.Handlers;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank.Passes;

internal unsafe class SurfacePass : DisposableObject
{
    private const float SurfaceScale = 0.5f;
    private const float CellSize = Simulation.ParticleSpacing * 1.4f;

    private static readonly Vector3 Origin = Simulation.TankMin - new Vector3(CellSize * 4.0f);

    private readonly Buffer constants;
    private readonly Sampler sampler;
    private readonly ComputePipeline clearPipeline;
    private readonly ComputePipeline depositPipeline;
    private readonly ComputePipeline smoothPipeline;
    private readonly ComputePipeline fieldPipeline;
    private readonly ComputePipeline tracePipeline;
    private readonly Buffer accumulation;
    private readonly Texture density;
    private readonly Texture field;
    private readonly Texture occupancy;

    public SurfacePass(uint width, uint height)
    {
        constants = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(DensityConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
        sampler = App.Context.CreateSampler(SamplerDesc.LinearClamp());

        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "DensitySurface.slang");
        using Shader clearShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "ClearCS"));
        using Shader depositShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "DepositCS"));
        using Shader smoothShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "SmoothCS"));
        using Shader fieldShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "BuildFieldCS"));
        using Shader traceShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "TraceCS"));

        clearPipeline = App.Context.CreateComputePipeline(new() { ComputeShader = clearShader });
        depositPipeline = App.Context.CreateComputePipeline(new() { ComputeShader = depositShader });
        smoothPipeline = App.Context.CreateComputePipeline(new() { ComputeShader = smoothShader });
        fieldPipeline = App.Context.CreateComputePipeline(new() { ComputeShader = fieldShader });
        tracePipeline = App.Context.CreateComputePipeline(new() { ComputeShader = traceShader });

        Vector3 extent = (Simulation.TankMax - Simulation.TankMin) / CellSize;
        uint gridWidth = ((uint)MathF.Ceiling(extent.X) + 12) / 4 * 4;
        uint gridHeight = ((uint)MathF.Ceiling(extent.Y) + 12) / 4 * 4;
        uint gridDepth = ((uint)MathF.Ceiling(extent.Z) + 12) / 4 * 4;

        accumulation = App.Context.CreateBuffer(new()
        {
            SizeInBytes = gridWidth * gridHeight * gridDepth * sizeof(int),
            StrideInBytes = sizeof(int),
            Usages = BufferUsages.StorageReadWrite,
            Residency = MemoryResidency.GpuOnly
        });
        density = App.Context.CreateTexture(TextureDesc.Texture3D(PixelFormat.R16Float, gridWidth, gridHeight, gridDepth, 1) with { Usages = TextureUsages.Sampled | TextureUsages.Storage });
        field = App.Context.CreateTexture(TextureDesc.Texture3D(PixelFormat.R16G16B16A16Float, gridWidth, gridHeight, gridDepth, 1) with { Usages = TextureUsages.Sampled | TextureUsages.Storage });
        occupancy = App.Context.CreateTexture(TextureDesc.Texture3D(PixelFormat.R16Float, gridWidth / 4, gridHeight / 4, gridDepth / 4, 1) with { Usages = TextureUsages.Sampled | TextureUsages.Storage });

        Resize(width, height);
    }

    public Texture Depth { get; private set; } = null!;

    public Texture Thickness { get; private set; } = null!;

    public Texture Normal { get; private set; } = null!;

    public void Record(CommandBuffer commandBuffer, Simulation simulation, CameraHandler camera, Texture sceneDepth, float interpolationAlpha)
    {
        Matrix4x4 view = camera.View;
        Matrix4x4.Invert(view, out Matrix4x4 inverseView);
        Matrix4x4.Invert(camera.Projection, out Matrix4x4 inverseProjection);

        DensityConstants densityData = new()
        {
            View = view,
            InvView = inverseView,
            InvProjection = inverseProjection,
            Origin = Origin,
            CellSize = CellSize,
            Minimum = Simulation.TankMin,
            InverseCellSize = 1.0f / CellSize,
            Maximum = Simulation.TankMax,
            IsoValue = 0.5f,
            GridX = density.Desc.Width,
            GridY = density.Desc.Height,
            GridZ = density.Desc.Depth,
            ParticleCount = Simulation.ParticleCount,
            Width = Depth.Desc.Width,
            Height = Depth.Desc.Height,
            InterpolationAlpha = interpolationAlpha,
            VolumeScale = Simulation.ParticleSpacing * Simulation.ParticleSpacing * Simulation.ParticleSpacing / (CellSize * CellSize * CellSize),
            Particles = simulation.Particles.StorageReadOnlyHandle,
            PreviousPositions = simulation.PreviousPositions.StorageReadOnlyHandle,
            Accumulation = accumulation.StorageReadWriteHandle,
            DensityWrite = density.StorageHandle,
            DensityRead = density.SampledHandle,
            FieldWrite = field.StorageHandle,
            FieldRead = field.SampledHandle,
            OccupancyWrite = occupancy.StorageHandle,
            OccupancyRead = occupancy.SampledHandle,
            SceneDepth = sceneDepth.SampledHandle,
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
        Dispatch(commandBuffer, depositPipeline, Simulation.ParticleCount, 1, 1);
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

    public void Resize(uint width, uint height)
    {
        width = Math.Max((uint)(width * SurfaceScale), 1);
        height = Math.Max((uint)(height * SurfaceScale), 1);

        if (Depth is not null && Depth.Desc.Width == width && Depth.Desc.Height == height)
        {
            return;
        }

        DestroyTextures();
        CreateTextures(width, height);
    }

    protected override void Destroy()
    {
        DestroyTextures();
        occupancy?.Dispose();
        field?.Dispose();
        density?.Dispose();
        accumulation?.Dispose();
        tracePipeline?.Dispose();
        fieldPipeline?.Dispose();
        smoothPipeline?.Dispose();
        depositPipeline?.Dispose();
        clearPipeline?.Dispose();
        sampler?.Dispose();
        constants?.Dispose();
    }

    private void Dispatch(CommandBuffer commandBuffer, ComputePipeline pipeline, uint width, uint height, uint depth)
    {
        ThreadGroupSize group = pipeline.Desc.ComputeShader.Desc.ThreadGroupSize;

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(constants, 0);
        commandBuffer.Dispatch((width + group.X - 1) / group.X, (height + group.Y - 1) / group.Y, (depth + group.Z - 1) / group.Z);
    }

    private void CreateTextures(uint width, uint height)
    {
        Depth = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R32Float, width, height, 1, SampleCount.Count1) with { Usages = TextureUsages.Sampled | TextureUsages.Storage });
        Thickness = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R16Float, width, height, 1, SampleCount.Count1) with { Usages = TextureUsages.Sampled | TextureUsages.Storage });
        Normal = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R16G16B16A16Float, width, height, 1, SampleCount.Count1) with { Usages = TextureUsages.Sampled | TextureUsages.Storage });
    }

    private void DestroyTextures()
    {
        Normal?.Dispose();
        Normal = null!;
        Thickness?.Dispose();
        Thickness = null!;
        Depth?.Dispose();
        Depth = null!;
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
