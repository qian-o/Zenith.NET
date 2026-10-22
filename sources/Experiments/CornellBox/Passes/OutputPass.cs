using System.Runtime.InteropServices;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Passes;

internal unsafe class OutputPass : DisposableObject
{
    private readonly Buffer buffer;
    private readonly GraphicsPipeline pipeline;
    private readonly GraphicsPipeline compositePipeline;

    private Buffer[] compositeBuffers = [];
    private Texture[] targets = [];
    private Texture[] colors = [];
    private int colorCount;

    public OutputPass(uint width, uint height)
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
        using Shader compositeShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "CompositeFS"));

        GraphicsPipelineDesc desc = new()
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
        };

        pipeline = App.Context.CreateGraphicsPipeline(desc);
        compositePipeline = App.Context.CreateGraphicsPipeline(desc with
        {
            FragmentShader = compositeShader,
            AttachmentFormats = new()
            {
                ColorFormats = [PixelFormat.B8G8R8A8UNorm],
                SampleCount = SampleCount.Count1
            }
        });

        CreateTextures(width, height);
        CreateOutputs(1);
    }

    public Texture SceneColor { get; private set; } = null!;

    public Texture Overlay { get; private set; } = null!;

    public ReadOnlySpan<Texture> Colors => colors.AsSpan(0, colorCount);

    public void Record(CommandBuffer commandBuffer, Texture color, uint frameIndex)
    {
        OutputConstants constants = new()
        {
            FrameIndex = frameIndex,
            Input = color.SampledHandle
        };

        buffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(OutputConstants)
        });

        commandBuffer.Transition(SceneColor, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.DontCare(SceneColor)], null);
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(buffer, 0);
        commandBuffer.Draw(3, 1, 0, 0);
        commandBuffer.EndRenderPass();
        commandBuffer.Transition(SceneColor, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
    }

    public void Record(CommandBuffer commandBuffer, ReadOnlySpan<Texture> inputs)
    {
        for (int i = 0; i < inputs.Length; i++)
        {
            OutputConstants constants = new()
            {
                Input = inputs[i].SampledHandle,
                Overlay = Overlay.SampledHandle
            };

            compositeBuffers[i].Upload(0, new()
            {
                Pointer = (nint)(&constants),
                SizeInBytes = (uint)sizeof(OutputConstants)
            });

            commandBuffer.Transition(targets[i], default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
            commandBuffer.BeginRenderPass([ColorAttachment.DontCare(targets[i])], null);
            commandBuffer.SetPipeline(compositePipeline);
            commandBuffer.SetConstantBuffer(compositeBuffers[i], 0);
            commandBuffer.Draw(3, 1, 0, 0);
            commandBuffer.EndRenderPass();
            colors[i] = targets[i];
        }

        colorCount = inputs.Length;
    }

    public void SelectOutputs(ReadOnlySpan<int> indices)
    {
        for (int i = 0; i < indices.Length; i++)
        {
            colors[i] = targets[indices[i]];
        }

        colorCount = indices.Length;
    }

    public void Resize(uint width, uint height)
    {
        Resize(width, height, targets.Length);
    }

    public void Resize(uint width, uint height, int outputCount)
    {
        if (SceneColor.Desc.Width != width || SceneColor.Desc.Height != height)
        {
            DestroyTextures();
            CreateTextures(width, height);
            CreateOutputs(outputCount);
        }
        else if (targets.Length != outputCount)
        {
            DestroyOutputs();
            CreateOutputs(outputCount);
        }
    }

    protected override void Destroy()
    {
        DestroyTextures();

        compositePipeline?.Dispose();
        pipeline?.Dispose();
        buffer?.Dispose();
    }

    private void CreateTextures(uint width, uint height)
    {
        SceneColor = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R8G8B8A8UNorm, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.ColorAttachment | TextureUsages.Sampled
        });

        Overlay = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.B8G8R8A8UNorm, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.ColorAttachment | TextureUsages.Sampled
        });
    }

    private void DestroyTextures()
    {
        DestroyOutputs();

        Overlay?.Dispose();
        Overlay = null!;
        SceneColor?.Dispose();
        SceneColor = null!;
    }

    private void CreateOutputs(int outputCount)
    {
        targets = new Texture[outputCount];
        colors = new Texture[outputCount];
        compositeBuffers = new Buffer[outputCount];
        colorCount = outputCount;

        for (int i = 0; i < outputCount; i++)
        {
            targets[i] = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.B8G8R8A8UNorm, SceneColor.Desc.Width, SceneColor.Desc.Height, 1, SampleCount.Count1) with
            {
                Usages = TextureUsages.ColorAttachment | TextureUsages.TransferSrc
            });
            colors[i] = targets[i];
        }

        for (int i = 0; i < outputCount; i++)
        {
            compositeBuffers[i] = App.Context.CreateBuffer(new()
            {
                SizeInBytes = (uint)sizeof(OutputConstants),
                Usages = BufferUsages.Constant,
                Residency = MemoryResidency.CpuWriteOnly
            });
        }
    }

    private void DestroyOutputs()
    {
        for (int i = compositeBuffers.Length - 1; i >= 0; i--)
        {
            compositeBuffers[i]?.Dispose();
        }
        compositeBuffers = [];

        for (int i = targets.Length - 1; i >= 0; i--)
        {
            targets[i]?.Dispose();
        }
        targets = [];
        colors = [];
        colorCount = 0;
    }
}

[StructLayout(LayoutKind.Explicit, Size = 32)]
file struct OutputConstants
{
    [FieldOffset(0)]
    public uint FrameIndex;

    [FieldOffset(16)]
    public ResourceHandle Input;

    [FieldOffset(24)]
    public ResourceHandle Overlay;
}
