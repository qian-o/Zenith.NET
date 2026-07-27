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

    private readonly Buffer constantBuffer;
    private readonly Buffer particles;
    private readonly Buffer previousPositions;
    private readonly Buffer deltaPositions;
    private readonly Buffer gridHeads;
    private readonly Buffer particleNext;
    private readonly Buffer newVelocities;

    private readonly ComputePipeline resetPipeline;
    private readonly ComputePipeline clearGridPipeline;
    private readonly ComputePipeline predictPipeline;
    private readonly ComputePipeline buildGridPipeline;
    private readonly ComputePipeline densityPipeline;
    private readonly ComputePipeline deltaPipeline;
    private readonly ComputePipeline applyDeltaPipeline;
    private readonly ComputePipeline velocityPipeline;
    private readonly ComputePipeline commitVelocityPipeline;

    private bool resetRequested = true;
    private Vector3 interactionOrigin;
    private Vector3 interactionDirection;
    private float interactionStrength;
    private TimelineValue ready;

    public FluidSimulation()
    {
        ParticleCount = DamDimensions.X * DamDimensions.Y * DamDimensions.Z;

        Vector3 tankExtent = TankMax - TankMin;
        GridDimensions = new((uint)MathF.Ceiling(tankExtent.X / SmoothingRadius),
                             (uint)MathF.Ceiling(tankExtent.Y / SmoothingRadius),
                             (uint)MathF.Ceiling(tankExtent.Z / SmoothingRadius));
        CellCount = GridDimensions.X * GridDimensions.Y * GridDimensions.Z;

        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(SimulationConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        particles = CreateStorageBuffer(ParticleCount, 32, includeReadOnly: true);
        previousPositions = CreateStorageBuffer(ParticleCount, 16, includeReadOnly: false);
        deltaPositions = CreateStorageBuffer(ParticleCount, 16, includeReadOnly: false);
        gridHeads = CreateStorageBuffer(CellCount, sizeof(int), includeReadOnly: false);
        particleNext = CreateStorageBuffer(ParticleCount, sizeof(int), includeReadOnly: false);
        newVelocities = CreateStorageBuffer(ParticleCount, 16, includeReadOnly: false);

        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "FluidSimulation.slang");

        resetPipeline = CreatePipeline(shaderPath, "ResetCS");
        clearGridPipeline = CreatePipeline(shaderPath, "ClearGridCS");
        predictPipeline = CreatePipeline(shaderPath, "PredictCS");
        buildGridPipeline = CreatePipeline(shaderPath, "BuildGridCS");
        densityPipeline = CreatePipeline(shaderPath, "DensityCS");
        deltaPipeline = CreatePipeline(shaderPath, "DeltaCS");
        applyDeltaPipeline = CreatePipeline(shaderPath, "ApplyDeltaCS");
        velocityPipeline = CreatePipeline(shaderPath, "VelocityCS");
        commitVelocityPipeline = CreatePipeline(shaderPath, "CommitVelocityCS");
    }

    public uint ParticleCount { get; }

    public uint CellCount { get; }

    public (uint X, uint Y, uint Z) GridDimensions { get; }

    public ResourceHandle ParticleHandle => particles.StorageReadOnlyHandle;

    public const float ParticleRadius = 0.0632f;

    public const float SmoothingRadius = 0.2197f;

    public const float RestDensity = 5.52f;

    public float Viscosity { get; set; } = 0.085f;

    public float SurfaceTension { get; set; } = 0.012f;

    public float WaveAmplitude { get; set; } = 0.12f;

    public float WaveFrequency { get; set; } = 0.58f;

    public bool WaveMakerEnabled { get; set; }

    public int SolverIterations { get; set; } = 3;

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
        const uint Substeps = 1;

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
            ParticleRadius = ParticleRadius,
            SmoothingRadius = SmoothingRadius,
            RestDensity = RestDensity,
            Relaxation = 0.018f,
            Viscosity = Viscosity,
            SurfaceTension = SurfaceTension,
            WaveAmplitude = WaveAmplitude,
            WaveFrequency = WaveFrequency,
            InteractionOrigin = interactionOrigin,
            InteractionRadius = 1.15f,
            InteractionDirection = interactionDirection,
            InteractionStrength = interactionStrength,
            ParticleCount = ParticleCount,
            CellCount = CellCount,
            WaveMakerEnabled = WaveMakerEnabled ? 1u : 0u,
            GridX = GridDimensions.X,
            GridY = GridDimensions.Y,
            GridZ = GridDimensions.Z,
            SolverIterations = (uint)SolverIterations,
            DamX = DamDimensions.X,
            DamY = DamDimensions.Y,
            DamZ = DamDimensions.Z,
            Substeps = Substeps,
            Particles = particles.StorageReadWriteHandle,
            PreviousPositions = previousPositions.StorageReadWriteHandle,
            DeltaPositions = deltaPositions.StorageReadWriteHandle,
            GridHeads = gridHeads.StorageReadWriteHandle,
            ParticleNext = particleNext.StorageReadWriteHandle,
            NewVelocities = newVelocities.StorageReadWriteHandle
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
            commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
            resetRequested = false;
        }

        if (!paused)
        {
            for (uint substep = 0; substep < Substeps; substep++)
            {
                Dispatch(commandBuffer, predictPipeline, ParticleCount);
                commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

                Dispatch(commandBuffer, clearGridPipeline, CellCount);
                commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

                Dispatch(commandBuffer, buildGridPipeline, ParticleCount);
                commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

                for (int iteration = 0; iteration < SolverIterations; iteration++)
                {
                    Dispatch(commandBuffer, densityPipeline, ParticleCount);
                    commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

                    Dispatch(commandBuffer, deltaPipeline, ParticleCount);
                    commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

                    Dispatch(commandBuffer, applyDeltaPipeline, ParticleCount);
                    commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
                }

                Dispatch(commandBuffer, velocityPipeline, ParticleCount);
                commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

                Dispatch(commandBuffer, commitVelocityPipeline, ParticleCount);
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
        commitVelocityPipeline.Dispose();
        velocityPipeline.Dispose();
        applyDeltaPipeline.Dispose();
        deltaPipeline.Dispose();
        densityPipeline.Dispose();
        buildGridPipeline.Dispose();
        predictPipeline.Dispose();
        clearGridPipeline.Dispose();
        resetPipeline.Dispose();

        newVelocities.Dispose();
        particleNext.Dispose();
        gridHeads.Dispose();
        deltaPositions.Dispose();
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

[StructLayout(LayoutKind.Explicit, Size = 192)]
file struct SimulationConstants
{
    [FieldOffset(0)] public Vector3 TankMin;
    [FieldOffset(12)] public float TimeStep;
    [FieldOffset(16)] public Vector3 TankMax;
    [FieldOffset(28)] public float Time;
    [FieldOffset(32)] public float ParticleRadius;
    [FieldOffset(36)] public float SmoothingRadius;
    [FieldOffset(40)] public float RestDensity;
    [FieldOffset(44)] public float Relaxation;
    [FieldOffset(48)] public float Viscosity;
    [FieldOffset(52)] public float SurfaceTension;
    [FieldOffset(56)] public float WaveAmplitude;
    [FieldOffset(60)] public float WaveFrequency;
    [FieldOffset(64)] public Vector3 InteractionOrigin;
    [FieldOffset(76)] public float InteractionRadius;
    [FieldOffset(80)] public Vector3 InteractionDirection;
    [FieldOffset(92)] public float InteractionStrength;
    [FieldOffset(96)] public uint ParticleCount;
    [FieldOffset(100)] public uint CellCount;
    [FieldOffset(104)] public uint WaveMakerEnabled;
    [FieldOffset(112)] public uint GridX;
    [FieldOffset(116)] public uint GridY;
    [FieldOffset(120)] public uint GridZ;
    [FieldOffset(124)] public uint SolverIterations;
    [FieldOffset(128)] public uint DamX;
    [FieldOffset(132)] public uint DamY;
    [FieldOffset(136)] public uint DamZ;
    [FieldOffset(140)] public uint Substeps;
    [FieldOffset(144)] public ResourceHandle Particles;
    [FieldOffset(152)] public ResourceHandle PreviousPositions;
    [FieldOffset(160)] public ResourceHandle DeltaPositions;
    [FieldOffset(168)] public ResourceHandle GridHeads;
    [FieldOffset(176)] public ResourceHandle ParticleNext;
    [FieldOffset(184)] public ResourceHandle NewVelocities;
}