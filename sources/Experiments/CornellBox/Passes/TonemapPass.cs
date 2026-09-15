using System.Numerics;
using System.Runtime.InteropServices;
using CornellBox.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class TonemapPass(uint renderWidth, uint renderHeight, uint displayWidth, uint displayHeight) : Pass(renderWidth, renderHeight, displayWidth, displayHeight)
{
    private Buffer buffer = null!;
    private Sampler sampler = null!;
    private ComputePipeline pipeline = null!;
    private bool resourcesInitialized;

    public Texture Color { get; private set; } = null!;

    protected override void Initialize()
    {
        using Shader shader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("Tonemap.slang"), "CSMain"));

        buffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(Constants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
        sampler = App.Context.CreateSampler(SamplerDesc.LinearClamp());
        pipeline = App.Context.CreateComputePipeline(new() { ComputeShader = shader });
        Color = CreateTexture(DisplayWidth, DisplayHeight, PixelFormat.B8G8R8A8UNorm);
    }

    protected override void RecordImpl(CommandBuffer commandBuffer, in PassArgs args)
    {
        Constants constants = new()
        {
            OutputSizeFrame = new(DisplayWidth, DisplayHeight, BitConverter.UInt32BitsToSingle(args.FrameIndex), 0.0f),
            Input = args.Color.SampledHandle,
            Output = Color.StorageHandle,
            Sampler = sampler.Handle
        };

        buffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(Constants)
        });

        commandBuffer.Transition(Color, default, resourcesInitialized ? TextureLayout.Sampled : TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(buffer, 0);
        commandBuffer.Dispatch((DisplayWidth + 7) / 8, (DisplayHeight + 7) / 8, 1);
        commandBuffer.Transition(Color, default, TextureLayout.Storage, TextureLayout.Sampled);

        resourcesInitialized = true;
    }

    protected override void ResizeImpl()
    {
        if (Color.Desc.Width == DisplayWidth && Color.Desc.Height == DisplayHeight)
        {
            return;
        }

        Color.Dispose();
        Color = CreateTexture(DisplayWidth, DisplayHeight, PixelFormat.B8G8R8A8UNorm);

        resourcesInitialized = false;
    }

    protected override void Destroy()
    {
        Color?.Dispose();
        pipeline.Dispose();
        sampler.Dispose();
        buffer.Dispose();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 48)]
file struct Constants
{
    [FieldOffset(0)]
    public Vector4 OutputSizeFrame;

    [FieldOffset(16)]
    public ResourceHandle Input;

    [FieldOffset(24)]
    public ResourceHandle Output;

    [FieldOffset(32)]
    public ResourceHandle Sampler;
}
