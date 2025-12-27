using System.Numerics;
using Hexa.NET.ImGui;
using SponzaScene.Models;
using Zenith.NET;
using Zenith.NET.Extensions.Slang;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer;

internal unsafe class GBufferPass : RenderPass
{
    private readonly Buffer cameraBuffer;
    private readonly Buffer materialBuffer;
    private readonly BufferView[] materialBufferViews;
    private readonly ResourceLayout resourceLayout;
    private readonly ResourceSet[] resourceSets;
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
            SizeInBytes = (uint)(sizeof(MaterialConstants) * App.Sponza.Materials.Length),
            StrideInBytes = (uint)sizeof(MaterialConstants),
            Flags = BufferUsageFlags.Constant
        });
        materialBuffer.Upload([.. App.Sponza.Materials.Select(static item => new MaterialConstants()
        {
            BaseColorFactor = item.BaseColorFactor,
            Flags = (item.BaseColorTexture is not null ? TextureFlags.HasBaseColorTexture : 0)
                    | (item.NormalTexture is not null ? TextureFlags.HasNormalTexture : 0)
        })], 0);

        materialBufferViews = new BufferView[App.Sponza.Materials.Length];
        for (int i = 0; i < App.Sponza.Materials.Length; i++)
        {
            materialBufferViews[i] = App.Context.CreateBufferView(new()
            {
                Buffer = materialBuffer,
                OffsetInBytes = (uint)(sizeof(MaterialConstants) * i),
                SizeInBytes = (uint)sizeof(MaterialConstants),
                StrideInBytes = (uint)sizeof(MaterialConstants)
            });
        }

        resourceLayout = App.Context.CreateResourceLayout(new()
        {
            Bindings =
            [
                new() { Type = ResourceType.ConstantBuffer, Index = 0, Count = 1, StageFlags = ShaderStageFlags.Vertex | ShaderStageFlags.Pixel },
                new() { Type = ResourceType.ConstantBuffer, Index = 1, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Texture, Index = 0, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Texture, Index = 1, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Sampler, Index = 0, Count = 1, StageFlags = ShaderStageFlags.Pixel }
            ]
        });

        resourceSets = new ResourceSet[App.Sponza.Materials.Length];
        for (int i = 0; i < App.Sponza.Materials.Length; i++)
        {
            Material material = App.Sponza.Materials[i];

            resourceSets[i] = App.Context.CreateResourceSet(new()
            {
                Layout = resourceLayout,
                Resources =
                [
                    cameraBuffer,
                    materialBufferViews[i],
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
            ResourceLayouts = [resourceLayout],
            InputLayouts = [Vertex.InputLayout()],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            Output = RenderContext.GBufferOutput
        });
    }

    public override void Execute(CommandBuffer commandBuffer, RenderContext context)
    {
        if (context.GBufferFrameBuffer is null)
        {
            return;
        }

        cameraBuffer.Upload([new CameraConstants()
        {
            View = context.View,
            Projection = context.Projection,
            NearPlane = context.NearPlane,
            FarPlane = context.FarPlane
        }], 0);

        commandBuffer.PreprocessResourceSets(resourceSets);

        commandBuffer.BindPipeline(pipeline);
        commandBuffer.BindFrameBuffer(context.GBufferFrameBuffer, ClearValues.Default);
        commandBuffer.BindVertexBuffer(App.Sponza.Vertices, 0, 0);
        commandBuffer.BindIndexBuffer(App.Sponza.Indices, 0, IndexFormat.UInt32);

        foreach (Node node in App.Sponza.Nodes)
        {
            commandBuffer.BindResourceSet(resourceSets[node.Material], 0);

            commandBuffer.DrawIndexed(node.Args.IndexCount,
                                      node.Args.InstanceCount,
                                      node.Args.FirstIndex,
                                      node.Args.VertexOffset,
                                      node.Args.FirstInstance);
        }
    }

    public override void DebugUI(RenderContext context)
    {
        if (ImGui.Begin("G-Buffer Textures", ImGuiWindowFlags.AlwaysAutoResize))
        {
            Vector2 size = new(400, 400 * context.Height / context.Width);

            ImGui.BeginGroup();
            ImGui.Text("Albedo");
            ImGui.Image(App.Binding(context.Albedo!), size);
            ImGui.EndGroup();

            ImGui.SameLine();

            ImGui.BeginGroup();
            ImGui.Text("Normal");
            ImGui.Image(App.Binding(context.Normal!), size);
            ImGui.EndGroup();

            ImGui.BeginGroup();
            ImGui.Text("Position");
            ImGui.Image(App.Binding(context.Position!), size);
            ImGui.EndGroup();

            ImGui.SameLine();

            ImGui.BeginGroup();
            ImGui.Text("Depth");
            ImGui.Image(App.Binding(context.LinearDepth!), size);
            ImGui.EndGroup();
        }
        ImGui.End();
    }

    protected override void Destroy()
    {
        pipeline.Dispose();
        foreach (ResourceSet set in resourceSets)
        {
            set.Dispose();
        }
        resourceLayout.Dispose();
        foreach (BufferView view in materialBufferViews)
        {
            view.Dispose();
        }
        materialBuffer.Dispose();
        cameraBuffer.Dispose();
    }

    [Flags]
    private enum TextureFlags
    {
        None = 0,

        HasBaseColorTexture = 1 << 0,

        HasNormalTexture = 1 << 1
    }

    private struct CameraConstants
    {
        public Matrix4x4 View;

        public Matrix4x4 Projection;

        public float NearPlane;

        public float FarPlane;
    }

    private struct MaterialConstants
    {
        public Vector4 BaseColorFactor;

        public TextureFlags Flags;
    }
}
