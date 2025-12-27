using System.Numerics;
using SponzaScene.Models;
using Zenith.NET;
using Zenith.NET.Extensions.Slang;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer;

internal unsafe class GBufferPass : RenderPass
{
    private readonly Buffer cameraBuffer;
    private readonly Buffer materialBuffer;
    private readonly ResourceLayout layout;
    private readonly Dictionary<string, ResourceSet> sets;
    private readonly GraphicsPipeline pipeline;

    public GBufferPass() : base("GBufferPass")
    {
        cameraBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(CameraConstants),
            StrideInBytes = (uint)sizeof(CameraConstants),
            Flags = BufferUsageFlags.Constant | BufferUsageFlags.MapWrite
        });

        materialBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(MaterialConstants),
            StrideInBytes = (uint)sizeof(MaterialConstants),
            Flags = BufferUsageFlags.Constant
        });

        layout = App.Context.CreateResourceLayout(new()
        {
            Bindings =
            [
                new() { Type = ResourceType.ConstantBuffer, Index = 0, Count = 1, StageFlags = ShaderStageFlags.Vertex },
                new() { Type = ResourceType.ConstantBuffer, Index = 1, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Texture, Index = 0, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Texture, Index = 1, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Sampler, Index = 0, Count = 1, StageFlags = ShaderStageFlags.Pixel }
            ]
        });

        sets = [];
        foreach (Material material in App.Sponza.Materials)
        {
            sets[material.Id] = App.Context.CreateResourceSet(new()
            {
                Layout = layout,
                Resources =
                [
                    cameraBuffer,
                    materialBuffer,
                    material.BaseColorTexture ?? App.FallbackTexture,
                    material.NormalTexture ?? App.FallbackTexture,
                    App.LinearSampler
                ]
            });
        }

        using Shader vs = App.Context.LoadShaderFromFile(GetShaderPath("GBuffer"), "VSMain", ShaderStageFlags.Vertex);
        using Shader ps = App.Context.LoadShaderFromFile(GetShaderPath("GBuffer"), "PSMain", ShaderStageFlags.Pixel);

        pipeline = App.Context.CreateGraphicsPipeline(new()
        {
            RenderStates = new()
            {
                RasterizerState = RasterizerStates.CullBack,
                DepthStencilState = DepthStencilStates.Default,
                BlendState = BlendStates.Opaque
            },
            Vertex = vs,
            Pixel = ps,
            ResourceLayouts = [layout],
            InputLayouts = [Vertex.InputLayout()],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            Output = RenderContext.GBufferOutput
        });
    }

    public override void Execute(CommandBuffer commandBuffer, RenderContext context)
    {
    }

    protected override void Destroy()
    {
        pipeline.Dispose();
        foreach (ResourceSet set in sets.Values)
        {
            set.Dispose();
        }
        layout.Dispose();
        materialBuffer.Dispose();
        cameraBuffer.Dispose();
    }
    private enum TextureFlags
    {
        HasBaseColorTexture = 1 << 0,

        HasNormalTexture = 1 << 1
    }

    private struct CameraConstants
    {
        public Matrix4x4 View;

        public Matrix4x4 Projection;
    }

    private struct MaterialConstants
    {
        public Matrix4x4 BaseColorFactor;

        public TextureFlags Flags;
    }
}
