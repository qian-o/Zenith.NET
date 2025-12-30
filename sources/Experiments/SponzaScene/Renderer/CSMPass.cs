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
    private readonly Buffer constantsBuffer;
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

        constantsBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(CSMConstants),
            StrideInBytes = (uint)sizeof(CSMConstants),
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
            Resources = [constantsBuffer]
        });

        using Shader vs = App.Context.LoadShaderFromFile(GetShaderPath("CSM"), "VSMain", ShaderStageFlags.Vertex);
        using Shader ps = App.Context.LoadShaderFromFile(GetShaderPath("CSM"), "PSMain", ShaderStageFlags.Pixel);

        pipeline = App.Context.CreateGraphicsPipeline(new()
        {
            RenderStates = new()
            {
                RasterizerState = RasterizerStates.CullNone,
                DepthStencilState = DepthStencilStates.Default,
                BlendState = BlendStates.Opaque
            },
            Vertex = vs,
            Pixel = ps,
            ResourceLayouts = [resourceLayout],
            InputLayouts = [Vertex.InputLayout()],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            Output = RenderContext.CSMOutput
        });
    }

    protected override void ExecuteImpl(CommandBuffer commandBuffer, RenderContext context)
    {
        CSMConstants[] constants = GetCSMConstants(context);

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetVertexBuffer(App.Sponza.Vertices, 0, 0);
        commandBuffer.SetIndexBuffer(App.Sponza.Indices, 0, IndexFormat.UInt32);
        commandBuffer.SetResourceSet(resourceSet, 0);

        for (int i = 0; i < RenderContext.CSMSplits.Length; i++)
        {
            commandBuffer.Upload(constantsBuffer, 0, [constants[i]]);

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
        constantsBuffer.Dispose();
        argsBuffer.Dispose();

        base.Destroy();
    }

    private static Vector4[] GetFrustumCornersViewSpace(RenderContext context, float nearPlane, float farPlane)
    {
        Matrix4x4 vp = context.View * Matrix4x4.CreatePerspectiveFieldOfView(float.DegreesToRadians(context.Fov), context.AspectRatio, nearPlane, farPlane);

        Matrix4x4.Invert(vp, out Matrix4x4 invVP);

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

    private static CSMConstants[] GetCSMConstants(RenderContext context)
    {
        CSMConstants[] constants = new CSMConstants[RenderContext.CSMSplits.Length];

        float previousSplitDist = context.NearPlane;

        for (int i = 0; i < RenderContext.CSMSplits.Length; i++)
        {
            float splitDist = context.FarPlane * RenderContext.CSMSplits[i];

            Vector4[] frustumCorners = GetFrustumCornersViewSpace(context, previousSplitDist, splitDist);

            Vector3 center = default;
            foreach (Vector4 corner in frustumCorners)
            {
                center += new Vector3(corner.X, corner.Y, corner.Z);
            }
            center /= frustumCorners.Length;

            Matrix4x4 lightView = Matrix4x4.CreateLookAt(center - App.Sponza.DirectionalLight.Direction, center, Vector3.UnitY);

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            float minZ = float.MaxValue;
            float maxZ = float.MinValue;
            foreach (Vector4 corner in frustumCorners)
            {
                Vector4 cornerLS = Vector4.Transform(corner, lightView);

                minX = MathF.Min(minX, cornerLS.X);
                maxX = MathF.Max(maxX, cornerLS.X);
                minY = MathF.Min(minY, cornerLS.Y);
                maxY = MathF.Max(maxY, cornerLS.Y);
                minZ = MathF.Min(minZ, cornerLS.Z);
                maxZ = MathF.Max(maxZ, cornerLS.Z);
            }

            const float zMult = 10.0f;

            if (minZ < 0)
            {
                minZ *= zMult;
            }
            else
            {
                minZ /= zMult;
            }

            if (maxZ < 0)
            {
                maxZ /= zMult;
            }
            else
            {
                maxZ *= zMult;
            }

            Matrix4x4 lightProjection = Matrix4x4.CreateOrthographicOffCenter(minX, maxX, minY, maxY, minZ, maxZ);

            constants[i] = new()
            {
                View = lightView,
                Projection = lightProjection,
                NearPlane = previousSplitDist,
                FarPlane = splitDist
            };

            previousSplitDist = splitDist;
        }

        return constants;
    }

    private struct CSMConstants
    {
        public Matrix4x4 View;

        public Matrix4x4 Projection;

        public float NearPlane;

        public float FarPlane;
    }
}
