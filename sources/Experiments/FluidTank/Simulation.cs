using System.Numerics;
using System.Runtime.InteropServices;
using FluidTank.Helpers;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank;

internal class Simulation(GraphicsContext context) : IDisposable
{
    public const float GridSpacing = 6.0f / 33.0f;

    public const float ParticleRadius = GridSpacing * 0.316f;

    public const float RestDensity = 5.52f;

    public const float ParticleSpacing = ParticleRadius * 1.67f;

    public const float FlipRatio = 0.97f;

    public const float VelocityDamping = 0.9998f;

    public const float WaveAmplitude = 0.12f;

    public const float WaveFrequency = 0.58f;

    public const uint Substeps = 2;

    public const uint PressureIterations = 18;

    public static readonly (uint X, uint Y, uint Z) DamDimensions = (40, 48, 59);

    public static readonly uint ParticleCount = DamDimensions.X * DamDimensions.Y * DamDimensions.Z;

    public static readonly Vector3 TankMin = new(-6.0f, 0.0f, -3.0f);

    public static readonly Vector3 TankMax = new(6.0f, 5.2f, 3.0f);

    public static readonly (uint X, uint Y, uint Z) GridDimensions = ((uint)MathF.Ceiling((TankMax.X - TankMin.X) / GridSpacing), (uint)MathF.Ceiling((TankMax.Y - TankMin.Y) / GridSpacing), (uint)MathF.Ceiling((TankMax.Z - TankMin.Z) / GridSpacing));

    public static readonly uint CellCount = GridDimensions.X * GridDimensions.Y * GridDimensions.Z;

    public static readonly uint GridPointCount = (GridDimensions.X + 1) * (GridDimensions.Y + 1) * (GridDimensions.Z + 1);

    public Buffer Particles = GraphicsHelper.CreateBuffer(context, ParticleCount, 32, BufferUsages.StorageReadOnly | BufferUsages.StorageReadWrite);

    public Buffer PreviousPositions = GraphicsHelper.CreateBuffer(context, ParticleCount, 16, BufferUsages.StorageReadOnly | BufferUsages.StorageReadWrite);

    private readonly Buffer constantBuffer = GraphicsHelper.CreateConstantBuffer<SimulationConstants>(context);

    private readonly Buffer particleAffine = GraphicsHelper.CreateBuffer(context, ParticleCount * 3, 16, BufferUsages.StorageReadWrite);

    private readonly Buffer gridAccumulation = GraphicsHelper.CreateBuffer(context, GridPointCount * 6, sizeof(int), BufferUsages.StorageReadWrite);

    private readonly Buffer gridVelocity = GraphicsHelper.CreateBuffer(context, GridPointCount, 16, BufferUsages.StorageReadWrite);

    private readonly Buffer gridVelocityOld = GraphicsHelper.CreateBuffer(context, GridPointCount, 16, BufferUsages.StorageReadWrite);

    private readonly Buffer cellTypes = GraphicsHelper.CreateBuffer(context, CellCount, sizeof(uint), BufferUsages.StorageReadWrite);

    private readonly Buffer divergence = GraphicsHelper.CreateBuffer(context, CellCount, sizeof(float), BufferUsages.StorageReadWrite);

    private readonly Buffer pressureA = GraphicsHelper.CreateBuffer(context, CellCount, sizeof(float), BufferUsages.StorageReadWrite);

    private readonly ComputePipeline resetPipeline = GraphicsHelper.CreateComputePipeline(context, "FluidSimulation.slang", "ResetCS");

    private readonly ComputePipeline initializeGridPipeline = GraphicsHelper.CreateComputePipeline(context, "FluidSimulation.slang", "InitializeGridCS");

    private readonly ComputePipeline clearGridPipeline = GraphicsHelper.CreateComputePipeline(context, "FluidSimulation.slang", "ClearGridCS");

    private readonly ComputePipeline beginParticleToGridPipeline = GraphicsHelper.CreateComputePipeline(context, "FluidSimulation.slang", "BeginParticleToGridCS");

    private readonly ComputePipeline particleToGridPipeline = GraphicsHelper.CreateComputePipeline(context, "FluidSimulation.slang", "ParticleToGridCS");

    private readonly ComputePipeline normalizeAndApplyForcesPipeline = GraphicsHelper.CreateComputePipeline(context, "FluidSimulation.slang", "NormalizeAndApplyForcesCS");

    private readonly ComputePipeline divergencePipeline = GraphicsHelper.CreateComputePipeline(context, "FluidSimulation.slang", "DivergenceCS");

    private readonly ComputePipeline pressureRedPipeline = GraphicsHelper.CreateComputePipeline(context, "FluidSimulation.slang", "PressureRedCS");

    private readonly ComputePipeline pressureBlackPipeline = GraphicsHelper.CreateComputePipeline(context, "FluidSimulation.slang", "PressureBlackCS");

    private readonly ComputePipeline projectGridPipeline = GraphicsHelper.CreateComputePipeline(context, "FluidSimulation.slang", "ProjectGridCS");

    private readonly ComputePipeline gridToParticleAndAdvectPipeline = GraphicsHelper.CreateComputePipeline(context, "FluidSimulation.slang", "GridToParticleAndAdvectCS");

    private bool resetRequested = true;

    private Vector3 interactionOrigin;

    private Vector3 interactionDirection;

    private float interactionStrength;

    private TimelineValue ready;

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

    public TimelineValue Step(double totalTime, double deltaSeconds, bool paused, bool waveMakerEnabled)
    {
        if (paused && !resetRequested)
        {
            return ready;
        }

        float timeStep = paused ? 0.0f : (float)deltaSeconds / Substeps;

        GraphicsHelper.Upload(constantBuffer, 0, new SimulationConstants()
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
            WaveMakerEnabled = waveMakerEnabled ? 1u : 0u,
            GridX = GridDimensions.X,
            GridY = GridDimensions.Y,
            GridZ = GridDimensions.Z,
            PressureIterations = PressureIterations,
            DamX = DamDimensions.X,
            DamY = DamDimensions.Y,
            DamZ = DamDimensions.Z,
            Substeps = Substeps,
            Particles = Particles.StorageReadWriteHandle,
            PreviousPositions = PreviousPositions.StorageReadWriteHandle,
            ParticleAffine = particleAffine.StorageReadWriteHandle,
            GridAccumulation = gridAccumulation.StorageReadWriteHandle,
            GridVelocity = gridVelocity.StorageReadWriteHandle,
            GridVelocityOld = gridVelocityOld.StorageReadWriteHandle,
            CellTypes = cellTypes.StorageReadWriteHandle,
            Divergence = divergence.StorageReadWriteHandle,
            PressureA = pressureA.StorageReadWriteHandle
        });

        CommandBuffer commandBuffer = context.ComputeQueue.CommandBuffer();

        if (resetRequested)
        {
            Dispatch(commandBuffer, resetPipeline, ParticleCount);
            Dispatch(commandBuffer, initializeGridPipeline, CellCount);
            commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
            resetRequested = false;
        }

        if (!paused)
        {
            uint pressureParityDispatchCount = (GridDimensions.X + 1) / 2 * GridDimensions.Y * GridDimensions.Z;

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

                for (uint iteration = 0; iteration < PressureIterations; iteration++)
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
        PreviousPositions.Dispose();
        Particles.Dispose();
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
