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
    private readonly ResourceSlot[] resourceSlots =
    [
        new() { Type = ResourceType.ConstantBuffer, Count = 1 },
        new() { Type = ResourceType.StructuredBuffer, Count = 1 }
    ];

    private readonly Buffer vertexBuffer;
    private readonly Buffer indexBuffer;
    private readonly Buffer materialBuffer;
    private readonly Buffer constantBuffer;
    private readonly uint indexCount;
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

        resourceTable = App.Context.CreateResourceTable(new() { Slots = resourceSlots });
        resourceTable.Write(0, constantBuffer);
        resourceTable.Write(1, materialBuffer);

        InputLayout inputLayout = new();
        inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Position });
        inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Normal });

        using Shader vertexShader = App.Context.LoadShaderFromFile(ShaderPath("Rasterization.slang"), "VSMain", ShaderStageFlags.Vertex);
        using Shader pixelShader = App.Context.LoadShaderFromFile(ShaderPath("Rasterization.slang"), "PSMain", ShaderStageFlags.Pixel);

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
            ResourceSlots = resourceSlots,
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
            LightPos = new(278.0f, 547.0f, 280.0f),
            LightColor = new(2.0f, 1.8f, 1.4f),
            CameraPos = camera.Position
        }], 0);
    }

    public override void Render(CommandBuffer commandBuffer)
    {
        commandBuffer.BeginRenderPass(FrameBuffer, new()
        {
            ColorValues = [new(0.51f, 0.518f, 0.557f, 1.0f)],
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
