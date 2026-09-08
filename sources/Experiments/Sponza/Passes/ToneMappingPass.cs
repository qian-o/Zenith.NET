using System.Runtime.InteropServices;
using Sponza.Helpers;
using Sponza.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace Sponza.Passes;

internal class ToneMappingPass : IDisposable
{
    private readonly Buffer constants;
    private readonly Sampler sampler;
    private readonly GraphicsPipeline pipeline;

    public ToneMappingPass(GraphicsContext context)
    {
        constants = GraphicsHelper.CreateConstantBuffer<ToneMappingConstants>(context);
        sampler = context.CreateSampler(SamplerDesc.PointClamp());

        using Shader vertex = GraphicsHelper.LoadShader(context, "ToneMapping.slang", "VSMain");
        using Shader fragment = GraphicsHelper.LoadShader(context, "ToneMapping.slang", "FSMain");

        pipeline = context.CreateGraphicsPipeline(new()
        {
            VertexShader = vertex,
            FragmentShader = fragment,
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
    }

    public void Record(CommandBuffer commandBuffer, ToneMappingPassArgs args)
    {
        GraphicsHelper.Upload<ToneMappingConstants>(constants, new()
        {
            HdrColor = args.HdrColor.SampledHandle,
            Sampler = sampler.Handle,
            Exposure = args.Exposure,
            Padding0 = 0.0f,
            Padding1 = 0.0f,
            Padding2 = 0.0f
        });

        commandBuffer.Transition(args.Target, default, args.TargetLayout, TextureLayout.ColorAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.DontCare(args.Target)], null);
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(constants, 0);
        commandBuffer.Draw(3, 1, 0, 0);
        commandBuffer.EndRenderPass();
        commandBuffer.Transition(args.Target, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
    }

    public void Dispose()
    {
        pipeline.Dispose();
        sampler.Dispose();
        constants.Dispose();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 32)]
file struct ToneMappingConstants
{
    [FieldOffset(0)]
    public ResourceHandle HdrColor;

    [FieldOffset(8)]
    public ResourceHandle Sampler;

    [FieldOffset(16)]
    public float Exposure;

    [FieldOffset(20)]
    public float Padding0;

    [FieldOffset(24)]
    public float Padding1;

    [FieldOffset(28)]
    public float Padding2;
}
