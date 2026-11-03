using System.Runtime.InteropServices;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class OutputPass : DisposableObject
{
    private const uint ConstantsStride = 256;

    private readonly Buffer buffer;
    private readonly GraphicsPipeline toneMappingPipeline;
    private readonly GraphicsPipeline compositePipeline;

    private Texture[] frames = [];

    public OutputPass()
    {
        buffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = ConstantsStride * 3,
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "Output.slang");
        using Shader vertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "FullscreenVS"));
        using Shader toneMappingShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "ToneMappingFS"));
        using Shader compositeShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "CompositeFS"));

        toneMappingPipeline = CreatePipeline(vertexShader, toneMappingShader, PixelFormat.R8G8B8A8UNorm);
        compositePipeline = CreatePipeline(vertexShader, compositeShader, PixelFormat.B8G8R8A8UNorm);
    }

    public Texture Color { get; private set; } = null!;

    public Texture Overlay { get; private set; } = null!;

    public void Record(CommandBuffer commandBuffer, Texture input, uint frameIndex)
    {
        OutputConstants constants = new()
        {
            Input = input.SampledHandle,
            FrameIndex = frameIndex
        };

        buffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(OutputConstants)
        });

        Draw(commandBuffer, toneMappingPipeline, Color, 0);
    }

    public ReadOnlySpan<Texture> Composite(CommandBuffer commandBuffer, scoped ReadOnlySpan<Texture> inputs)
    {
        for (int i = 0; i < inputs.Length; i++)
        {
            uint offsetInBytes = ConstantsStride * (uint)(i + 1);
            OutputConstants constants = new()
            {
                Input = inputs[i].SampledHandle,
                Overlay = Overlay.SampledHandle
            };

            buffer.Upload(offsetInBytes, new()
            {
                Pointer = (nint)(&constants),
                SizeInBytes = (uint)sizeof(OutputConstants)
            });

            Draw(commandBuffer, compositePipeline, frames[i], offsetInBytes);
        }

        return frames.AsSpan(0, inputs.Length);
    }

    public void Resize(uint width, uint height, int frameCount)
    {
        DestroyTextures();
        CreateTextures(width, height, frameCount);
    }

    protected override void Destroy()
    {
        DestroyTextures();

        compositePipeline?.Dispose();
        toneMappingPipeline?.Dispose();
        buffer?.Dispose();
    }

    private static GraphicsPipeline CreatePipeline(Shader vertexShader, Shader fragmentShader, PixelFormat format)
    {
        return App.Context.CreateGraphicsPipeline(new()
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            InputLayouts = [],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            AttachmentFormats = new()
            {
                ColorFormats = [format],
                SampleCount = SampleCount.Count1
            },
            RenderState = new()
            {
                Rasterizer = RasterizerState.CullNone(),
                DepthStencil = DepthStencilState.DepthNone(),
                Blend = BlendState.Opaque()
            }
        });
    }

    private void CreateTextures(uint width, uint height, int frameCount)
    {
        Color = App.Context.CreateTexture(TextureDesc.ColorAttachment(PixelFormat.R8G8B8A8UNorm, width, height, 1, SampleCount.Count1));
        Overlay = App.Context.CreateTexture(TextureDesc.ColorAttachment(PixelFormat.B8G8R8A8UNorm, width, height, 1, SampleCount.Count1));

        frames = new Texture[frameCount];
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = App.Context.CreateTexture(Overlay.Desc with
            {
                Usages = Overlay.Desc.Usages | TextureUsages.TransferSrc
            });
        }
    }

    private void DestroyTextures()
    {
        for (int i = frames.Length - 1; i >= 0; i--)
        {
            frames[i]?.Dispose();
        }
        frames = [];

        Overlay?.Dispose();
        Overlay = null!;
        Color?.Dispose();
        Color = null!;
    }

    private void Draw(CommandBuffer commandBuffer, GraphicsPipeline pipeline, Texture output, uint offsetInBytes)
    {
        commandBuffer.Transition(output, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.DontCare(output)], null);
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(buffer, offsetInBytes);
        commandBuffer.Draw(3, 1, 0, 0);
        commandBuffer.EndRenderPass();
        commandBuffer.Transition(output, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
    }
}

[StructLayout(LayoutKind.Explicit, Size = 32)]
file struct OutputConstants
{
    [FieldOffset(0)]
    public ResourceHandle Input;

    [FieldOffset(8)]
    public ResourceHandle Overlay;

    [FieldOffset(16)]
    public uint FrameIndex;
}
