using System.Runtime.InteropServices;
using CornellBox.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class GiProbePass : DisposableObject
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
    private readonly Sampler linearSampler;

    private Texture prevIrradianceAtlas;
    private Texture currIrradianceAtlas;
    private Texture prevDistanceAtlas;
    private Texture currDistanceAtlas;
    private uint warmupFramesRemaining;

    public GiProbePass()
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(GiProbeConstants) * ConstantSlotCount),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        using Shader shader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "GiProbe.slang"), "UpdateProbesCS"));
        pipeline = App.Context.CreateComputePipeline(new() { ComputeShader = shader });

        linearSampler = App.Context.CreateSampler(SamplerDesc.LinearClamp());

        prevIrradianceAtlas = CreateAtlasTexture(PixelFormat.R16G16B16A16Float, IrradianceAtlasWidth, IrradianceAtlasHeight);
        currIrradianceAtlas = CreateAtlasTexture(PixelFormat.R16G16B16A16Float, IrradianceAtlasWidth, IrradianceAtlasHeight);
        prevDistanceAtlas = CreateAtlasTexture(PixelFormat.R16G16Float, DistanceAtlasWidth, DistanceAtlasHeight);
        currDistanceAtlas = CreateAtlasTexture(PixelFormat.R16G16Float, DistanceAtlasWidth, DistanceAtlasHeight);
    }

    public Texture IrradianceAtlas => prevIrradianceAtlas;

    public Texture DistanceAtlas => prevDistanceAtlas;

    public Sampler LinearSampler => linearSampler;

    public void Record(CommandBuffer commandBuffer, SceneResources scene, in FrameContext frame)
    {
        uint stride = (uint)sizeof(GiProbeConstants);
        GiProbeConstants* slots = stackalloc GiProbeConstants[(int)ConstantSlotCount];

        if (frame.ResetLighting)
        {
            warmupFramesRemaining = 16;

            slots[0] = CreateConstants(in frame, frame.FrameIndex, hysteresis: 0.0f, probeStride: 1, probePhase: 0, resetFlag: 1, prevIrradianceAtlas, prevDistanceAtlas, currIrradianceAtlas, currDistanceAtlas);
            slots[1] = CreateConstants(in frame, frame.FrameIndex + 1, hysteresis: 0.0f, probeStride: 1, probePhase: 0, resetFlag: 0, currIrradianceAtlas, currDistanceAtlas, prevIrradianceAtlas, prevDistanceAtlas);
            slots[2] = CreateConstants(in frame, frame.FrameIndex + 2, hysteresis: 0.5f, probeStride: 1, probePhase: 0, resetFlag: 0, prevIrradianceAtlas, prevDistanceAtlas, currIrradianceAtlas, currDistanceAtlas);
            slots[3] = CreateConstants(in frame, frame.FrameIndex + 3, hysteresis: 0.67f, probeStride: 1, probePhase: 0, resetFlag: 0, currIrradianceAtlas, currDistanceAtlas, prevIrradianceAtlas, prevDistanceAtlas);

            constantBuffer.Upload(0, new()
            {
                Pointer = (nint)slots,
                SizeInBytes = stride * ConstantSlotCount
            });

            DispatchFullSlot(commandBuffer, 0, ref prevIrradianceAtlas, ref currIrradianceAtlas, ref prevDistanceAtlas, ref currDistanceAtlas);
            DispatchFullSlot(commandBuffer, stride, ref prevIrradianceAtlas, ref currIrradianceAtlas, ref prevDistanceAtlas, ref currDistanceAtlas);
            DispatchFullSlot(commandBuffer, stride * 2, ref prevIrradianceAtlas, ref currIrradianceAtlas, ref prevDistanceAtlas, ref currDistanceAtlas);
            DispatchFullSlot(commandBuffer, stride * 3, ref prevIrradianceAtlas, ref currIrradianceAtlas, ref prevDistanceAtlas, ref currDistanceAtlas);
        }
        else if (warmupFramesRemaining > 12)
        {
            warmupFramesRemaining--;

            slots[0] = CreateConstants(in frame, frame.FrameIndex, hysteresis: 0.80f, probeStride: 1, probePhase: 0, resetFlag: 0, prevIrradianceAtlas, prevDistanceAtlas, currIrradianceAtlas, currDistanceAtlas);

            constantBuffer.Upload(0, new()
            {
                Pointer = (nint)slots,
                SizeInBytes = stride
            });

            DispatchFullSlot(commandBuffer, 0, ref prevIrradianceAtlas, ref currIrradianceAtlas, ref prevDistanceAtlas, ref currDistanceAtlas);
        }
        else
        {
            float hysteresis = SteadyStateHysteresis;

            if (warmupFramesRemaining > 0)
            {
                warmupFramesRemaining--;
                hysteresis = 0.90f;
            }

            uint phase = frame.FrameIndex & (SteadyStateStride - 1u);
            slots[0] = CreateConstants(in frame, frame.FrameIndex, hysteresis: hysteresis, probeStride: SteadyStateStride, probePhase: phase, resetFlag: 0, prevIrradianceAtlas, prevDistanceAtlas, currIrradianceAtlas, currDistanceAtlas);

            constantBuffer.Upload(0, new()
            {
                Pointer = (nint)slots,
                SizeInBytes = stride
            });

            commandBuffer.Transition(prevIrradianceAtlas, default, TextureLayout.Sampled, TextureLayout.CopySrc);
            commandBuffer.Transition(currIrradianceAtlas, default, TextureLayout.Undefined, TextureLayout.CopyDst);
            commandBuffer.Transition(prevDistanceAtlas, default, TextureLayout.Sampled, TextureLayout.CopySrc);
            commandBuffer.Transition(currDistanceAtlas, default, TextureLayout.Undefined, TextureLayout.CopyDst);

            commandBuffer.CopyTexture(prevIrradianceAtlas, default, default, currIrradianceAtlas, default, default, new()
            {
                Width = IrradianceAtlasWidth,
                Height = IrradianceAtlasHeight,
                Depth = 1
            });
            commandBuffer.CopyTexture(prevDistanceAtlas, default, default, currDistanceAtlas, default, default, new()
            {
                Width = DistanceAtlasWidth,
                Height = DistanceAtlasHeight,
                Depth = 1
            });

            commandBuffer.Transition(prevIrradianceAtlas, default, TextureLayout.CopySrc, TextureLayout.Sampled);
            commandBuffer.Transition(currIrradianceAtlas, default, TextureLayout.CopyDst, TextureLayout.Storage);
            commandBuffer.Transition(prevDistanceAtlas, default, TextureLayout.CopySrc, TextureLayout.Sampled);
            commandBuffer.Transition(currDistanceAtlas, default, TextureLayout.CopyDst, TextureLayout.Storage);
            commandBuffer.Barrier(BarrierStages.Copy, BarrierStages.ComputeShading);

            commandBuffer.SetPipeline(pipeline);
            commandBuffer.SetConstantBuffer(constantBuffer, 0);
            commandBuffer.Dispatch(ProbeCount / SteadyStateStride, 1, 1);

            commandBuffer.Transition(currIrradianceAtlas, default, TextureLayout.Storage, TextureLayout.Sampled);
            commandBuffer.Transition(currDistanceAtlas, default, TextureLayout.Storage, TextureLayout.Sampled);
            commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

            (prevIrradianceAtlas, currIrradianceAtlas) = (currIrradianceAtlas, prevIrradianceAtlas);
            (prevDistanceAtlas, currDistanceAtlas) = (currDistanceAtlas, prevDistanceAtlas);
        }

        GiProbeConstants CreateConstants(in FrameContext currentFrame,
                                         uint slotFrameIndex,
                                         float hysteresis,
                                         uint probeStride,
                                         uint probePhase,
                                         uint resetFlag,
                                         Texture prevIrr,
                                         Texture prevDist,
                                         Texture currIrr,
                                         Texture currDist)
        {
            return new()
            {
                FrameIndex = slotFrameIndex,
                Hysteresis = hysteresis,
                ProbeStride = probeStride,
                PhaseAndReset = (probePhase << 1) | (resetFlag & 1u),
                Environment = currentFrame.Environment,
                Scene = scene.AccelerationStructure,
                Vertices = scene.Vertices,
                Indices = scene.Indices,
                Materials = scene.Materials,
                Instances = scene.Instances,
                PrevIrradianceAtlas = resetFlag is 0 ? prevIrr.SampledHandle : default,
                PrevDistanceAtlas = resetFlag is 0 ? prevDist.SampledHandle : default,
                CurrIrradianceAtlas = currIrr.StorageHandle,
                CurrDistanceAtlas = currDist.StorageHandle,
                LinearSampler = linearSampler.Handle
            };
        }
    }

    protected override void Destroy()
    {
        currDistanceAtlas?.Dispose();
        prevDistanceAtlas?.Dispose();
        currIrradianceAtlas?.Dispose();
        prevIrradianceAtlas?.Dispose();
        linearSampler?.Dispose();
        pipeline?.Dispose();
        constantBuffer?.Dispose();
    }

    private void DispatchFullSlot(CommandBuffer commandBuffer,
                                  uint constantOffset,
                                  ref Texture prevIrr,
                                  ref Texture currIrr,
                                  ref Texture prevDist,
                                  ref Texture currDist)
    {
        commandBuffer.Transition(currIrr, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.Transition(currDist, default, TextureLayout.Undefined, TextureLayout.Storage);

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(constantBuffer, constantOffset);
        commandBuffer.Dispatch(ProbeCount, 1, 1);

        commandBuffer.Transition(currIrr, default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Transition(currDist, default, TextureLayout.Storage, TextureLayout.Sampled);
        commandBuffer.Barrier(BarrierStages.ComputeShading, BarrierStages.ComputeShading);

        (prevIrr, currIrr) = (currIrr, prevIrr);
        (prevDist, currDist) = (currDist, prevDist);
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
file struct GiProbeConstants
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
    public ResourceHandle PrevIrradianceAtlas;

    [FieldOffset(160)]
    public ResourceHandle PrevDistanceAtlas;

    [FieldOffset(168)]
    public ResourceHandle CurrIrradianceAtlas;

    [FieldOffset(176)]
    public ResourceHandle CurrDistanceAtlas;

    [FieldOffset(184)]
    public ResourceHandle LinearSampler;
}
