using System.Numerics;
using Hexa.NET.ImGui;
using SponzaScene.Helpers;
using SponzaScene.Models;
using Zenith.NET;
using Zenith.NET.Extensions.Slang;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer.Passes;

internal unsafe class GBufferPass : RenderPass
{
    private readonly Buffer cameraBuffer;
    private readonly Buffer materialBuffer;
    private readonly BufferView[] materialBufferViews;
    private readonly ResourceLayout resourceLayout;
    private readonly ResourceTable[] resourceTables;
    private readonly GraphicsPipeline cullBackPipeline;
    private readonly GraphicsPipeline cullNonePipeline;

    public GBufferPass() : base("G-Buffer Pass")
    {
        cameraBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(CameraConstants),
            StrideInBytes = (uint)sizeof(CameraConstants),
            Flags = BufferUsageFlags.Constant | BufferUsageFlags.MapWrite
        });

        materialBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(ZenithHelper.Align((uint)sizeof(MaterialConstants), GraphicsContext.ConstantBufferAlignment) * App.Sponza.Materials.Length),
            StrideInBytes = (uint)sizeof(MaterialConstants),
            Flags = BufferUsageFlags.Constant | BufferUsageFlags.MapWrite
        });

        materialBufferViews = new BufferView[App.Sponza.Materials.Length];

        MappedMemory mappedMemory = materialBuffer.Map();

        for (int i = 0; i < App.Sponza.Materials.Length; i++)
        {
            uint offsetInBytes = (uint)(ZenithHelper.Align((uint)sizeof(MaterialConstants), GraphicsContext.ConstantBufferAlignment) * i);

            Material material = App.Sponza.Materials[i];

            *(MaterialConstants*)(mappedMemory.Pointer + offsetInBytes) = new()
            {
                AlphaCutoff = material.AlphaCutoff,
                MetallicFactor = material.MetallicFactor,
                RoughnessFactor = material.RoughnessFactor,
                EmissiveStrength = material.EmissiveStrength,
                BaseColorFactor = material.BaseColorFactor,
                EmissiveFactor = material.EmissiveFactor,
                Flags = (material.AlphaCutoff > 0 ? MaterialFlags.UseAlphaCutoff : 0)
                        | (material.BaseColorTexture is not null ? MaterialFlags.HasBaseColorTexture : 0)
                        | (material.NormalTexture is not null ? MaterialFlags.HasNormalTexture : 0)
                        | (material.MetallicRoughnessTexture is not null ? MaterialFlags.HasMetallicRoughnessTexture : 0)
            };

            materialBufferViews[i] = App.Context.CreateBufferView(new()
            {
                Buffer = materialBuffer,
                OffsetInBytes = offsetInBytes,
                SizeInBytes = (uint)sizeof(MaterialConstants),
                StrideInBytes = (uint)sizeof(MaterialConstants)
            });
        }

        materialBuffer.Unmap();

        resourceLayout = App.Context.CreateResourceLayout(new()
        {
            Bindings = Bindings
            (
                new() { Type = ResourceType.ConstantBuffer, Count = 1, StageFlags = ShaderStageFlags.Vertex | ShaderStageFlags.Pixel },
                new() { Type = ResourceType.ConstantBuffer, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Texture, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Sampler, Count = 1, StageFlags = ShaderStageFlags.Pixel }
            )
        });

        resourceTables = new ResourceTable[App.Sponza.Materials.Length];
        for (int i = 0; i < App.Sponza.Materials.Length; i++)
        {
            Material material = App.Sponza.Materials[i];

            resourceTables[i] = App.Context.CreateResourceTable(new()
            {
                Layout = resourceLayout,
                Resources =
                [
                    cameraBuffer,
                    materialBufferViews[i],
                    material.BaseColorTexture ?? App.FallbackTexture,
                    material.NormalTexture ?? App.FallbackTexture,
                    material.MetallicRoughnessTexture ?? App.FallbackTexture,
                    App.LinearSampler
                ]
            });
        }

        using Shader vs = App.Context.LoadShaderFromFile(GetShaderPath("GBuffer"), "VSMain", ShaderStageFlags.Vertex);
        using Shader ps = App.Context.LoadShaderFromFile(GetShaderPath("GBuffer"), "PSMain", ShaderStageFlags.Pixel);

        cullBackPipeline = App.Context.CreateGraphicsPipeline(new()
        {
            RenderStates = new()
            {
                RasterizerState = RasterizerStates.CullBack,
                DepthStencilState = DepthStencilStates.Default,
                BlendState = BlendStates.Opaque
            },
            Vertex = vs,
            Pixel = ps,
            ResourceLayout = resourceLayout,
            InputLayouts = [Vertex.InputLayout()],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            Output = RenderContext.GBufferOutput
        });

        cullNonePipeline = App.Context.CreateGraphicsPipeline(new()
        {
            RenderStates = new()
            {
                RasterizerState = RasterizerStates.CullNone,
                DepthStencilState = DepthStencilStates.Default,
                BlendState = BlendStates.Opaque
            },
            Vertex = vs,
            Pixel = ps,
            ResourceLayout = resourceLayout,
            InputLayouts = [Vertex.InputLayout()],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            Output = RenderContext.GBufferOutput
        });
    }

    public override void Resize(uint width, uint height)
    {
    }

    protected override void ExecuteImpl(CommandBuffer commandBuffer, RenderContext context)
    {
        cameraBuffer.Upload([new CameraConstants()
        {
            View = context.View,
            Projection = context.Projection,
            NearPlane = context.NearPlane,
            FarPlane = context.FarPlane
        }], 0);

        commandBuffer.BeginRenderPass(context.GBufferFrameBuffer!, ClearValues.Default, resourceTables);

        commandBuffer.SetPipeline(cullBackPipeline);
        commandBuffer.SetVertexBuffer(App.Sponza.Vertices, 0, 0);
        commandBuffer.SetIndexBuffer(App.Sponza.Indices, 0, IndexFormat.UInt32);

        foreach (Node node in App.Sponza.Nodes)
        {
            if (App.Sponza.Materials[node.Material].DoubleSided)
            {
                continue;
            }

            commandBuffer.SetResourceTable(resourceTables[node.Material]);

            commandBuffer.DrawIndexed(node.Args.IndexCount,
                                       node.Args.InstanceCount,
                                       node.Args.FirstIndex,
                                       node.Args.VertexOffset,
                                       node.Args.FirstInstance);
        }

        commandBuffer.SetPipeline(cullNonePipeline);
        commandBuffer.SetVertexBuffer(App.Sponza.Vertices, 0, 0);
        commandBuffer.SetIndexBuffer(App.Sponza.Indices, 0, IndexFormat.UInt32);

        foreach (Node node in App.Sponza.Nodes)
        {
            if (!App.Sponza.Materials[node.Material].DoubleSided)
            {
                continue;
            }

            commandBuffer.SetResourceTable(resourceTables[node.Material]);

            commandBuffer.DrawIndexed(node.Args.IndexCount,
                                      node.Args.InstanceCount,
                                      node.Args.FirstIndex,
                                      node.Args.VertexOffset,
                                      node.Args.FirstInstance);
        }

        commandBuffer.EndRenderPass();
    }

    protected override void DebugUIImpl(RenderContext context)
    {
        Vector2 size = new((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 3.0f);
        size = size with { Y = size.X * context.Height / context.Width };

        ImGui.BeginGroup();
        ImGui.Text("Albedo");
        ImGuiHelper.Image(context.Albedo!, size);
        ImGui.EndGroup();

        ImGui.SameLine();

        ImGui.BeginGroup();
        ImGui.Text("Normal");
        ImGuiHelper.Image(context.Normal!, size);
        ImGui.EndGroup();

        ImGui.SameLine();

        ImGui.BeginGroup();
        ImGui.Text("Position");
        ImGuiHelper.Image(context.Position!, size);
        ImGui.EndGroup();

        ImGui.BeginGroup();
        ImGui.Text("Depth");
        ImGuiHelper.Image(context.NormalizedDepth!, size);
        ImGui.EndGroup();

        ImGui.SameLine();

        ImGui.BeginGroup();
        ImGui.Text("Metallic Roughness");
        ImGuiHelper.Image(context.MetallicRoughness!, size);
        ImGui.EndGroup();

        ImGui.SameLine();

        ImGui.BeginGroup();
        ImGui.Text("Emissive");
        ImGuiHelper.Image(context.Emissive!, size);
        ImGui.EndGroup();
    }

    protected override void Destroy()
    {
        cullNonePipeline.Dispose();
        cullBackPipeline.Dispose();

        foreach (ResourceTable resourceTable in resourceTables)
        {
            resourceTable.Dispose();
        }

        resourceLayout.Dispose();

        foreach (BufferView view in materialBufferViews)
        {
            view.Dispose();
        }

        materialBuffer.Dispose();
        cameraBuffer.Dispose();

        base.Destroy();
    }
}

[Flags]
file enum MaterialFlags
{
    None = 0,

    UseAlphaCutoff = 1 << 0,

    HasBaseColorTexture = 1 << 1,

    HasNormalTexture = 1 << 2,

    HasMetallicRoughnessTexture = 1 << 3
}

file struct CameraConstants
{
    public Matrix4x4 View;

    public Matrix4x4 Projection;

    public float NearPlane;

    public float FarPlane;

    public float _pad0;

    public float _pad1;
}

file struct MaterialConstants
{
    public float AlphaCutoff;

    public float MetallicFactor;

    public float RoughnessFactor;

    public float EmissiveStrength;

    public Vector4 BaseColorFactor;

    public Vector4 EmissiveFactor;

    public MaterialFlags Flags;

    public int _pad0;

    public int _pad1;

    public int _pad2;
}
