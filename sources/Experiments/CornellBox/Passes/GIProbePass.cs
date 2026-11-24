using System.Runtime.InteropServices;
using CornellBox.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class GIProbePass : DisposableObject
{
    private const uint ConstantSlotCount = 4;
    private const uint ProbeCount = 2784;
    private const uint SteadyStateStride = 4;
    private const uint IrradianceAtlasWidth = 512;
    private const uint IrradianceAtlasHeight = 352;
    private const uint DistanceAtlasWidth = 1024;
    private const uint DistanceAtlasHeight = 704;
    private const float SteadyStateHysteresis = 0.975f;

    private readonly Buffer constantBuffer;
    private readonly ComputePipeline pipeline;

    private Texture previousIrradianceAtlas;
    private Texture currentIrradianceAtlas;
    private Texture previousDistanceAtlas;
    private Texture currentDistanceAtlas;
    private uint remainingWarmupFrameCount;

    public GIProbePass()
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(GIProbeConstants) * ConstantSlotCount),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        using Shader shader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "GIProbe.slang"), "UpdateProbesCS"));
        pipeline = App.Context.CreateComputePipeline(new() { ComputeShader = shader });

        LinearSampler = App.Context.CreateSampler(SamplerDesc.LinearClamp());

        previousIrradianceAtlas = CreateAtlasTexture(PixelFormat.R16G16B16A16Float, IrradianceAtlasWidth, IrradianceAtlasHeight);
        currentIrradianceAtlas = CreateAtlasTexture(PixelFormat.R16G16B16A16Float, IrradianceAtlasWidth, IrradianceAtlasHeight);
        previousDistanceAtlas = CreateAtlasTexture(PixelFormat.R16G16Float, DistanceAtlasWidth, DistanceAtlasHeight);
        currentDistanceAtlas = CreateAtlasTexture(PixelFormat.R16G16Float, DistanceAtlasWidth, DistanceAtlasHeight);
    }

    public Texture IrradianceAtlas => previousIrradianceAtlas;

    public Texture DistanceAtlas => previousDistanceAtlas;

    public Sampler LinearSampler { get; }

    public void Record(CommandBuffer commandBuffer, SceneResources scene, in FrameContext frame)
    {
        uint strideInBytes = (uint)sizeof(GIProbeConstants);
        GIProbeConstants* slots = stackalloc GIProbeConstants[(int)ConstantSlotCount];

        if (frame.ResetLighting)
        {
            remainingWarmupFrameCount = 16;

            slots[0] = CreateConstants(scene, LinearSampler, in frame, frame.FrameIndex, 0.0f, 1, 0, reset: true, previousIrradianceAtlas, previousDistanceAtlas, currentIrradianceAtlas, currentDistanceAtlas);
            slots[1] = CreateConstants(scene, LinearSampler, in frame, frame.FrameIndex + 1, 0.0f, 1, 0, reset: false, currentIrradianceAtlas, currentDistanceAtlas, previousIrradianceAtlas, previousDistanceAtlas);
            slots[2] = CreateConstants(scene, LinearSampler, in frame, frame.FrameIndex + 2, 0.5f, 1, 0, reset: false, previousIrradianceAtlas, previousDistanceAtlas, currentIrradianceAtlas, currentDistanceAtlas);
            slots[3] = CreateConstants(scene, LinearSampler, in frame, frame.FrameIndex + 3, 0.67f, 1, 0, reset: false, currentIrradianceAtlas, currentDistanceAtlas, previousIrradianceAtlas, previousDistanceAtlas);

            constantBuffer.Upload(0, new()
            {
                Pointer = (nint)slots,
                SizeInBytes = strideInBytes * ConstantSlotCount
            });

            DispatchFullSlot(commandBuffer, 0, ref previousIrradianceAtlas, ref currentIrradianceAtlas, ref previousDistanceAtlas, ref currentDistanceAtlas);
            DispatchFullSlot(commandBuffer, strideInBytes, ref previousIrradianceAtlas, ref currentIrradianceAtlas, ref previousDistanceAtlas, ref currentDistanceAtlas);
            DispatchFullSlot(commandBuffer, strideInBytes * 2, ref previousIrradianceAtlas, ref currentIrradianceAtlas, ref previousDistanceAtlas, ref currentDistanceAtlas);
            DispatchFullSlot(commandBuffer, strideInBytes * 3, ref previousIrradianceAtlas, ref currentIrradianceAtlas, ref previousDistanceAtlas, ref currentDistanceAtlas);
        }
        else if (remainingWarmupFrameCount > 12)
        {
            remainingWarmupFrameCount--;

            slots[0] = CreateConstants(scene, LinearSampler, in frame, frame.FrameIndex, 0.80f, 1, 0, reset: false, previousIrradianceAtlas, previousDistanceAtlas, currentIrradianceAtlas, currentDistanceAtlas);

            constantBuffer.Upload(0, new()
            {
                Pointer = (nint)slots,
                SizeInBytes = strideInBytes
            });

            DispatchFullSlot(commandBuffer, 0, ref previousIrradianceAtlas, ref currentIrradianceAtlas, ref previousDistanceAtlas, ref currentDistanceAtlas);
        }
        else
        {
            float hysteresis = SteadyStateHysteresis;

            if (remainingWarmupFrameCount > 0)
            {
                remainingWarmupFrameCount--;
                hysteresis = 0.90f;
            }

            uint phase = frame.FrameIndex & (SteadyStateStride - 1u);
            slots[0] = CreateConstants(scene, LinearSampler, in frame, frame.FrameIndex, hysteresis, SteadyStateStride, phase, reset: false, previousIrradianceAtlas, previousDistanceAtlas, currentIrradianceAtlas, currentDistanceAtlas);

            constantBuffer.Upload(0, new()
            {
                Pointer = (nint)slots,
                SizeInBytes = strideInBytes
            });

            commandBuffer.Transition(previousIrradianceAtlas, default, TextureLayout.Sampled, TextureLayout.CopySrc);
            commandBuffer.Transition(currentIrradianceAtlas, default, TextureLayout.Undefined, TextureLayout.CopyDst);
            commandBuffer.Transition(previousDistanceAtlas, default, TextureLayout.Sampled, TextureLayout.CopySrc);
            commandBuffer.Transition(currentDistanceAtlas, default, TextureLayout.Undefined, TextureLayout.CopyDst);

            commandBuffer.CopyTexture(previousIrradianceAtlas, default, default, currentIrradianceAtlas, default, default, new()
            {
                Width = IrradianceAtlasWidth,
                Height = IrradianceAtlasHeight,
                Depth = 1
            });
            commandBuffer.CopyTexture(previousDistanceAtlas, default, default, currentDistanceAtlas, default, default, new()
            {
                Width = DistanceAtlasWidth,
                Height = DistanceAtlasHeight,
                Depth = 1
            });

            commandBuffer.Transition(previousIrradianceAtlas, default, TextureLayout.CopySrc, TextureLayout.Sampled);
            commandBuffer.Transition(currentIrradianceAtlas, default, TextureLayout.CopyDst, TextureLayout.Storage);
            commandBuffer.Transition(previousDistanceAtlas, default, TextureLayout.CopySrc, TextureLayout.Sampled);
            commandBuffer.Transition(currentDistanceAtlas, default, TextureLayout.CopyDst, TextureLayout.Storage);
            commandBuffer.Barrier(BarrierStages.Copy, BarrierStages.ComputeShading);

            commandBuffer.SetPipeline(pipeline);
            commandBuffer.SetConstantBuffer(constantBuffer, 0);
            commandBuffer.Dispatch(ProbeCount / SteadyStateStride, 1, 1);

            commandBuffer.Transition(currentIrradianceAtlas, default, TextureLayout.Storage, TextureLayout.Sampled);
            commandBuffer.Transition(currentDistanceAtlas, default, TextureLayout.Storage, TextureLayout.Sampled);
            commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

            (previousIrradianceAtlas, currentIrradianceAtlas) = (currentIrradianceAtlas, previousIrradianceAtlas);
            (previousDistanceAtlas, currentDistanceAtlas) = (currentDistanceAtlas, previousDistanceAtlas);
        }

        static GIProbeConstants CreateConstants(SceneResources scene,
                                                Sampler linearSampler,
                                                in FrameContext frame,
                                                uint slotFrameIndex,
                                                float hysteresis,
                                                uint probeStride,
                                                uint probePhase,
                                                bool reset,
                                                Texture previousIrradiance,
                                                Texture previousDistance,
                                                Texture currentIrradiance,
                                                Texture currentDistance)
        {
            return new()
            {
                FrameIndex = slotFrameIndex,
                Hysteresis = hysteresis,
                ProbeStride = probeStride,
                PhaseAndReset = (probePhase << 1) | (reset ? 1u : 0u),
                Environment = frame.Environment,
                Scene = scene.AccelerationStructure,
                Vertices = scene.Vertices,
                Indices = scene.Indices,
                Materials = scene.Materials,
                Instances = scene.Instances,
                PreviousIrradianceAtlas = reset ? default : previousIrradiance.SampledHandle,
                PreviousDistanceAtlas = reset ? default : previousDistance.SampledHandle,
                CurrentIrradianceAtlas = currentIrradiance.StorageHandle,
                CurrentDistanceAtlas = currentDistance.StorageHandle,
                LinearSampler = linearSampler.Handle
            };
        }
    }

    protected override void Destroy()
    {
        currentDistanceAtlas?.Dispose();
        previousDistanceAtlas?.Dispose();
        currentIrradianceAtlas?.Dispose();
        previousIrradianceAtlas?.Dispose();
        LinearSampler?.Dispose();
        pipeline?.Dispose();
        constantBuffer?.Dispose();
    }

    private void DispatchFullSlot(CommandBuffer commandBuffer,
                                  uint offsetInBytes,
                                  ref Texture previousIrradiance,
                                  ref Texture currentIrradiance,
                                  ref Texture previousDistance,
                                  ref Texture currentDistance)
    {
        commandBuffer.Transition(currentIrradiance, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.Transition(currentDistance, default, TextureLayout.Undefined, TextureLayout.Storage);

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(constantBuffer, offsetInBytes);
        commandBuffer.Dispatch(ProbeCount, 1, 1);

        commandBuffer.Transition(currentIrradiance, default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Transition(currentDistance, default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

        (previousIrradiance, currentIrradiance) = (currentIrradiance, previousIrradiance);
        (previousDistance, currentDistance) = (currentDistance, previousDistance);
    }

    private static Texture CreateAtlasTexture(PixelFormat format, uint width, uint height)
    {
        return App.Context.CreateTexture(TextureDesc.Texture2D(format, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage | TextureUsages.TransferSrc | TextureUsages.TransferDst
        });
    }
}

[StructLayout(LayoutKind.Explicit, Size = 256)]
file struct GIProbeConstants
{
    [FieldOffset(0)]
    public uint FrameIndex;

    [FieldOffset(4)]
    public float Hysteresis;

    [FieldOffset(8)]
    public uint ProbeStride;

    [FieldOffset(12)]
    public uint PhaseAndReset;

    [FieldOffset(16)]
    public SceneEnvironment Environment;

    [FieldOffset(112)]
    public ResourceHandle Scene;

    [FieldOffset(120)]
    public ResourceHandle Vertices;

    [FieldOffset(128)]
    public ResourceHandle Indices;

    [FieldOffset(136)]
    public ResourceHandle Materials;

    [FieldOffset(144)]
    public ResourceHandle Instances;

    [FieldOffset(152)]
    public ResourceHandle PreviousIrradianceAtlas;

    [FieldOffset(160)]
    public ResourceHandle PreviousDistanceAtlas;

    [FieldOffset(168)]
    public ResourceHandle CurrentIrradianceAtlas;

    [FieldOffset(176)]
    public ResourceHandle CurrentDistanceAtlas;

    [FieldOffset(184)]
    public ResourceHandle LinearSampler;
}
