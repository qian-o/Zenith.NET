using System.Numerics;
using System.Runtime.InteropServices;
using CornellBox.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class ShadowPass : DisposableObject
{
    private const int ConstantSlotCount = 4;
    private const uint FlagResetAll = 1 << 0;
    private const uint FlagResetLight = 1 << 1;

    private readonly Buffer constantBuffer;
    private readonly ComputePipeline tracePipeline;
    private readonly ComputePipeline filterPipeline;

    private Texture filterScratch = null!;
    private Texture previousHistory = null!;
    private Texture currentHistory = null!;
    private Texture previousHistoryLength = null!;
    private Texture currentHistoryLength = null!;
    private Texture previousLinearDepth = null!;
    private Texture currentLinearDepth = null!;
    private Vector2 previousJitterOffset;
    private Vector3 previousCameraPosition;

    public ShadowPass()
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(ShadowConstants) * ConstantSlotCount),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "Shadow.slang");

        using Shader traceShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "TraceShadowCS"));
        using Shader filterShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "FilterShadowCS"));

        tracePipeline = App.Context.CreateComputePipeline(new() { ComputeShader = traceShader });
        filterPipeline = App.Context.CreateComputePipeline(new() { ComputeShader = filterShader });
    }

    public uint Width { get; private set; }

    public uint Height { get; private set; }

    public Texture ShadowAO { get; private set; } = null!;

    public Texture PreviousLinearDepth => currentLinearDepth;

    public Texture HistoryLength => previousHistoryLength;

    public void Record(CommandBuffer commandBuffer, SceneResources scene, GBufferPass gBuffer, in FrameContext frame)
    {
        Vector2 effectivePreviousJitter = frame.Reset ? frame.JitterOffset : previousJitterOffset;
        Vector2 jitterDelta = (frame.JitterOffset - effectivePreviousJitter) / new Vector2(Width, Height);
        Vector3 effectivePreviousCameraPosition = frame.Reset ? frame.CameraPosition : previousCameraPosition;

        uint resetFlags = (frame.Reset ? FlagResetAll : 0) | (frame.ResetLighting ? FlagResetLight : 0);

        uint strideInBytes = (uint)sizeof(ShadowConstants);
        ShadowConstants* slots = stackalloc ShadowConstants[ConstantSlotCount];

        ShadowConstants baseConstants = new()
        {
            InverseJitteredViewProjection = frame.InverseJitteredViewProjection,
            CameraPosition = frame.CameraPosition,
            FrameIndex = frame.FrameIndex,
            PreviousCameraPosition = effectivePreviousCameraPosition,
            Width = Width,
            Height = Height,
            Reset = resetFlags,
            JitterDelta = jitterDelta,
            NightLightIntensity = frame.Environment.NightLightIntensity,
            KeyLightDirection = frame.Environment.KeyLightDirection,
            SoftKeyCosRadius = frame.Environment.SoftKeyCosRadius,
            Scene = scene.AccelerationStructure,
            Depth = gBuffer.Depth.SampledHandle,
            NormalAndRoughness = gBuffer.NormalAndRoughness.SampledHandle,
            MotionVectors = gBuffer.MotionVectors.SampledHandle,
            PreviousLinearDepth = frame.Reset ? default : previousLinearDepth.SampledHandle,
            CurrentLinearDepth = currentLinearDepth.StorageHandle,
            PreviousHistory = frame.Reset ? default : previousHistory.SampledHandle,
            CurrentHistory = currentHistory.StorageHandle,
            PreviousHistoryLength = frame.Reset ? default : previousHistoryLength.SampledHandle,
            CurrentHistoryLength = currentHistoryLength.StorageHandle
        };

        slots[0] = baseConstants with { StepSize = 0 };
        slots[1] = baseConstants with
        {
            StepSize = 4,
            PreviousLinearDepth = currentLinearDepth.SampledHandle,
            FilterInput = currentHistory.SampledHandle,
            FilterOutput = ShadowAO.StorageHandle,
            PreviousHistoryLength = currentHistoryLength.SampledHandle
        };
        slots[2] = baseConstants with
        {
            StepSize = 2,
            PreviousLinearDepth = currentLinearDepth.SampledHandle,
            FilterInput = ShadowAO.SampledHandle,
            FilterOutput = filterScratch.StorageHandle,
            PreviousHistoryLength = currentHistoryLength.SampledHandle
        };
        slots[3] = baseConstants with
        {
            StepSize = 1,
            PreviousLinearDepth = currentLinearDepth.SampledHandle,
            FilterInput = filterScratch.SampledHandle,
            FilterOutput = ShadowAO.StorageHandle,
            PreviousHistoryLength = currentHistoryLength.SampledHandle
        };

        constantBuffer.Upload(0, new()
        {
            Pointer = (nint)slots,
            SizeInBytes = strideInBytes * ConstantSlotCount
        });

        commandBuffer.Transition(currentLinearDepth, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.Transition(currentHistory, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.Transition(currentHistoryLength, default, TextureLayout.Undefined, TextureLayout.Storage);
        Dispatch(commandBuffer, tracePipeline, 0);

        commandBuffer.Transition(currentLinearDepth, default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Transition(currentHistory, default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Transition(currentHistoryLength, default, TextureLayout.Storage, TextureLayout.Sampled);

        commandBuffer.Transition(ShadowAO, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
        Dispatch(commandBuffer, filterPipeline, strideInBytes);
        commandBuffer.Transition(ShadowAO, default, TextureLayout.Storage, TextureLayout.Sampled);

        commandBuffer.Transition(filterScratch, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
        Dispatch(commandBuffer, filterPipeline, strideInBytes * 2);
        commandBuffer.Transition(filterScratch, default, TextureLayout.Storage, TextureLayout.Sampled);

        commandBuffer.Transition(ShadowAO, default, TextureLayout.Sampled, TextureLayout.Storage);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
        Dispatch(commandBuffer, filterPipeline, strideInBytes * 3);
        commandBuffer.Transition(ShadowAO, default, TextureLayout.Storage, TextureLayout.Sampled);

        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

        (previousHistory, currentHistory) = (currentHistory, previousHistory);
        (previousHistoryLength, currentHistoryLength) = (currentHistoryLength, previousHistoryLength);
        (previousLinearDepth, currentLinearDepth) = (currentLinearDepth, previousLinearDepth);
        previousJitterOffset = frame.JitterOffset;
        previousCameraPosition = frame.CameraPosition;
    }

    public void Resize(uint width, uint height)
    {
        if (Width == width && Height == height)
        {
            return;
        }

        DestroyResources();
        CreateResources(width, height);
    }

    protected override void Destroy()
    {
        DestroyResources();

        filterPipeline?.Dispose();
        tracePipeline?.Dispose();
        constantBuffer?.Dispose();
    }

    private void Dispatch(CommandBuffer commandBuffer, ComputePipeline pipeline, uint offsetInBytes)
    {
        ThreadGroup group = pipeline.Desc.ComputeShader.Desc.ThreadGroup;

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(constantBuffer, offsetInBytes);
        commandBuffer.Dispatch((Width + group.X - 1) / group.X, (Height + group.Y - 1) / group.Y, 1);
    }

    private void CreateResources(uint width, uint height)
    {
        Width = width;
        Height = height;

        ShadowAO = CreateComputeTexture(PixelFormat.R16G16B16A16Float, width, height);
        filterScratch = CreateComputeTexture(PixelFormat.R16G16B16A16Float, width, height);
        previousHistory = CreateComputeTexture(PixelFormat.R16G16B16A16Float, width, height);
        currentHistory = CreateComputeTexture(PixelFormat.R16G16B16A16Float, width, height);
        previousHistoryLength = CreateComputeTexture(PixelFormat.R16G16Float, width, height);
        currentHistoryLength = CreateComputeTexture(PixelFormat.R16G16Float, width, height);
        previousLinearDepth = CreateComputeTexture(PixelFormat.R32Float, width, height);
        currentLinearDepth = CreateComputeTexture(PixelFormat.R32Float, width, height);
    }

    private void DestroyResources()
    {
        currentLinearDepth?.Dispose();
        currentLinearDepth = null!;
        previousLinearDepth?.Dispose();
        previousLinearDepth = null!;
        currentHistoryLength?.Dispose();
        currentHistoryLength = null!;
        previousHistoryLength?.Dispose();
        previousHistoryLength = null!;
        currentHistory?.Dispose();
        currentHistory = null!;
        previousHistory?.Dispose();
        previousHistory = null!;
        filterScratch?.Dispose();
        filterScratch = null!;
        ShadowAO?.Dispose();
        ShadowAO = null!;

        Width = 0;
        Height = 0;
        previousJitterOffset = Vector2.Zero;
        previousCameraPosition = Vector3.Zero;
    }

    private static Texture CreateComputeTexture(PixelFormat format, uint width, uint height)
    {
        return App.Context.CreateTexture(TextureDesc.Texture2D(format, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });
    }
}

[StructLayout(LayoutKind.Explicit, Size = 256)]
file struct ShadowConstants
{
    [FieldOffset(0)]
    public Matrix4x4 InverseJitteredViewProjection;

    [FieldOffset(64)]
    public Vector3 CameraPosition;

    [FieldOffset(76)]
    public uint FrameIndex;

    [FieldOffset(80)]
    public Vector3 PreviousCameraPosition;

    [FieldOffset(96)]
    public uint Width;

    [FieldOffset(100)]
    public uint Height;

    [FieldOffset(104)]
    public uint Reset;

    [FieldOffset(108)]
    public uint StepSize;

    [FieldOffset(112)]
    public Vector2 JitterDelta;

    [FieldOffset(120)]
    public float NightLightIntensity;

    [FieldOffset(128)]
    public Vector3 KeyLightDirection;

    [FieldOffset(140)]
    public float SoftKeyCosRadius;

    [FieldOffset(144)]
    public ResourceHandle Scene;

    [FieldOffset(152)]
    public ResourceHandle Depth;

    [FieldOffset(160)]
    public ResourceHandle NormalAndRoughness;

    [FieldOffset(168)]
    public ResourceHandle MotionVectors;

    [FieldOffset(176)]
    public ResourceHandle PreviousLinearDepth;

    [FieldOffset(184)]
    public ResourceHandle CurrentLinearDepth;

    [FieldOffset(192)]
    public ResourceHandle PreviousHistory;

    [FieldOffset(200)]
    public ResourceHandle CurrentHistory;

    [FieldOffset(208)]
    public ResourceHandle FilterInput;

    [FieldOffset(216)]
    public ResourceHandle FilterOutput;

    [FieldOffset(224)]
    public ResourceHandle PreviousHistoryLength;

    [FieldOffset(232)]
    public ResourceHandle CurrentHistoryLength;
}
