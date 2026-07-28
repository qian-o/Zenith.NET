using System.Numerics;
using System.Runtime.InteropServices;
using FluidTank.Helpers;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank;

internal unsafe class FluidSimulation : IDisposable
{
    private static readonly Vector3 TankMin = new(-6.0f, 0.0f, -3.0f);
    private static readonly Vector3 TankMax = new(6.0f, 5.2f, 3.0f);
    private static readonly (uint X, uint Y, uint Z) DamDimensions = (40, 48, 59);
    private const float GridSpacing = 6.0f / 33.0f;

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
    private readonly uint pressureParityDispatchCount;

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

        constantBuffer = GraphicsHelper.CreateConstantBuffer<SimulationConstants>();

        particles = GraphicsHelper.CreateBuffer(ParticleCount, 32, BufferUsages.StorageReadOnly | BufferUsages.StorageReadWrite);
        previousPositions = GraphicsHelper.CreateBuffer(ParticleCount, 16, BufferUsages.StorageReadOnly | BufferUsages.StorageReadWrite);
        particleAffine = GraphicsHelper.CreateBuffer(ParticleCount * 3, 16, BufferUsages.StorageReadWrite);
        gridAccumulation = GraphicsHelper.CreateBuffer(GridPointCount * 6, sizeof(int), BufferUsages.StorageReadWrite);
        gridVelocity = GraphicsHelper.CreateBuffer(GridPointCount, 16, BufferUsages.StorageReadWrite);
        gridVelocityOld = GraphicsHelper.CreateBuffer(GridPointCount, 16, BufferUsages.StorageReadWrite);
        cellTypes = GraphicsHelper.CreateBuffer(CellCount, sizeof(uint), BufferUsages.StorageReadWrite);
        divergence = GraphicsHelper.CreateBuffer(CellCount, sizeof(float), BufferUsages.StorageReadWrite);
        pressureA = GraphicsHelper.CreateBuffer(CellCount, sizeof(float), BufferUsages.StorageReadWrite);

        resetPipeline = GraphicsHelper.CreateComputePipeline("FluidSimulation.slang", "ResetCS");
        initializeGridPipeline = GraphicsHelper.CreateComputePipeline("FluidSimulation.slang", "InitializeGridCS");
        clearGridPipeline = GraphicsHelper.CreateComputePipeline("FluidSimulation.slang", "ClearGridCS");
        beginParticleToGridPipeline = GraphicsHelper.CreateComputePipeline("FluidSimulation.slang", "BeginParticleToGridCS");
        particleToGridPipeline = GraphicsHelper.CreateComputePipeline("FluidSimulation.slang", "ParticleToGridCS");
        normalizeAndApplyForcesPipeline = GraphicsHelper.CreateComputePipeline("FluidSimulation.slang", "NormalizeAndApplyForcesCS");
        divergencePipeline = GraphicsHelper.CreateComputePipeline("FluidSimulation.slang", "DivergenceCS");
        pressureRedPipeline = GraphicsHelper.CreateComputePipeline("FluidSimulation.slang", "PressureRedCS");
        pressureBlackPipeline = GraphicsHelper.CreateComputePipeline("FluidSimulation.slang", "PressureBlackCS");
        projectGridPipeline = GraphicsHelper.CreateComputePipeline("FluidSimulation.slang", "ProjectGridCS");
        gridToParticleAndAdvectPipeline = GraphicsHelper.CreateComputePipeline("FluidSimulation.slang", "GridToParticleAndAdvectCS");
    }

    public uint ParticleCount { get; }

    public uint CellCount { get; }

    public uint GridPointCount { get; }

    public (uint X, uint Y, uint Z) GridDimensions { get; }

    public ResourceHandle ParticleHandle => particles.StorageReadOnlyHandle;

    public ResourceHandle PreviousPositionHandle => previousPositions.StorageReadOnlyHandle;

    public const float ParticleRadius = GridSpacing * 0.316f;

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
    } = 18;

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
        const uint substeps = 2;

        if (paused && !resetRequested)
        {
            return ready;
        }

        float frameTime = (float)deltaSeconds;
        float timeStep = paused ? 0.0f : frameTime / substeps;

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
            Substeps = substeps,
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
            for (uint substep = 0; substep < substeps; substep++)
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
    [FieldOffset(0)]
    public Vector3 TankMin;

    [FieldOffset(12)]
    public float TimeStep;

    [FieldOffset(16)]
    public Vector3 TankMax;

    [FieldOffset(28)]
    public float Time;

    [FieldOffset(32)]
    public float GridSpacing;

    [FieldOffset(36)]
    public float InverseGridSpacing;

    [FieldOffset(40)]
    public float FlipRatio;

    [FieldOffset(44)]
    public float VelocityDamping;

    [FieldOffset(48)]
    public float WaveAmplitude;

    [FieldOffset(52)]
    public float WaveFrequency;

    [FieldOffset(56)]
    public float InteractionRadius;

    [FieldOffset(60)]
    public float InteractionStrength;

    [FieldOffset(64)]
    public Vector3 InteractionOrigin;

    [FieldOffset(76)]
    public float ParticleRadius;

    [FieldOffset(80)]
    public Vector3 InteractionDirection;

    [FieldOffset(92)]
    public float RestDensity;

    [FieldOffset(96)]
    public uint ParticleCount;

    [FieldOffset(100)]
    public uint CellCount;

    [FieldOffset(104)]
    public uint GridPointCount;

    [FieldOffset(108)]
    public uint WaveMakerEnabled;

    [FieldOffset(112)]
    public uint GridX;

    [FieldOffset(116)]
    public uint GridY;

    [FieldOffset(120)]
    public uint GridZ;

    [FieldOffset(124)]
    public uint PressureIterations;

    [FieldOffset(128)]
    public uint DamX;

    [FieldOffset(132)]
    public uint DamY;

    [FieldOffset(136)]
    public uint DamZ;

    [FieldOffset(140)]
    public uint Substeps;

    [FieldOffset(144)]
    public ResourceHandle Particles;

    [FieldOffset(152)]
    public ResourceHandle PreviousPositions;

    [FieldOffset(160)]
    public ResourceHandle ParticleAffine;

    [FieldOffset(168)]
    public ResourceHandle GridAccumulation;

    [FieldOffset(176)]
    public ResourceHandle GridVelocity;

    [FieldOffset(184)]
    public ResourceHandle GridVelocityOld;

    [FieldOffset(192)]
    public ResourceHandle CellTypes;

    [FieldOffset(200)]
    public ResourceHandle Divergence;

    [FieldOffset(208)]
    public ResourceHandle PressureA;
}
