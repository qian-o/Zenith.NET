using System.Numerics;
using System.Runtime.InteropServices;
using CornellBox.Handlers;
using CornellBox.Helpers;
using Zenith.NET;
using Zenith.NET.Extensions.Slang;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Renderers;

internal unsafe class RasterizationRenderer : Renderer
{
    private const string ShaderSource = """
        struct Material
        {
            private float4 AlbedoAndEmission;

            property float3 Albedo { get { return AlbedoAndEmission.xyz; } }

            property float Emission { get { return AlbedoAndEmission.w; } }
        };

        struct RasterConstants
        {
            float4x4 Model;

            float4x4 View;

            float4x4 Projection;

            private float4 LightPosAndPadding;

            private float4 LightColorAndPadding;

            private float4 CameraPosAndPadding;

            property float3 LightPos { get { return LightPosAndPadding.xyz; } }

            property float3 LightColor { get { return LightColorAndPadding.xyz; } }

            property float3 CameraPos { get { return CameraPosAndPadding.xyz; } }
        };

        ConstantBuffer<RasterConstants> cb;
        StructuredBuffer<Material> materials;

        struct VSInput
        {
            float4 Position : POSITION0;

            float4 NormalAndMaterialID : NORMAL0;
        };

        struct PSInput
        {
            float4 Position : SV_POSITION;

            float3 WorldPos : TEXCOORD0;

            float3 Normal : TEXCOORD1;

            nointerpolation uint MaterialID : TEXCOORD2;
        };

        PSInput VSMain(VSInput input)
        {
            float4 worldPos = mul(float4(input.Position.xyz, 1.0), cb.Model);

            PSInput output;
            output.Position = mul(mul(worldPos, cb.View), cb.Projection);
            output.WorldPos = worldPos.xyz;
            output.Normal = normalize(mul(float4(input.NormalAndMaterialID.xyz, 0.0), cb.Model).xyz);
            output.MaterialID = asuint(input.NormalAndMaterialID.w);

            return output;
        }

        float4 PSMain(PSInput input) : SV_TARGET
        {
            Material mat = materials[input.MaterialID];

            if (mat.Emission > 0.0)
            {
                float3 emissive = mat.Albedo * mat.Emission;
                float3 mapped = emissive / (emissive + 1.0);
                return float4(pow(mapped, 1.0 / 2.2), 1.0);
            }

            float3 N = normalize(input.Normal);
            float3 worldPos = input.WorldPos;
            float3 L = normalize(cb.LightPos - worldPos);
            float3 V = normalize(cb.CameraPos - worldPos);
            float3 H = normalize(L + V);

            float NdotL = max(dot(N, L), 0.0);
            float NdotH = max(dot(N, H), 0.0);
            float spec = pow(NdotH, 64.0);

            float dist = length(cb.LightPos - worldPos);
            float atten = 1.0 / (1.0 + 0.000005 * dist * dist);

            float3 ambient = mat.Albedo * 0.08;
            float3 diffuse = mat.Albedo * cb.LightColor * NdotL * atten;
            float3 specular = cb.LightColor * spec * atten * 0.1;

            float3 color = ambient + diffuse + specular;
            color = pow(color, 1.0 / 2.2);
            return float4(color, 1.0);
        }
        """;

    private readonly Buffer vertexBuffer;
    private readonly Buffer indexBuffer;
    private readonly Buffer materialBuffer;
    private readonly Buffer constantBuffer;
    private readonly uint indexCount;
    private readonly ResourceLayout resourceLayout;
    private readonly ResourceTable resourceTable;
    private readonly GraphicsPipeline pipeline;

    public RasterizationRenderer()
    {
        CornellBoxGeometry.Create(out Vertex[] vertices, out uint[] indices, out Material[] materials);

        indexCount = (uint)indices.Length;

        vertexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Vertex) * vertices.Length),
            StrideInBytes = (uint)sizeof(Vertex),
            Flags = BufferUsageFlags.Vertex
        });
        vertexBuffer.Upload(vertices, 0);

        indexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(uint) * indices.Length),
            StrideInBytes = sizeof(uint),
            Flags = BufferUsageFlags.Index
        });
        indexBuffer.Upload(indices, 0);

        materialBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Material) * materials.Length),
            StrideInBytes = (uint)sizeof(Material),
            Flags = BufferUsageFlags.ShaderResource
        });
        materialBuffer.Upload(materials, 0);

        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(RasterConstants),
            StrideInBytes = (uint)sizeof(RasterConstants),
            Flags = BufferUsageFlags.Constant | BufferUsageFlags.MapWrite
        });

        resourceLayout = App.Context.CreateResourceLayout(new()
        {
            Bindings = BindingHelper.Bindings
            (
                new() { Type = ResourceType.ConstantBuffer, Count = 1, StageFlags = ShaderStageFlags.Vertex | ShaderStageFlags.Pixel },
                new() { Type = ResourceType.StructuredBuffer, Count = 1, StageFlags = ShaderStageFlags.Pixel }
            )
        });

        resourceTable = App.Context.CreateResourceTable(new()
        {
            Layout = resourceLayout,
            Resources = [constantBuffer, materialBuffer]
        });

        InputLayout inputLayout = new();
        inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Position });
        inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Normal });

        using Shader vertexShader = App.Context.LoadShaderFromSource(ShaderSource, "VSMain", ShaderStageFlags.Vertex);
        using Shader pixelShader = App.Context.LoadShaderFromSource(ShaderSource, "PSMain", ShaderStageFlags.Pixel);

        pipeline = App.Context.CreateGraphicsPipeline(new()
        {
            RenderStates = new()
            {
                RasterizerState = RasterizerStates.CullNone,
                DepthStencilState = DepthStencilStates.Default,
                BlendState = BlendStates.Opaque
            },
            Vertex = vertexShader,
            Pixel = pixelShader,
            ResourceLayout = resourceLayout,
            InputLayouts = [inputLayout],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            Output = FrameBuffer.Output
        });
    }

    public override void Update(CameraHandler camera)
    {
        constantBuffer.Upload<RasterConstants>([new()
        {
            Model = Matrix4x4.Identity,
            View = camera.View,
            Projection = camera.Projection,
            LightPos = new(278.0f, 548.0f, 280.0f),
            LightColor = new(2.0f, 1.8f, 1.4f),
            CameraPos = camera.Position
        }], 0);
    }

    public override void Render(CommandBuffer commandBuffer)
    {
        commandBuffer.BeginRenderPass(FrameBuffer, new()
        {
            ColorValues = [new(0.0f, 0.0f, 0.0f, 1.0f)],
            Depth = 1.0f,
            Stencil = 0,
            Flags = ClearFlags.All
        }, resourceTable);

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetResourceTable(resourceTable);
        commandBuffer.SetVertexBuffer(vertexBuffer, 0, 0);
        commandBuffer.SetIndexBuffer(indexBuffer, 0, IndexFormat.UInt32);
        commandBuffer.DrawIndexed(indexCount, 1, 0, 0, 0);

        commandBuffer.EndRenderPass();
    }

    public override void Dispose()
    {
        base.Dispose();

        pipeline.Dispose();
        resourceTable.Dispose();
        resourceLayout.Dispose();
        constantBuffer.Dispose();
        materialBuffer.Dispose();
        indexBuffer.Dispose();
        vertexBuffer.Dispose();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 240)]
file struct RasterConstants
{
    [FieldOffset(0)]
    public Matrix4x4 Model;

    [FieldOffset(64)]
    public Matrix4x4 View;

    [FieldOffset(128)]
    public Matrix4x4 Projection;

    [FieldOffset(192)]
    public Vector3 LightPos;

    [FieldOffset(208)]
    public Vector3 LightColor;

    [FieldOffset(224)]
    public Vector3 CameraPos;
}
