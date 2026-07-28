using System.Numerics;
using System.Runtime.InteropServices;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank.Simulation;

internal unsafe class FluidSimulation : IDisposable
{
    private static readonly Vector3 TankMin = new(-6.0f, 0.0f, -3.0f);
    private static readonly Vector3 TankMax = new(6.0f, 5.2f, 3.0f);
    private static readonly (uint X, uint Y, uint Z) DamDimensions = (36, 44, 54);
    private const float GridSpacing = 0.20f;

    private readonly Buffer constantBuffer;
    private readonly Buffer particles;
    private readonly Buffer previousPositions;
    private readonly Buffer particleAffine;
    private readonly Buffer gridAccumulation;
    private readonly Buffer gridVelocity;
    private readonly Buffer gridVelocityOld;
    private readonly Buffer cellTypes;
    private readonly Buffer divergence;
    private readonly Buffer pressureA;

    private readonly ComputePipeline resetPipeline;
    private readonly ComputePipeline initializeGridPipeline;
    private readonly ComputePipeline clearGridPipeline;
    private readonly ComputePipeline beginParticleToGridPipeline;
    private readonly ComputePipeline particleToGridPipeline;
    private readonly ComputePipeline normalizeAndApplyForcesPipeline;
    private readonly ComputePipeline divergencePipeline;
    private readonly ComputePipeline pressureRedPipeline;
    private readonly ComputePipeline pressureBlackPipeline;
    private readonly ComputePipeline projectGridPipeline;
    private readonly ComputePipeline gridToParticleAndAdvectPipeline;

    private bool resetRequested = true;
    private Vector3 interactionOrigin;
    private Vector3 interactionDirection;
    private float interactionStrength;
    private readonly uint pressureParityDispatchCount;
    private TimelineValue ready;

    public FluidSimulation()
    {
        ParticleCount = DamDimensions.X * DamDimensions.Y * DamDimensions.Z;

        Vector3 tankExtent = TankMax - TankMin;
        GridDimensions = new((uint)MathF.Ceiling(tankExtent.X / GridSpacing),
                             (uint)MathF.Ceiling(tankExtent.Y / GridSpacing),
                             (uint)MathF.Ceiling(tankExtent.Z / GridSpacing));
        CellCount = GridDimensions.X * GridDimensions.Y * GridDimensions.Z;
        GridPointCount = (GridDimensions.X + 1) * (GridDimensions.Y + 1) * (GridDimensions.Z + 1);
        pressureParityDispatchCount = (GridDimensions.X + 1) / 2 * GridDimensions.Y * GridDimensions.Z;

        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(SimulationConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        particles = CreateStorageBuffer(ParticleCount, 32, includeReadOnly: true);
        previousPositions = CreateStorageBuffer(ParticleCount, 16, includeReadOnly: true);
        particleAffine = CreateStorageBuffer(ParticleCount * 3, 16, includeReadOnly: false);
        gridAccumulation = CreateStorageBuffer(GridPointCount * 6, sizeof(int), includeReadOnly: false);
        gridVelocity = CreateStorageBuffer(GridPointCount, 16, includeReadOnly: false);
        gridVelocityOld = CreateStorageBuffer(GridPointCount, 16, includeReadOnly: false);
        cellTypes = CreateStorageBuffer(CellCount, sizeof(uint), includeReadOnly: false);
        divergence = CreateStorageBuffer(CellCount, sizeof(float), includeReadOnly: false);
        pressureA = CreateStorageBuffer(CellCount, sizeof(float), includeReadOnly: false);

        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "FluidSimulationAPIC.slang");

        resetPipeline = CreatePipeline(shaderPath, "ResetCS");
        initializeGridPipeline = CreatePipeline(shaderPath, "InitializeGridCS");
        clearGridPipeline = CreatePipeline(shaderPath, "ClearGridCS");
        beginParticleToGridPipeline = CreatePipeline(shaderPath, "BeginParticleToGridCS");
        particleToGridPipeline = CreatePipeline(shaderPath, "ParticleToGridCS");
        normalizeAndApplyForcesPipeline = CreatePipeline(shaderPath, "NormalizeAndApplyForcesCS");
        divergencePipeline = CreatePipeline(shaderPath, "DivergenceCS");
        pressureRedPipeline = CreatePipeline(shaderPath, "PressureRedCS");
        pressureBlackPipeline = CreatePipeline(shaderPath, "PressureBlackCS");
        projectGridPipeline = CreatePipeline(shaderPath, "ProjectGridCS");
        gridToParticleAndAdvectPipeline = CreatePipeline(shaderPath, "GridToParticleAndAdvectCS");
    }

    public uint ParticleCount { get; }

    public uint CellCount { get; }

    public uint GridPointCount { get; }

    public (uint X, uint Y, uint Z) GridDimensions { get; }

    public ResourceHandle ParticleHandle => particles.StorageReadOnlyHandle;

    public ResourceHandle PreviousPositionHandle => previousPositions.StorageReadOnlyHandle;

    public const float ParticleRadius = 0.0632f;

    public const float RestDensity = 5.52f;

    public float FlipRatio { get; set; } = 0.92f;

    public float VelocityDamping { get; set; } = 0.999f;

    public float WaveAmplitude { get; set; } = 0.12f;

    public float WaveFrequency { get; set; } = 0.58f;

    public bool WaveMakerEnabled { get; set; }

    public int PressureIterations
    {
        get;
        set => field = Math.Clamp(value, 4, 32);
    } = 16;

    public void Reset()
    {
        resetRequested = true;
    }

    public void Push(Vector3 origin, Vector3 direction)
    {
        interactionOrigin = origin;
        interactionDirection = Vector3.Normalize(direction);
        interactionStrength = 4.8f;
    }

    public TimelineValue Step(double totalTime, double deltaSeconds, bool paused)
    {
        const uint Substeps = 2;

        if (paused && !resetRequested)
        {
            return ready;
        }

        float frameTime = (float)deltaSeconds;
        float timeStep = paused ? 0.0f : frameTime / Substeps;

        SimulationConstants parameters = new()
        {
            TankMin = TankMin,
            TimeStep = MathF.Max(timeStep, 0.000001f),
            TankMax = TankMax,
            Time = (float)totalTime,
            GridSpacing = GridSpacing,
            InverseGridSpacing = 1.0f / GridSpacing,
            FlipRatio = FlipRatio,
            VelocityDamping = VelocityDamping,
            WaveAmplitude = WaveAmplitude,
            WaveFrequency = WaveFrequency,
            InteractionRadius = 1.15f,
            InteractionStrength = interactionStrength,
            InteractionOrigin = interactionOrigin,
            ParticleRadius = ParticleRadius,
            InteractionDirection = interactionDirection,
            RestDensity = RestDensity,
            ParticleCount = ParticleCount,
            CellCount = CellCount,
            GridPointCount = GridPointCount,
            WaveMakerEnabled = WaveMakerEnabled ? 1u : 0u,
            GridX = GridDimensions.X,
            GridY = GridDimensions.Y,
            GridZ = GridDimensions.Z,
            PressureIterations = (uint)PressureIterations,
            DamX = DamDimensions.X,
            DamY = DamDimensions.Y,
            DamZ = DamDimensions.Z,
            Substeps = Substeps,
            Particles = particles.StorageReadWriteHandle,
            PreviousPositions = previousPositions.StorageReadWriteHandle,
            ParticleAffine = particleAffine.StorageReadWriteHandle,
            GridAccumulation = gridAccumulation.StorageReadWriteHandle,
            GridVelocity = gridVelocity.StorageReadWriteHandle,
            GridVelocityOld = gridVelocityOld.StorageReadWriteHandle,
            CellTypes = cellTypes.StorageReadWriteHandle,
            Divergence = divergence.StorageReadWriteHandle,
            PressureA = pressureA.StorageReadWriteHandle
        };

        constantBuffer.Upload(0, new()
        {
            Pointer = (nint)(&parameters),
            SizeInBytes = (uint)sizeof(SimulationConstants)
        });

        CommandBuffer commandBuffer = App.Context.ComputeQueue.CommandBuffer();

        if (resetRequested)
        {
            Dispatch(commandBuffer, resetPipeline, ParticleCount);
            Dispatch(commandBuffer, initializeGridPipeline, CellCount);
            commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
            resetRequested = false;
        }

        if (!paused)
        {
            for (uint substep = 0; substep < Substeps; substep++)
            {
                Dispatch(commandBuffer, clearGridPipeline, Math.Max(GridPointCount * 6, CellCount));
                commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

                Dispatch(commandBuffer, substep is 0 ? beginParticleToGridPipeline : particleToGridPipeline, ParticleCount);
                commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

                Dispatch(commandBuffer, normalizeAndApplyForcesPipeline, GridPointCount);
                commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

                Dispatch(commandBuffer, divergencePipeline, CellCount);
                commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

                for (int iteration = 0; iteration < PressureIterations; iteration++)
                {
                    Dispatch(commandBuffer, pressureRedPipeline, pressureParityDispatchCount);
                    commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

                    Dispatch(commandBuffer, pressureBlackPipeline, pressureParityDispatchCount);
                    commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
                }

                Dispatch(commandBuffer, projectGridPipeline, GridPointCount);
                commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

                Dispatch(commandBuffer, gridToParticleAndAdvectPipeline, ParticleCount);
                commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
            }
        }

        if (!paused)
        {
            interactionStrength = 0.0f;
        }

        return ready = commandBuffer.Submit();
    }

    public void Dispose()
    {
        gridToParticleAndAdvectPipeline.Dispose();
        projectGridPipeline.Dispose();
        pressureBlackPipeline.Dispose();
        pressureRedPipeline.Dispose();
        divergencePipeline.Dispose();
        normalizeAndApplyForcesPipeline.Dispose();
        particleToGridPipeline.Dispose();
        beginParticleToGridPipeline.Dispose();
        clearGridPipeline.Dispose();
        initializeGridPipeline.Dispose();
        resetPipeline.Dispose();

        pressureA.Dispose();
        divergence.Dispose();
        cellTypes.Dispose();
        gridVelocityOld.Dispose();
        gridVelocity.Dispose();
        gridAccumulation.Dispose();
        particleAffine.Dispose();
        previousPositions.Dispose();
        particles.Dispose();
        constantBuffer.Dispose();
    }

    private static Buffer CreateStorageBuffer(uint count, uint stride, bool includeReadOnly)
    {
        return App.Context.CreateBuffer(new()
        {
            SizeInBytes = count * stride,
            StrideInBytes = stride,
            Usages = BufferUsages.StorageReadWrite | (includeReadOnly ? BufferUsages.StorageReadOnly : BufferUsages.None),
            Residency = MemoryResidency.GpuOnly
        });
    }

    private static ComputePipeline CreatePipeline(string shaderPath, string entryPoint)
    {
        using Shader shader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, entryPoint));

        return App.Context.CreateComputePipeline(new() { ComputeShader = shader });
    }

    private void Dispatch(CommandBuffer commandBuffer, ComputePipeline pipeline, uint count)
    {
        uint groupSize = pipeline.Desc.ComputeShader.Desc.ThreadGroupSize.X;

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(constantBuffer, 0);
        commandBuffer.Dispatch((count + groupSize - 1) / groupSize, 1, 1);
    }
}

[StructLayout(LayoutKind.Explicit, Size = 216)]
file struct SimulationConstants
{
    [FieldOffset(0)] public Vector3 TankMin;
    [FieldOffset(12)] public float TimeStep;
    [FieldOffset(16)] public Vector3 TankMax;
    [FieldOffset(28)] public float Time;
    [FieldOffset(32)] public float GridSpacing;
    [FieldOffset(36)] public float InverseGridSpacing;
    [FieldOffset(40)] public float FlipRatio;
    [FieldOffset(44)] public float VelocityDamping;
    [FieldOffset(48)] public float WaveAmplitude;
    [FieldOffset(52)] public float WaveFrequency;
    [FieldOffset(56)] public float InteractionRadius;
    [FieldOffset(60)] public float InteractionStrength;
    [FieldOffset(64)] public Vector3 InteractionOrigin;
    [FieldOffset(76)] public float ParticleRadius;
    [FieldOffset(80)] public Vector3 InteractionDirection;
    [FieldOffset(92)] public float RestDensity;
    [FieldOffset(96)] public uint ParticleCount;
    [FieldOffset(100)] public uint CellCount;
    [FieldOffset(104)] public uint GridPointCount;
    [FieldOffset(108)] public uint WaveMakerEnabled;
    [FieldOffset(112)] public uint GridX;
    [FieldOffset(116)] public uint GridY;
    [FieldOffset(120)] public uint GridZ;
    [FieldOffset(124)] public uint PressureIterations;
    [FieldOffset(128)] public uint DamX;
    [FieldOffset(132)] public uint DamY;
    [FieldOffset(136)] public uint DamZ;
    [FieldOffset(140)] public uint Substeps;
    [FieldOffset(144)] public ResourceHandle Particles;
    [FieldOffset(152)] public ResourceHandle PreviousPositions;
    [FieldOffset(160)] public ResourceHandle ParticleAffine;
    [FieldOffset(168)] public ResourceHandle GridAccumulation;
    [FieldOffset(176)] public ResourceHandle GridVelocity;
    [FieldOffset(184)] public ResourceHandle GridVelocityOld;
    [FieldOffset(192)] public ResourceHandle CellTypes;
    [FieldOffset(200)] public ResourceHandle Divergence;
    [FieldOffset(208)] public ResourceHandle PressureA;
}