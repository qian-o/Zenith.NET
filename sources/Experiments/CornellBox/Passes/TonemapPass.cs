using System.Numerics;
using System.Runtime.InteropServices;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class TonemapPass : DisposableObject
{
    private readonly Buffer buffer;
    private readonly Sampler sampler;
    private readonly ComputePipeline computePipeline;

    private bool isInitialized;

    public TonemapPass(uint width, uint height)
    {
        Width = width;
        Height = height;

        using Shader shader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "Tonemap.slang"), "TonemapCS"));

        buffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(TonemapConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
        sampler = App.Context.CreateSampler(SamplerDesc.LinearClamp());
        computePipeline = App.Context.CreateComputePipeline(new() { ComputeShader = shader });

        CreateTexture();
    }

    public uint Width { get; private set; }

    public uint Height { get; private set; }

    public Texture Color { get; private set; } = null!;

    public void Record(CommandBuffer commandBuffer, Texture color, uint frameIndex)
    {
        TonemapConstants constants = new()
        {
            OutputSizeFrame = new(Width, Height, BitConverter.UInt32BitsToSingle(frameIndex), 0.0f),
            Input = color.SampledHandle,
            Output = Color.StorageHandle,
            Sampler = sampler.Handle
        };

        buffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(TonemapConstants)
        });

        ThreadGroupSize group = computePipeline.Desc.ComputeShader.Desc.ThreadGroupSize;

        commandBuffer.Transition(Color, default, isInitialized ? TextureLayout.Sampled : TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.SetPipeline(computePipeline);
        commandBuffer.SetConstantBuffer(buffer, 0);
        commandBuffer.Dispatch((Width + group.X - 1) / group.X, (Height + group.Y - 1) / group.Y, 1);
        commandBuffer.Transition(Color, default, TextureLayout.Storage, TextureLayout.Sampled);

        isInitialized = true;
    }

    public void Resize(uint width, uint height)
    {
        if (Width == width && Height == height)
        {
            return;
        }

        DestroyTexture();

        Width = width;
        Height = height;

        CreateTexture();
    }

    protected override void Destroy()
    {
        DestroyTexture();
        computePipeline?.Dispose();
        sampler?.Dispose();
        buffer?.Dispose();
    }

    private void CreateTexture()
    {
        Color = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.B8G8R8A8UNorm, Width, Height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.Storage
        });
    }

    private void DestroyTexture()
    {
        Color?.Dispose();
        Color = null!;

        isInitialized = false;
    }
}

[StructLayout(LayoutKind.Explicit, Size = 48)]
file struct TonemapConstants
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
