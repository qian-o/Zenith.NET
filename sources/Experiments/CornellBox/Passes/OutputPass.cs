using System.Runtime.InteropServices;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class OutputPass : DisposableObject
{
    private const uint ConstantsStrideInBytes = 256;

    private readonly Buffer constants;
    private readonly GraphicsPipeline toneMappingPipeline;
    private readonly GraphicsPipeline compositePipeline;

    private Texture[] frames = [];

    public OutputPass()
    {
        constants = App.Context.CreateBuffer(new()
        {
            SizeInBytes = ConstantsStrideInBytes * 3,
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "Output.slang");
        using Shader vertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "FullscreenVS"));
        using Shader toneMappingFragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "ToneMappingFS"));
        using Shader compositeFragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "CompositeFS"));

        toneMappingPipeline = CreatePipeline(vertexShader, toneMappingFragmentShader, PixelFormat.R8G8B8A8UNorm);
        compositePipeline = CreatePipeline(vertexShader, compositeFragmentShader, PixelFormat.B8G8R8A8UNorm);
    }

    public Texture Color { get; private set; } = null!;

    public Texture Overlay { get; private set; } = null!;

    public void Record(CommandBuffer commandBuffer, Texture input, uint frameIndex)
    {
        OutputConstants toneMappingConstants = new()
        {
            Input = input.SampledHandle,
            FrameIndex = frameIndex
        };

        constants.Upload(0, new()
        {
            Pointer = (nint)(&toneMappingConstants),
            SizeInBytes = (uint)sizeof(OutputConstants)
        });

        Draw(commandBuffer, toneMappingPipeline, Color, 0);
    }

    public ReadOnlySpan<Texture> Composite(CommandBuffer commandBuffer, scoped ReadOnlySpan<Texture> inputs)
    {
        for (int i = 0; i < inputs.Length; i++)
        {
            uint offsetInBytes = ConstantsStrideInBytes * (uint)(i + 1);

            OutputConstants compositeConstants = new()
            {
                Input = inputs[i].SampledHandle,
                Overlay = Overlay.SampledHandle
            };

            constants.Upload(offsetInBytes, new()
            {
                Pointer = (nint)(&compositeConstants),
                SizeInBytes = (uint)sizeof(OutputConstants)
            });

            Draw(commandBuffer, compositePipeline, frames[i], offsetInBytes);
        }

        return frames.AsSpan(0, inputs.Length);
    }

    public void Resize(int frameCount)
    {
        DestroyTextures();
        CreateTextures(frameCount);
    }

    protected override void Destroy()
    {
        DestroyTextures();
        compositePipeline?.Dispose();
        toneMappingPipeline?.Dispose();
        constants?.Dispose();
    }

    private void CreateTextures(int frameCount)
    {
        Color = App.Context.CreateTexture(TextureDesc.ColorAttachment(PixelFormat.R8G8B8A8UNorm, App.PhysicalWidth, App.PhysicalHeight, 1, SampleCount.Count1));
        Overlay = App.Context.CreateTexture(TextureDesc.ColorAttachment(PixelFormat.B8G8R8A8UNorm, App.PhysicalWidth, App.PhysicalHeight, 1, SampleCount.Count1));

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
        commandBuffer.SetConstantBuffer(constants, offsetInBytes);
        commandBuffer.Draw(3, 1, 0, 0);
        commandBuffer.EndRenderPass();
        commandBuffer.Transition(output, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
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
