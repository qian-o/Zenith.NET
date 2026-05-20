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
    private static readonly ResourceBinding[] ResourceBindings =
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

        fixed (Vertex* pointer = vertices)
        {
            vertexBuffer.Upload(0, new() { Pointer = (nint)pointer, SizeInBytes = (uint)(sizeof(Vertex) * vertices.Length) });
        }

        indexBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(uint) * indices.Length),
            StrideInBytes = sizeof(uint),
            Flags = BufferUsageFlags.Index
        });

        fixed (uint* pointer = indices)
        {
            indexBuffer.Upload(0, new() { Pointer = (nint)pointer, SizeInBytes = (uint)(sizeof(uint) * indices.Length) });
        }

        materialBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Material) * materials.Length),
            StrideInBytes = (uint)sizeof(Material),
            Flags = BufferUsageFlags.ShaderResource
        });

        fixed (Material* pointer = materials)
        {
            materialBuffer.Upload(0, new() { Pointer = (nint)pointer, SizeInBytes = (uint)(sizeof(Material) * materials.Length) });
        }

        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(RasterConstants),
            StrideInBytes = (uint)sizeof(RasterConstants),
            Flags = BufferUsageFlags.Constant | BufferUsageFlags.MapWrite
        });

        resourceTable = App.Context.CreateResourceTable(new() { Bindings = ResourceBindings });
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
                RasterizerState = RasterizerState.CullNone(),
                DepthStencilState = DepthStencilState.DepthReadWrite(),
                BlendState = BlendState.Opaque()
            },
            Vertex = vertexShader,
            Pixel = pixelShader,
            ResourceBindings = ResourceBindings,
            InputLayouts = [inputLayout],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            Output = RenderOutput
        });
    }

    public override void Update(CameraHandler camera)
    {
        RasterConstants constants = new()
        {
            Model = Matrix4x4.Identity,
            View = camera.View,
            Projection = camera.Projection,
            LightPos = new(278.0f, 547.0f, 280.0f),
            LightColor = new(2.0f, 1.8f, 1.4f),
            CameraPos = camera.Position
        };

        constantBuffer.Upload(0, new() { Pointer = (nint)(&constants), SizeInBytes = (uint)sizeof(RasterConstants) });
    }

    public override void Render(CommandBuffer commandBuffer)
    {
        commandBuffer.BeginRenderPass([new()
        {
            Texture = Color,
            LoadOp = LoadOp.Clear,
            StoreOp = StoreOp.Store,
            ClearColor = new(0.51f, 0.518f, 0.557f, 1.0f)
        }],
                                      new()
                                      {
                                          Texture = DepthStencil,
                                          DepthLoadOp = LoadOp.Clear,
                                          DepthStoreOp = StoreOp.Store,
                                          StencilLoadOp = LoadOp.Clear,
                                          StencilStoreOp = StoreOp.Store,
                                          ClearDepth = 1.0f,
                                          ClearStencil = 0
                                      });

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.PushResourceTable(resourceTable);
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
