using System.Numerics;
using Hexa.NET.ImGui;
using SponzaScene.Models;
using Zenith.NET;
using Zenith.NET.Extensions.Slang;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer;

internal unsafe class CSMPass : RenderPass
{
    private readonly Buffer argsBuffer;
    private readonly Buffer dataBuffer;
    private readonly ResourceLayout resourceLayout;
    private readonly ResourceSet resourceSet;
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

        resourceSet = App.Context.CreateResourceSet(new()
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
                RasterizerState = RasterizerStates.CullNone with
                {
                    DepthBias = 100,
                    SlopeScaledDepthBias = 2.0f
                },
                DepthStencilState = DepthStencilStates.Default,
                BlendState = BlendStates.Opaque
            },
            Vertex = vs,
            Pixel = ps,
            ResourceLayouts = [resourceLayout],
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
        commandBuffer.SetResourceSet(resourceSet, 0);

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
            ImGui.Image(App.Binding(context.CSMNormalizedDepths![i]), size);
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
        resourceSet.Dispose();
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

        Vector3 lightDir = Vector3.Normalize(App.Sponza.DirectionalLight.Direction);

        for (int i = 0; i < RenderContext.CSMSplits.Length; i++)
        {
            float splitDist = context.FarPlane * RenderContext.CSMSplits[i];

            Vector4[] frustumCorners = GetFrustumCornersWorldSpace(context, previousSplitDist, splitDist);

            // 计算视锥体中心
            Vector3 center = Vector3.Zero;
            foreach (Vector4 corner in frustumCorners)
            {
                center += new Vector3(corner.X, corner.Y, corner.Z);
            }
            center /= frustumCorners.Length;

            // 计算视锥体的包围球半径
            float radius = 0f;
            foreach (Vector4 corner in frustumCorners)
            {
                float distance = Vector3.Distance(new(corner.X, corner.Y, corner.Z), center);
                radius = MathF.Max(radius, distance);
            }
            radius = MathF.Ceiling(radius * 16f) / 16f;

            // 选择合适的 up 向量
            Vector3 up = MathF.Abs(lightDir.Y) > 0.99f ? Vector3.UnitZ : Vector3.UnitY;

            // 计算光源视图矩阵
            Vector3 lightPos = center - (lightDir * radius);
            Matrix4x4 lightView = Matrix4x4.CreateLookAt(lightPos, center, up);

            // 使用包围球创建正交投影（保证覆盖整个视锥体）
            float texelsPerUnit = 4096f / (radius * 2f);

            // 对齐到纹素网格以减少阴影抖动
            Vector3 centerLS = Vector3.Transform(center, lightView);
            centerLS.X = MathF.Floor(centerLS.X * texelsPerUnit) / texelsPerUnit;
            centerLS.Y = MathF.Floor(centerLS.Y * texelsPerUnit) / texelsPerUnit;

            Matrix4x4.Invert(lightView, out Matrix4x4 invLightView);
            center = Vector3.Transform(centerLS, invLightView);
            lightPos = center - (lightDir * radius);
            lightView = Matrix4x4.CreateLookAt(lightPos, center, up);

            // 创建正交投影矩阵
            Matrix4x4 lightProjection = Matrix4x4.CreateOrthographic(radius * 2f,
                                                                     radius * 2f,
                                                                     0.0f,
                                                                     (radius * 2f) + 50f);

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