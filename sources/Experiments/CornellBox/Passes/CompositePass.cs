using System.Runtime.InteropServices;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class CompositePass : DisposableObject
{
    private readonly GraphicsPipeline pipeline;

    private Buffer[] buffers = [];
    private Texture[] colors = [];

    public CompositePass(uint width, uint height, int outputCount)
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

        CreateOverlay(width, height);
        CreateOutputs(outputCount);
    }

    public Texture Overlay { get; private set; } = null!;

    public ReadOnlySpan<Texture> Colors => colors;

    public void Record(CommandBuffer commandBuffer, ReadOnlySpan<Texture> inputs)
    {
        for (int i = 0; i < inputs.Length; i++)
        {
            CompositeConstants constants = new()
            {
                Input = inputs[i].SampledHandle,
                Overlay = Overlay.SampledHandle
            };

            buffers[i].Upload(0, new()
            {
                Pointer = (nint)(&constants),
                SizeInBytes = (uint)sizeof(CompositeConstants)
            });

            commandBuffer.Transition(colors[i], default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
            commandBuffer.BeginRenderPass([ColorAttachment.DontCare(colors[i])], null);
            commandBuffer.SetPipeline(pipeline);
            commandBuffer.SetConstantBuffer(buffers[i], 0);
            commandBuffer.Draw(3, 1, 0, 0);
            commandBuffer.EndRenderPass();
        }
    }

    public void Resize(uint width, uint height, int outputCount)
    {
        if (Overlay.Desc.Width != width || Overlay.Desc.Height != height)
        {
            DestroyOutputs();
            DestroyOverlay();
            CreateOverlay(width, height);
            CreateOutputs(outputCount);
        }
        else if (colors.Length != outputCount)
        {
            DestroyOutputs();
            CreateOutputs(outputCount);
        }
    }

    protected override void Destroy()
    {
        DestroyOutputs();
        DestroyOverlay();

        pipeline?.Dispose();
    }

    private void CreateOverlay(uint width, uint height)
    {
        Overlay = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.B8G8R8A8UNorm, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.ColorAttachment | TextureUsages.Sampled
        });
    }

    private void DestroyOverlay()
    {
        Overlay?.Dispose();
        Overlay = null!;
    }

    private void CreateOutputs(int outputCount)
    {
        colors = new Texture[outputCount];
        for (int i = 0; i < outputCount; i++)
        {
            colors[i] = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.B8G8R8A8UNorm, Overlay.Desc.Width, Overlay.Desc.Height, 1, SampleCount.Count1) with
            {
                Usages = TextureUsages.ColorAttachment | TextureUsages.TransferSrc
            });
        }

        buffers = new Buffer[outputCount];
        for (int i = 0; i < outputCount; i++)
        {
            buffers[i] = App.Context.CreateBuffer(new()
            {
                SizeInBytes = (uint)sizeof(CompositeConstants),
                Usages = BufferUsages.Constant,
                Residency = MemoryResidency.CpuWriteOnly
            });
        }
    }

    private void DestroyOutputs()
    {
        for (int i = buffers.Length - 1; i >= 0; i--)
        {
            buffers[i]?.Dispose();
        }
        buffers = [];

        for (int i = colors.Length - 1; i >= 0; i--)
        {
            colors[i]?.Dispose();
        }
        colors = [];
    }
}

[StructLayout(LayoutKind.Explicit, Size = 32)]
file struct CompositeConstants
{
    [FieldOffset(16)]
    public ResourceHandle Input;

    [FieldOffset(24)]
    public ResourceHandle Overlay;
}
