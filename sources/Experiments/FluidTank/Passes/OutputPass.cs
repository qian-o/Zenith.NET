using System.Numerics;
using System.Runtime.InteropServices;
using FluidTank.Helpers;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank.Passes;

internal unsafe class OutputPass : IDisposable
{
    private readonly GraphicsContext context;

    private readonly Buffer constants;

    private readonly Sampler sampler;

    private readonly GraphicsPipeline toneMappingPipeline;

    private readonly GraphicsPipeline antialiasingPipeline;

    private Texture displayColor = null!;

    public OutputPass(GraphicsContext context)
    {
        this.context = context;

        constants = GraphicsHelper.CreateConstantBuffer(context, 512);
        sampler = context.CreateSampler(SamplerDesc.LinearClamp());

        AttachmentFormats formats = new() { ColorFormats = [PixelFormat.B8G8R8A8UNorm], SampleCount = SampleCount.Count1 };
        toneMappingPipeline = GraphicsHelper.CreateGraphicsPipeline(context, "Output.slang", "FullscreenVS", "ToneMappingFS", [], formats, RasterizerState.CullNone(), DepthStencilState.DepthNone(), BlendState.Opaque());
        antialiasingPipeline = GraphicsHelper.CreateGraphicsPipeline(context, "Output.slang", "FullscreenVS", "AntialiasingFS", [], formats, RasterizerState.CullNone(), DepthStencilState.DepthNone(), BlendState.Opaque());
    }

    public void Resize(uint width, uint height)
    {
        displayColor?.Dispose();
        displayColor = GraphicsHelper.CreateTexture(context, PixelFormat.B8G8R8A8UNorm, width, height, TextureUsages.ColorAttachment | TextureUsages.Sampled);
    }

    public void Render(CommandBuffer commandBuffer, Texture input, Texture output, float exposure, bool antialiasing)
    {
        OutputConstants toneMapping = new()
        {
            TexelSize = new(1.0f / output.Desc.Width, 1.0f / output.Desc.Height),
            Exposure = exposure,
            EncodeLuminance = antialiasing ? 1u : 0u,
            Input = input.SampledHandle,
            Sampler = sampler.Handle
        };
        GraphicsHelper.Upload(constants, 0, &toneMapping, (uint)sizeof(OutputConstants));

        Draw(commandBuffer, toneMappingPipeline, antialiasing ? displayColor : output, 0);

        if (antialiasing)
        {
            OutputConstants filter = toneMapping;
            filter.Input = displayColor.SampledHandle;
            GraphicsHelper.Upload(constants, 256, &filter, (uint)sizeof(OutputConstants));

            Draw(commandBuffer, antialiasingPipeline, output, 256);
        }
    }

    public void Dispose()
    {
        displayColor?.Dispose();
        antialiasingPipeline.Dispose();
        toneMappingPipeline.Dispose();
        sampler.Dispose();
        constants.Dispose();
    }

    private void Draw(CommandBuffer commandBuffer, GraphicsPipeline pipeline, Texture output, uint offset)
    {
        commandBuffer.Transition(output, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.DontCare(output)], null);
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(constants, offset);
        commandBuffer.Draw(3, 1, 0, 0);
        commandBuffer.EndRenderPass();
        commandBuffer.Transition(output, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
    }
}

[StructLayout(LayoutKind.Explicit, Size = 32)]
file struct OutputConstants
{
    [FieldOffset(0)]
    public Vector2 TexelSize;

    [FieldOffset(8)]
    public float Exposure;

    [FieldOffset(12)]
    public uint EncodeLuminance;

    [FieldOffset(16)]
    public ResourceHandle Input;

    [FieldOffset(24)]
    public ResourceHandle Sampler;
}
