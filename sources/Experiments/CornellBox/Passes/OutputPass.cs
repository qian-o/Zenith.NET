using CornellBox.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class OutputPass : DisposableObject
{
    private readonly Buffer buffer;
    private readonly GraphicsPipeline pipeline;

    public OutputPass(RenderDesc desc)
    {
        buffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(OutputConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "Output.slang");
        using Shader vertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "FullscreenVS"));
        using Shader fragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "ToneMappingFS"));

        pipeline = App.Context.CreateGraphicsPipeline(new()
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            InputLayouts = [],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            AttachmentFormats = new()
            {
                ColorFormats = [PixelFormat.R8G8B8A8UNorm],
                SampleCount = SampleCount.Count1
            },
            RenderState = new()
            {
                Rasterizer = RasterizerState.CullNone(),
                DepthStencil = DepthStencilState.DepthNone(),
                Blend = BlendState.Opaque()
            }
        });

        Resize(desc);
    }

    public Texture Color { get; private set; } = null!;

    public void Record(CommandBuffer commandBuffer, in RenderArgs args)
    {
        OutputConstants constants = new()
        {
            Input = args.Color.SampledHandle,
            FrameIndex = args.FrameIndex
        };

        buffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(OutputConstants)
        });

        commandBuffer.Transition(Color, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.DontCare(Color)], null);
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(buffer, 0);
        commandBuffer.Draw(3, 1, 0, 0);
        commandBuffer.EndRenderPass();
        commandBuffer.Transition(Color, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
    }

    public void Resize(RenderDesc desc)
    {
        if (Color is not null && Color.Desc.Width == desc.OutputWidth && Color.Desc.Height == desc.OutputHeight)
        {
            return;
        }

        DestroyTexture();
        CreateTexture(desc.OutputWidth, desc.OutputHeight);
    }

    protected override void Destroy()
    {
        DestroyTexture();

        pipeline?.Dispose();
        buffer?.Dispose();
    }

    private void CreateTexture(uint width, uint height)
    {
        Color = App.Context.CreateTexture(TextureDesc.ColorAttachment(PixelFormat.R8G8B8A8UNorm, width, height, 1, SampleCount.Count1));
    }

    private void DestroyTexture()
    {
        Color?.Dispose();
        Color = null!;
    }
}
