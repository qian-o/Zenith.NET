using System.Numerics;
using System.Runtime.InteropServices;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class TonemapPass : DisposableObject
{
    private readonly Buffer buffer;
    private readonly Sampler sampler;
    private readonly ComputePipeline pipeline;
    private bool resourcesInitialized;

    public TonemapPass()
    {
        using Shader shader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "Tonemap.slang"), "CSMain"));

        buffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(Constants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
        sampler = App.Context.CreateSampler(SamplerDesc.LinearClamp());
        pipeline = App.Context.CreateComputePipeline(new() { ComputeShader = shader });
    }

    public Texture Color { get; private set; } = null!;

    public void Render(CommandBuffer commandBuffer, Texture hdr, uint frameIndex)
    {
        Constants constants = new()
        {
            OutputSizeFrame = new(Color.Desc.Width, Color.Desc.Height, BitConverter.UInt32BitsToSingle(frameIndex), 0.0f),
            Input = hdr.SampledHandle,
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
        commandBuffer.Dispatch((Color.Desc.Width + 7) / 8, (Color.Desc.Height + 7) / 8, 1);
        commandBuffer.Transition(Color, default, TextureLayout.Storage, TextureLayout.Sampled);

        resourcesInitialized = true;
    }

    public void Resize(uint width, uint height)
    {
        Color?.Dispose();
        Color = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.B8G8R8A8UNorm,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });

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
