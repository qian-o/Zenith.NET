using System.Numerics;
using System.Runtime.InteropServices;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank;

internal unsafe class Simulation : DisposableObject
{
    public const float GridSpacing = 6.0f / 33.0f;
    public const float ParticleRadius = GridSpacing * 0.316f;
    public const float RestDensity = 5.52f;
    public const float ParticleSpacing = ParticleRadius * 1.67f;
    public const float FlipRatio = 0.97f;
    public const float VelocityDamping = 0.9998f;
    public const uint Substeps = 2;
    public const uint PressureIterations = 18;

    public static readonly (uint X, uint Y, uint Z) DamDimensions = (40, 48, 59);
    public static readonly uint ParticleCount = DamDimensions.X * DamDimensions.Y * DamDimensions.Z;
    public static readonly Vector3 TankMin = new(-6.0f, 0.0f, -3.0f);
    public static readonly Vector3 TankMax = new(6.0f, 5.2f, 3.0f);
    public static readonly (uint X, uint Y, uint Z) GridDimensions = ((uint)MathF.Ceiling((TankMax.X - TankMin.X) / GridSpacing), (uint)MathF.Ceiling((TankMax.Y - TankMin.Y) / GridSpacing), (uint)MathF.Ceiling((TankMax.Z - TankMin.Z) / GridSpacing));
    public static readonly uint CellCount = GridDimensions.X * GridDimensions.Y * GridDimensions.Z;
    public static readonly uint GridPointCount = (GridDimensions.X + 1) * (GridDimensions.Y + 1) * (GridDimensions.Z + 1);

    private readonly Buffer constantBuffer;
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
    private TimelineValue ready;

    public Simulation()
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(SimulationConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
        particleAffine = CreateBuffer(ParticleCount * 3, 16, BufferUsages.StorageReadWrite);
        gridAccumulation = CreateBuffer(GridPointCount * 6, sizeof(int), BufferUsages.StorageReadWrite);
        gridVelocity = CreateBuffer(GridPointCount, 16, BufferUsages.StorageReadWrite);
        gridVelocityOld = CreateBuffer(GridPointCount, 16, BufferUsages.StorageReadWrite);
        cellTypes = CreateBuffer(CellCount, sizeof(uint), BufferUsages.StorageReadWrite);
        divergence = CreateBuffer(CellCount, sizeof(float), BufferUsages.StorageReadWrite);
        pressureA = CreateBuffer(CellCount, sizeof(float), BufferUsages.StorageReadWrite);

        resetPipeline = CreatePipeline("ResetCS");
        initializeGridPipeline = CreatePipeline("InitializeGridCS");
        clearGridPipeline = CreatePipeline("ClearGridCS");
        beginParticleToGridPipeline = CreatePipeline("BeginParticleToGridCS");
        particleToGridPipeline = CreatePipeline("ParticleToGridCS");
        normalizeAndApplyForcesPipeline = CreatePipeline("NormalizeAndApplyForcesCS");
        divergencePipeline = CreatePipeline("DivergenceCS");
        pressureRedPipeline = CreatePipeline("PressureRedCS");
        pressureBlackPipeline = CreatePipeline("PressureBlackCS");
        projectGridPipeline = CreatePipeline("ProjectGridCS");
        gridToParticleAndAdvectPipeline = CreatePipeline("GridToParticleAndAdvectCS");

        Particles = CreateBuffer(ParticleCount, 32, BufferUsages.StorageReadOnly | BufferUsages.StorageReadWrite);
        PreviousPositions = CreateBuffer(ParticleCount, 16, BufferUsages.StorageReadOnly | BufferUsages.StorageReadWrite);
    }

    public Buffer Particles { get; }

    public Buffer PreviousPositions { get; }

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

    public TimelineValue Step(double deltaSeconds, bool paused)
    {
        if (paused && !resetRequested)
        {
            return ready;
        }

        float timeStep = paused ? 0.0f : (float)deltaSeconds / Substeps;

        SimulationConstants constants = new()
        {
            TankMin = TankMin,
            TimeStep = MathF.Max(timeStep, 0.000001f),
            TankMax = TankMax,
            GridSpacing = GridSpacing,
            InverseGridSpacing = 1.0f / GridSpacing,
            FlipRatio = FlipRatio,
            VelocityDamping = VelocityDamping,
            InteractionRadius = 1.15f,
            InteractionStrength = interactionStrength,
            InteractionOrigin = interactionOrigin,
            ParticleRadius = ParticleRadius,
            InteractionDirection = interactionDirection,
            RestDensity = RestDensity,
            ParticleCount = ParticleCount,
            CellCount = CellCount,
            GridPointCount = GridPointCount,
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
        };

        constantBuffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
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

    protected override void Destroy()
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

    private static Buffer CreateBuffer(uint count, uint strideInBytes, BufferUsages usages)
    {
        return App.Context.CreateBuffer(new()
        {
            SizeInBytes = count * strideInBytes,
            StrideInBytes = strideInBytes,
            Usages = usages,
            Residency = MemoryResidency.GpuOnly
        });
    }

    private static ComputePipeline CreatePipeline(string entryPoint)
    {
        using Shader shader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "FluidSimulation.slang"), entryPoint));

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

[StructLayout(LayoutKind.Explicit, Size = 200)]
file struct SimulationConstants
{
    [FieldOffset(0)]
    public Vector3 TankMin;

    [FieldOffset(12)]
    public float TimeStep;

    [FieldOffset(16)]
    public Vector3 TankMax;

    [FieldOffset(28)]
    public float InteractionRadius;

    [FieldOffset(32)]
    public float GridSpacing;

    [FieldOffset(36)]
    public float InverseGridSpacing;

    [FieldOffset(40)]
    public float FlipRatio;

    [FieldOffset(44)]
    public float VelocityDamping;

    [FieldOffset(48)]
    public Vector3 InteractionOrigin;

    [FieldOffset(60)]
    public float ParticleRadius;

    [FieldOffset(64)]
    public Vector3 InteractionDirection;

    [FieldOffset(76)]
    public float RestDensity;

    [FieldOffset(80)]
    public uint ParticleCount;

    [FieldOffset(84)]
    public uint CellCount;

    [FieldOffset(88)]
    public uint GridPointCount;

    [FieldOffset(92)]
    public float InteractionStrength;

    [FieldOffset(96)]
    public uint GridX;

    [FieldOffset(100)]
    public uint GridY;

    [FieldOffset(104)]
    public uint GridZ;

    [FieldOffset(108)]
    public uint PressureIterations;

    [FieldOffset(112)]
    public uint DamX;

    [FieldOffset(116)]
    public uint DamY;

    [FieldOffset(120)]
    public uint DamZ;

    [FieldOffset(124)]
    public uint Substeps;

    [FieldOffset(128)]
    public ResourceHandle Particles;

    [FieldOffset(136)]
    public ResourceHandle PreviousPositions;

    [FieldOffset(144)]
    public ResourceHandle ParticleAffine;

    [FieldOffset(152)]
    public ResourceHandle GridAccumulation;

    [FieldOffset(160)]
    public ResourceHandle GridVelocity;

    [FieldOffset(168)]
    public ResourceHandle GridVelocityOld;

    [FieldOffset(176)]
    public ResourceHandle CellTypes;

    [FieldOffset(184)]
    public ResourceHandle Divergence;

    [FieldOffset(192)]
    public ResourceHandle PressureA;
}
