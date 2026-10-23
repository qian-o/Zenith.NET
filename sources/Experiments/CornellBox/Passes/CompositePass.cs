using CornellBox.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class CompositePass : DisposableObject
{
    private readonly GraphicsPipeline pipeline;

    private Buffer buffer = null!;
    private Texture[] colors = [];

    public CompositePass(RenderDesc desc)
    {
        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "Output.slang");
        using Shader vertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "FullscreenVS"));
        using Shader fragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "CompositeFS"));

        pipeline = App.Context.CreateGraphicsPipeline(new()
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            InputLayouts = [],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            AttachmentFormats = new()
            {
                ColorFormats = [PixelFormat.B8G8R8A8UNorm],
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

    public Texture Overlay { get; private set; } = null!;

    public ReadOnlySpan<Texture> Colors => colors;

    public void Record(CommandBuffer commandBuffer, in RenderArgs args)
    {
        for (int i = 0; i < colors.Length; i++)
        {
            Texture input = i < args.Generated.Length ? args.Generated[i] : args.Color;
            uint offset = (uint)(sizeof(OutputConstants) * i);
            OutputConstants constants = new()
            {
                Input = input.SampledHandle,
                Overlay = Overlay.SampledHandle
            };

            buffer.Upload(offset, new()
            {
                Pointer = (nint)(&constants),
                SizeInBytes = (uint)sizeof(OutputConstants)
            });

            commandBuffer.Transition(colors[i], default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
            commandBuffer.BeginRenderPass([ColorAttachment.DontCare(colors[i])], null);
            commandBuffer.SetPipeline(pipeline);
            commandBuffer.SetConstantBuffer(buffer, offset);
            commandBuffer.Draw(3, 1, 0, 0);
            commandBuffer.EndRenderPass();
        }
    }

    public void Resize(RenderDesc desc)
    {
        int count = (int)desc.FrameGeneration + 1;
        bool isSizeChanged = Overlay is null || Overlay.Desc.Width != desc.OutputWidth || Overlay.Desc.Height != desc.OutputHeight;
        if (!isSizeChanged && colors.Length == count)
        {
            return;
        }

        DestroyOutputs();

        if (isSizeChanged)
        {
            DestroyOverlay();
            CreateOverlay(desc.OutputWidth, desc.OutputHeight);
        }

        CreateOutputs(count);
    }

    protected override void Destroy()
    {
        DestroyOutputs();
        DestroyOverlay();

        pipeline?.Dispose();
    }

    private void CreateOverlay(uint width, uint height)
    {
        Overlay = App.Context.CreateTexture(TextureDesc.ColorAttachment(PixelFormat.B8G8R8A8UNorm, width, height, 1, SampleCount.Count1));
    }

    private void DestroyOverlay()
    {
        Overlay?.Dispose();
        Overlay = null!;
    }

    private void CreateOutputs(int count)
    {
        colors = new Texture[count];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = App.Context.CreateTexture(Overlay.Desc with
            {
                Usages = TextureUsages.ColorAttachment | TextureUsages.TransferSrc
            });
        }

        buffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(OutputConstants) * colors.Length),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
    }

    private void DestroyOutputs()
    {
        buffer?.Dispose();
        buffer = null!;

        for (int i = colors.Length - 1; i >= 0; i--)
        {
            colors[i]?.Dispose();
        }
        colors = [];
    }
}
