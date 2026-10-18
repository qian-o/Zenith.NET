using System.Runtime.InteropServices;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class CompositePass : DisposableObject
{
    private readonly Buffer buffer;
    private readonly ComputePipeline computePipeline;

    public CompositePass(uint width, uint height, uint maxGeneratedFrameCount)
    {
        Width = width;
        Height = height;

        using Shader shader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "Composite.slang"), "CompositeCS"));

        buffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(CompositeConstants) * (maxGeneratedFrameCount + 1),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        computePipeline = App.Context.CreateComputePipeline(new() { ComputeShader = shader });

        CreateTextures();
    }

    public uint Width { get; private set; }

    public uint Height { get; private set; }

    public Texture UI { get; private set; } = null!;

    public Texture Color { get; private set; } = null!;

    public Texture[] GeneratedColors { get; private set; } = [];

    public void Record(CommandBuffer commandBuffer, Texture color, ReadOnlySpan<Texture> generatedColors)
    {
        Composite(commandBuffer, color, Color, 0);

        for (int i = 0; i < generatedColors.Length; i++)
        {
            Composite(commandBuffer, generatedColors[i], GeneratedColors[i], (uint)i + 1);
        }
    }

    public void SetGeneratedFrameCount(uint generatedFrameCount)
    {
        DestroyGeneratedColors();

        GeneratedColors = [.. Enumerable.Range(0, (int)generatedFrameCount).Select(_ => CreateTexture())];
    }

    public void Resize(uint width, uint height)
    {
        if (Width == width && Height == height)
        {
            return;
        }

        uint generatedFrameCount = (uint)GeneratedColors.Length;

        DestroyTextures();

        Width = width;
        Height = height;

        CreateTextures();
        SetGeneratedFrameCount(generatedFrameCount);
    }

    protected override void Destroy()
    {
        DestroyTextures();
        computePipeline?.Dispose();
        buffer?.Dispose();
    }

    private void Composite(CommandBuffer commandBuffer, Texture input, Texture output, uint index)
    {
        uint offsetInBytes = (uint)sizeof(CompositeConstants) * index;

        CompositeConstants constants = new()
        {
            Width = Width,
            Height = Height,
            Color = input.SampledHandle,
            UI = UI.SampledHandle,
            Output = output.StorageHandle
        };

        buffer.Upload(offsetInBytes, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(CompositeConstants)
        });

        ThreadGroupSize group = computePipeline.Desc.ComputeShader.Desc.ThreadGroupSize;

        commandBuffer.Transition(output, default, TextureLayout.Undefined, TextureLayout.Storage);
        commandBuffer.SetPipeline(computePipeline);
        commandBuffer.SetConstantBuffer(buffer, offsetInBytes);
        commandBuffer.Dispatch((Width + group.X - 1) / group.X, (Height + group.Y - 1) / group.Y, 1);
        commandBuffer.Transition(output, default, TextureLayout.Storage, TextureLayout.CopySrc);
    }

    private void CreateTextures()
    {
        UI = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.B8G8R8A8UNorm, Width, Height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.ColorAttachment
        });
        Color = CreateTexture();
    }

    private Texture CreateTexture()
    {
        return App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.B8G8R8A8UNorm, Width, Height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Storage | TextureUsages.TransferSrc
        });
    }

    private void DestroyGeneratedColors()
    {
        foreach (Texture generatedColor in GeneratedColors)
        {
            generatedColor.Dispose();
        }

        GeneratedColors = [];
    }

    private void DestroyTextures()
    {
        DestroyGeneratedColors();

        Color?.Dispose();
        Color = null!;

        UI?.Dispose();
        UI = null!;
    }
}

[StructLayout(LayoutKind.Explicit, Size = 256)]
file struct CompositeConstants
{
    [FieldOffset(0)]
    public uint Width;

    [FieldOffset(4)]
    public uint Height;

    [FieldOffset(16)]
    public ResourceHandle Color;

    [FieldOffset(24)]
    public ResourceHandle UI;

    [FieldOffset(32)]
    public ResourceHandle Output;
}
