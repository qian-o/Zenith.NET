using System.Numerics;
using Hexa.NET.ImGui;
using SponzaScene.Helpers;
using SponzaScene.Models;
using Zenith.NET;
using Zenith.NET.Extensions.Slang;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer.Passes;

internal unsafe class CSMPass : RenderPass
{
    private readonly Buffer argsBuffer;
    private readonly Buffer dataBuffer;
    private readonly ResourceLayout resourceLayout;
    private readonly ResourceTable resourceTable;
    private readonly GraphicsPipeline pipeline;

    public CSMPass() : base("CSM Pass")
    {
        argsBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(IndirectDrawIndexedArgs) * App.Sponza.Nodes.Length),
            StrideInBytes = (uint)sizeof(IndirectDrawIndexedArgs),
            Flags = BufferUsageFlags.Indirect
        });
        argsBuffer.Upload([.. App.Sponza.Nodes.Select(static item => item.Args)], 0);

        dataBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(CSMData),
            StrideInBytes = (uint)sizeof(CSMData),
            Flags = BufferUsageFlags.Constant
        });

        resourceLayout = App.Context.CreateResourceLayout(new()
        {
            Bindings = Bindings
            (
                new ResourceBinding() { Type = ResourceType.ConstantBuffer, Count = 1, StageFlags = ShaderStageFlags.Vertex | ShaderStageFlags.Pixel }
            )
        });

        resourceTable = App.Context.CreateResourceTable(new()
        {
            Layout = resourceLayout,
            Resources = [dataBuffer]
        });

        using Shader vs = App.Context.LoadShaderFromFile(GetShaderPath("CSM"), "VSMain", ShaderStageFlags.Vertex);
        using Shader ps = App.Context.LoadShaderFromFile(GetShaderPath("CSM"), "PSMain", ShaderStageFlags.Pixel);

        InputLayout inputLayout = new()
        {
            Elements = [new() { Format = ElementFormat.Float3, Semantic = ElementSemantic.Position }],
            StrideInBytes = (uint)sizeof(Vertex)
        };

        pipeline = App.Context.CreateGraphicsPipeline(new()
        {
            RenderStates = new()
            {
                RasterizerState = RasterizerStates.CullFront with
                {
                    DepthBias = 250,
                    SlopeScaledDepthBias = 3.0f,
                    DepthBiasClamp = 0.02f
                },
                DepthStencilState = DepthStencilStates.Default,
                BlendState = BlendStates.Opaque
            },
            Vertex = vs,
            Pixel = ps,
            ResourceLayout = resourceLayout,
            InputLayouts = [inputLayout],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            Output = RenderContext.CSMOutput
        });
    }

    protected override void ExecuteImpl(CommandBuffer commandBuffer, RenderContext context)
    {
        UpdateCSMDatas(context);

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetVertexBuffer(App.Sponza.Vertices, 0, 0);
        commandBuffer.SetIndexBuffer(App.Sponza.Indices, 0, IndexFormat.UInt32);
        commandBuffer.SetResourceTable(resourceTable);

        for (int i = 0; i < RenderContext.CSMSplits.Length; i++)
        {
            commandBuffer.Upload(dataBuffer, 0, [context.CSMDatas[i]]);

            commandBuffer.BeginRenderPass(context.CSMFrameBuffers![i], ClearValues.Default);
            commandBuffer.DrawIndexedIndirect(argsBuffer, 0, (uint)App.Sponza.Nodes.Length);
            commandBuffer.EndRenderPass();
        }
    }

    protected override void DebugUIImpl(RenderContext context)
    {
        int splitCount = RenderContext.CSMSplits.Length;

        float spacing = ImGui.GetStyle().ItemSpacing.X;

        Vector2 size = new((ImGui.GetContentRegionAvail().X - (spacing * (splitCount - 1))) / splitCount);
        size = size with { Y = size.X };

        for (int i = 0; i < splitCount; i++)
        {
            ImGui.BeginGroup();
            ImGui.Text($"Cascade {i}");
            ImGuiHelper.Image(context.CSMTextureViews![i], size);
            ImGui.EndGroup();

            if (i < splitCount - 1)
            {
                ImGui.SameLine();
            }
        }
    }

    public override void Resize(uint width, uint height)
    {
    }

    protected override void Destroy()
    {
        pipeline.Dispose();
        resourceTable.Dispose();
        resourceLayout.Dispose();
        dataBuffer.Dispose();
        argsBuffer.Dispose();

        base.Destroy();
    }

    private static Vector4[] GetFrustumCornersWorldSpace(RenderContext context, float nearPlane, float farPlane)
    {
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(float.DegreesToRadians(context.Fov),
                                                                      context.AspectRatio,
                                                                      nearPlane,
                                                                      farPlane);

        Matrix4x4.Invert(context.View * projection, out Matrix4x4 invVP);

        Vector4[] frustumCorners =
        [
            new(-1,  1, 0, 1),
            new( 1,  1, 0, 1),
            new( 1, -1, 0, 1),
            new(-1, -1, 0, 1),
            new(-1,  1, 1, 1),
            new( 1,  1, 1, 1),
            new( 1, -1, 1, 1),
            new(-1, -1, 1, 1)
        ];

        for (int i = 0; i < frustumCorners.Length; i++)
        {
            Vector4 corner = Vector4.Transform(frustumCorners[i], invVP);
            frustumCorners[i] = corner / corner.W;
        }

        return frustumCorners;
    }

    private static void UpdateCSMDatas(RenderContext context)
    {
        float previousSplitDist = context.NearPlane;

        DirectionalLight dl = App.Sponza.DirectionalLight;

        Vector3 lightDir = Vector3.Normalize(new Vector3(dl.DirectionAndIntensity.X, dl.DirectionAndIntensity.Y, dl.DirectionAndIntensity.Z));

        for (int i = 0; i < RenderContext.CSMSplits.Length; i++)
        {
            float splitDist = context.FarPlane * RenderContext.CSMSplits[i];

            Vector4[] frustumCorners = GetFrustumCornersWorldSpace(context, previousSplitDist, splitDist);

            Vector3 center = Vector3.Zero;
            foreach (Vector4 corner in frustumCorners)
            {
                center += new Vector3(corner.X, corner.Y, corner.Z);
            }
            center /= frustumCorners.Length;

            float radius = 0.0f;
            foreach (Vector4 corner in frustumCorners)
            {
                float distance = Vector3.Distance(new(corner.X, corner.Y, corner.Z), center);
                radius = MathF.Max(radius, distance);
            }
            radius = MathF.Ceiling(radius * 16f) / 16f;

            Vector3 up = MathF.Abs(lightDir.Y) > 0.99f ? Vector3.UnitZ : Vector3.UnitY;

            float shadowDistance = radius * 4.0f;

            Vector3 lightPos = center - (lightDir * shadowDistance);
            Matrix4x4 lightView = Matrix4x4.CreateLookAt(lightPos, center, up);

            float texelSize = radius * 2.0f / 4096.0f;

            Vector3 centerLS = Vector3.Transform(center, lightView);
            centerLS.X = MathF.Floor(centerLS.X / texelSize) * texelSize;
            centerLS.Y = MathF.Floor(centerLS.Y / texelSize) * texelSize;

            Matrix4x4.Invert(lightView, out Matrix4x4 invLightView);

            center = Vector3.Transform(centerLS, invLightView);
            lightPos = center - (lightDir * shadowDistance);
            lightView = Matrix4x4.CreateLookAt(lightPos, center, up);

            Matrix4x4 lightProjection = Matrix4x4.CreateOrthographic(radius * 2f,
                                                                     radius * 2f,
                                                                     0.1f,
                                                                     (shadowDistance * 2.0f) + radius);

            context.CSMDatas[i] = new()
            {
                View = lightView,
                Projection = lightProjection,
                NearPlane = previousSplitDist,
                FarPlane = splitDist
            };

            previousSplitDist = splitDist;
        }
    }
}