using System.Runtime.InteropServices;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class CompositePass : DisposableObject
{
    private readonly Buffer buffer;
    private readonly ComputePipeline computePipeline;

    public CompositePass(uint width, uint height)
    {
        Width = width;
        Height = height;

        using Shader shader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "Composite.slang"), "CompositeCS"));

        buffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(CompositeConstants),
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

    public Texture? GeneratedColor { get; private set; }

    public void Record(CommandBuffer commandBuffer, Texture color, Texture? generatedColor)
    {
        CompositeConstants constants = new()
        {
            Width = Width,
            Height = Height,
            Color = color.SampledHandle,
            GeneratedColor = generatedColor?.SampledHandle ?? default,
            UI = UI.SampledHandle,
            Output = Color.StorageHandle,
            GeneratedOutput = generatedColor is not null ? GeneratedColor!.StorageHandle : default
        };

        buffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(CompositeConstants)
        });

        ThreadGroupSize group = computePipeline.Desc.ComputeShader.Desc.ThreadGroupSize;
        uint depth = generatedColor is not null ? 2u : 1u;

        commandBuffer.Transition(Color, default, TextureLayout.Undefined, TextureLayout.Storage);

        if (generatedColor is not null)
        {
            commandBuffer.Transition(GeneratedColor!, default, TextureLayout.Undefined, TextureLayout.Storage);
        }

        commandBuffer.SetPipeline(computePipeline);
        commandBuffer.SetConstantBuffer(buffer, 0);
        commandBuffer.Dispatch((Width + group.X - 1) / group.X, (Height + group.Y - 1) / group.Y, (depth + group.Z - 1) / group.Z);
        commandBuffer.Transition(Color, default, TextureLayout.Storage, TextureLayout.CopySrc);

        if (generatedColor is not null)
        {
            commandBuffer.Transition(GeneratedColor!, default, TextureLayout.Storage, TextureLayout.CopySrc);
        }
    }

    public void SetFrameGenerationEnabled(bool enabled)
    {
        if (enabled)
        {
            GeneratedColor ??= CreateTexture();
        }
        else
        {
            GeneratedColor?.Dispose();
            GeneratedColor = null;
        }
    }

    public void Resize(uint width, uint height)
    {
        if (Width == width && Height == height)
        {
            return;
        }

        bool isFrameGenerationEnabled = GeneratedColor is not null;

        DestroyTextures();

        Width = width;
        Height = height;

        CreateTextures();

        if (isFrameGenerationEnabled)
        {
            GeneratedColor = CreateTexture();
        }
    }

    protected override void Destroy()
    {
        DestroyTextures();
        computePipeline?.Dispose();
        buffer?.Dispose();
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

    private void DestroyTextures()
    {
        GeneratedColor?.Dispose();
        GeneratedColor = null;

        Color?.Dispose();
        Color = null!;

        UI?.Dispose();
        UI = null!;
    }
}

[StructLayout(LayoutKind.Explicit, Size = 64)]
file struct CompositeConstants
{
    [FieldOffset(0)]
    public uint Width;

    [FieldOffset(4)]
    public uint Height;

    [FieldOffset(16)]
    public ResourceHandle Color;

    [FieldOffset(24)]
    public ResourceHandle GeneratedColor;

    [FieldOffset(32)]
    public ResourceHandle UI;

    [FieldOffset(40)]
    public ResourceHandle Output;

    [FieldOffset(48)]
    public ResourceHandle GeneratedOutput;
}
