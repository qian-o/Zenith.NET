using System.Numerics;
using System.Runtime.InteropServices;
using CornellBox.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class ShadowPass : DisposableObject
{
    private const uint ConstantSlotCount = 4;

    private readonly Buffer constantBuffer;
    private readonly ComputePipeline tracePipeline;
    private readonly ComputePipeline filterPipeline;

    private Texture filterScratch = null!;
    private Texture prevHistory = null!;
    private Texture currHistory = null!;
    private Texture prevHistoryLength = null!;
    private Texture currHistoryLength = null!;
    private Texture prevLinearDepth = null!;
    private Texture currLinearDepth = null!;
    private Vector2 previousJitterOffset;
    private Vector3 previousKeyLightDirection;
    private bool hadNightLights;
    private uint filterWarmupFrames;

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

    public Texture ShadowAo { get; private set; } = null!;

    public Texture PreviousLinearDepth => currLinearDepth;

    public void Record(CommandBuffer commandBuffer, SceneResources scene, GBufferPass gBuffer, in FrameContext frame)
    {
        Vector2 effectivePreviousJitter = frame.Reset ? frame.JitterOffset : previousJitterOffset;
        Vector2 jitterDelta = (Width > 0 && Height > 0) ? (frame.JitterOffset - effectivePreviousJitter) / new Vector2(Width, Height) : Vector2.Zero;

        bool hasNightLights = frame.Environment.NightLightIntensity > 0.001f;
        bool hasKeyLightChanged = !frame.Reset && Vector3.DistanceSquared(frame.Environment.KeyLightDirection, previousKeyLightDirection) > 1e-6f;
        bool hasNightLightChanged = !frame.Reset && (hasNightLights != hadNightLights);
        uint resetFlags = (frame.Reset ? 1u : 0u) | (hasKeyLightChanged ? 2u : 0u) | (hasNightLightChanged ? 4u : 0u);
        if (resetFlags is not 0u)
        {
            filterWarmupFrames = 12u;
        }

        previousKeyLightDirection = frame.Environment.KeyLightDirection;
        hadNightLights = hasNightLights;

        bool isWarmupFilter = filterWarmupFrames > 0u;
        if (isWarmupFilter)
        {
            filterWarmupFrames--;
        }

        uint stride = (uint)sizeof(ShadowConstants);
        ShadowConstants* slots = stackalloc ShadowConstants[(int)ConstantSlotCount];

        ShadowConstants baseConstants = new()
        {
            InverseJitteredViewProjection = frame.InverseJitteredViewProjection,
            CameraPosition = frame.CameraPosition,
            FrameIndex = frame.FrameIndex,
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
            PrevLinearDepth = frame.Reset ? default : prevLinearDepth.SampledHandle,
            CurrLinearDepth = currLinearDepth.StorageHandle,
            PrevHistory = frame.Reset ? default : prevHistory.SampledHandle,
            CurrHistory = currHistory.StorageHandle,
            PrevHistoryLength = frame.Reset ? default : prevHistoryLength.SampledHandle,
            CurrHistoryLength = currHistoryLength.StorageHandle
        };

        uint activeSlots;
        slots[0] = baseConstants with { StepSize = 0 };

        if (isWarmupFilter)
        {
            activeSlots = 4u;
            slots[1] = baseConstants with
            {
                StepSize = 4,
                PrevLinearDepth = currLinearDepth.SampledHandle,
                FilterInput = currHistory.SampledHandle,
                FilterOutput = ShadowAo.StorageHandle,
                PrevHistoryLength = currHistoryLength.SampledHandle
            };
            slots[2] = baseConstants with
            {
                StepSize = 2,
                PrevLinearDepth = currLinearDepth.SampledHandle,
                FilterInput = ShadowAo.SampledHandle,
                FilterOutput = filterScratch.StorageHandle,
                PrevHistoryLength = currHistoryLength.SampledHandle
            };
            slots[3] = baseConstants with
            {
                StepSize = 1,
                PrevLinearDepth = currLinearDepth.SampledHandle,
                FilterInput = filterScratch.SampledHandle,
                FilterOutput = ShadowAo.StorageHandle,
                PrevHistoryLength = currHistoryLength.SampledHandle
            };
        }
        else
        {
            activeSlots = 3u;
            slots[1] = baseConstants with
            {
                StepSize = 2,
                PrevLinearDepth = currLinearDepth.SampledHandle,
                FilterInput = currHistory.SampledHandle,
                FilterOutput = filterScratch.StorageHandle,
                PrevHistoryLength = currHistoryLength.SampledHandle
            };
            slots[2] = baseConstants with
            {
                StepSize = 1,
                PrevLinearDepth = currLinearDepth.SampledHandle,
                FilterInput = filterScratch.SampledHandle,
                FilterOutput = ShadowAo.StorageHandle,
                PrevHistoryLength = currHistoryLength.SampledHandle
            };
        }

        constantBuffer.Upload(0, new()
        {
            Pointer = (nint)slots,
            SizeInBytes = stride * activeSlots
        });

        commandBuffer.Transition(currLinearDepth, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.Transition(currHistory, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.Transition(currHistoryLength, default, TextureLayout.Undefined, TextureLayout.Storage);
        Dispatch(commandBuffer, tracePipeline, 0);

        commandBuffer.Transition(currLinearDepth, default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Transition(currHistory, default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Transition(currHistoryLength, default, TextureLayout.Storage, TextureLayout.Sampled);

        if (isWarmupFilter)
        {
            commandBuffer.Transition(ShadowAo, default, TextureLayout.Undefined, TextureLayout.Storage);
            commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
            Dispatch(commandBuffer, filterPipeline, stride);
            commandBuffer.Transition(ShadowAo, default, TextureLayout.Storage, TextureLayout.Sampled);

            commandBuffer.Transition(filterScratch, default, TextureLayout.Undefined, TextureLayout.Storage);
            commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
            Dispatch(commandBuffer, filterPipeline, stride * 2);
            commandBuffer.Transition(filterScratch, default, TextureLayout.Storage, TextureLayout.Sampled);

            commandBuffer.Transition(ShadowAo, default, TextureLayout.Sampled, TextureLayout.Storage);
            commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
            Dispatch(commandBuffer, filterPipeline, stride * 3);
            commandBuffer.Transition(ShadowAo, default, TextureLayout.Storage, TextureLayout.Sampled);
        }
        else
        {
            commandBuffer.Transition(filterScratch, default, TextureLayout.Undefined, TextureLayout.Storage);
            commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
            Dispatch(commandBuffer, filterPipeline, stride);
            commandBuffer.Transition(filterScratch, default, TextureLayout.Storage, TextureLayout.Sampled);

            commandBuffer.Transition(ShadowAo, default, TextureLayout.Undefined, TextureLayout.Storage);
            commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);
            Dispatch(commandBuffer, filterPipeline, stride * 2);
            commandBuffer.Transition(ShadowAo, default, TextureLayout.Storage, TextureLayout.Sampled);
        }

        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

        (prevHistory, currHistory) = (currHistory, prevHistory);
        (prevHistoryLength, currHistoryLength) = (currHistoryLength, prevHistoryLength);
        (prevLinearDepth, currLinearDepth) = (currLinearDepth, prevLinearDepth);
        previousJitterOffset = frame.JitterOffset;
    }

    public void Resize(uint width, uint height)
    {
        previousJitterOffset = Vector2.Zero;
        filterWarmupFrames = 0;

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

    private void Dispatch(CommandBuffer commandBuffer, ComputePipeline pipeline, uint constantOffset)
    {
        ThreadGroupSize group = pipeline.Desc.ComputeShader.Desc.ThreadGroupSize;

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(constantBuffer, constantOffset);
        commandBuffer.Dispatch((Width + group.X - 1) / group.X, (Height + group.Y - 1) / group.Y, 1);
    }

    private void CreateResources(uint width, uint height)
    {
        Width = width;
        Height = height;

        ShadowAo = CreateComputeTexture(PixelFormat.R16G16B16A16Float, width, height);
        filterScratch = CreateComputeTexture(PixelFormat.R16G16B16A16Float, width, height);
        prevHistory = CreateComputeTexture(PixelFormat.R16G16B16A16Float, width, height);
        currHistory = CreateComputeTexture(PixelFormat.R16G16B16A16Float, width, height);
        prevHistoryLength = CreateComputeTexture(PixelFormat.R16G16Float, width, height);
        currHistoryLength = CreateComputeTexture(PixelFormat.R16G16Float, width, height);
        prevLinearDepth = CreateComputeTexture(PixelFormat.R32Float, width, height);
        currLinearDepth = CreateComputeTexture(PixelFormat.R32Float, width, height);
    }

    private void DestroyResources()
    {
        currLinearDepth?.Dispose();
        currLinearDepth = null!;
        prevLinearDepth?.Dispose();
        prevLinearDepth = null!;
        currHistoryLength?.Dispose();
        currHistoryLength = null!;
        prevHistoryLength?.Dispose();
        prevHistoryLength = null!;
        currHistory?.Dispose();
        currHistory = null!;
        prevHistory?.Dispose();
        prevHistory = null!;
        filterScratch?.Dispose();
        filterScratch = null!;
        ShadowAo?.Dispose();
        ShadowAo = null!;

        Width = 0;
        Height = 0;
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
    public uint Width;

    [FieldOffset(84)]
    public uint Height;

    [FieldOffset(88)]
    public uint Reset;

    [FieldOffset(92)]
    public uint StepSize;

    [FieldOffset(96)]
    public Vector2 JitterDelta;

    [FieldOffset(104)]
    public float NightLightIntensity;

    [FieldOffset(112)]
    public Vector3 KeyLightDirection;

    [FieldOffset(124)]
    public float SoftKeyCosRadius;

    [FieldOffset(128)]
    public ResourceHandle Scene;

    [FieldOffset(136)]
    public ResourceHandle Depth;

    [FieldOffset(144)]
    public ResourceHandle NormalAndRoughness;

    [FieldOffset(152)]
    public ResourceHandle MotionVectors;

    [FieldOffset(160)]
    public ResourceHandle PrevLinearDepth;

    [FieldOffset(168)]
    public ResourceHandle CurrLinearDepth;

    [FieldOffset(176)]
    public ResourceHandle PrevHistory;

    [FieldOffset(184)]
    public ResourceHandle CurrHistory;

    [FieldOffset(192)]
    public ResourceHandle FilterInput;

    [FieldOffset(200)]
    public ResourceHandle FilterOutput;

    [FieldOffset(208)]
    public ResourceHandle PrevHistoryLength;

    [FieldOffset(216)]
    public ResourceHandle CurrHistoryLength;
}
