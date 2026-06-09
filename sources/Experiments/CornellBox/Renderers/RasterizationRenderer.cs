using System.Numerics;
using System.Runtime.InteropServices;
using CornellBox.Handlers;
using CornellBox.Helpers;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace CornellBox.Renderers;

internal unsafe class RasterizationRenderer : Renderer
{
    private readonly uint indexCount;

    private readonly Buffer vertexBuffer;
    private readonly Buffer indexBuffer;
    private readonly Buffer constantBuffer;
    private readonly GraphicsPipeline pipeline;

    private readonly Buffer materialBuffer;

    public RasterizationRenderer()
    {
        CornellBoxGeometry.Create(out Vertex[] vertices, out uint[] indices, out Material[] materials);

        indexCount = (uint)indices.Length;

        vertexBuffer = App.Context.CreateBuffer(BufferDesc.Vertex((uint)(sizeof(Vertex) * vertices.Length)));

        fixed (Vertex* pointer = vertices)
        {
            vertexBuffer.Upload(0, new() { Pointer = (nint)pointer, SizeInBytes = (uint)(sizeof(Vertex) * vertices.Length) });
        }

        indexBuffer = App.Context.CreateBuffer(BufferDesc.Index((uint)(sizeof(uint) * indices.Length)));

        fixed (uint* pointer = indices)
        {
            indexBuffer.Upload(0, new() { Pointer = (nint)pointer, SizeInBytes = (uint)(sizeof(uint) * indices.Length) });
        }

        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(RasterizationConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        InputLayout inputLayout = new();
        inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Position });
        inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Normal });

        using Shader vertexShader = App.Context.CreateShader(new()
        {
            Name = "VSMain",
            CodeBytes = ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("Rasterization.slang"), "VSMain")
        });
        using Shader fragmentShader = App.Context.CreateShader(new()
        {
            Name = "FSMain",
            CodeBytes = ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("Rasterization.slang"), "FSMain")
        });

        pipeline = App.Context.CreateGraphicsPipeline(new()
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            InputLayouts = [inputLayout],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            AttachmentFormats = AttachmentFormats,
            RenderState = new()
            {
                RasterizerState = RasterizerState.CullNone(),
                DepthStencilState = DepthStencilState.DepthReadWrite(),
                BlendState = BlendState.Opaque()
            }
        });

        materialBuffer = App.Context.CreateBuffer(BufferDesc.StorageReadOnly((uint)(sizeof(Material) * materials.Length), (uint)sizeof(Material)));

        fixed (Material* pointer = materials)
        {
            materialBuffer.Upload(0, new() { Pointer = (nint)pointer, SizeInBytes = (uint)(sizeof(Material) * materials.Length) });
        }
    }

    public override void Update(CameraHandler camera)
    {
        RasterizationConstants parameters = new()
        {
            Model = Matrix4x4.Identity,
            View = camera.View,
            Projection = camera.Projection,
            LightPosition = new(278.0f, 547.0f, 280.0f),
            LightColor = new(2.0f, 1.8f, 1.4f),
            CameraPosition = camera.Position,
            Materials = materialBuffer.StorageReadOnlyHandle
        };

        constantBuffer.Upload(0, new() { Pointer = (nint)(&parameters), SizeInBytes = (uint)sizeof(RasterizationConstants) });
    }

    public override void Render(CommandBuffer commandBuffer)
    {
        commandBuffer.Transition(Color, default, TextureLayout.ColorAttachment);
        commandBuffer.Transition(DepthStencil, default, TextureLayout.DepthStencilAttachment);

        commandBuffer.BeginRenderPass([ColorAttachment.Clear(Color, new(0.51f, 0.518f, 0.557f, 1.0f))], DepthStencilAttachment.Clear(DepthStencil, 1.0f, 0));

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetVertexBuffer(vertexBuffer, 0, 0);
        commandBuffer.SetIndexBuffer(indexBuffer, 0, IndexFormat.UInt32);
        commandBuffer.SetConstantBuffer(constantBuffer, 0);

        commandBuffer.DrawIndexed(indexCount, 1, 0, 0, 0);

        commandBuffer.EndRenderPass();

        commandBuffer.Transition(Color, default, TextureLayout.Sampled);
    }

    public override void Dispose()
    {
        base.Dispose();

        materialBuffer.Dispose();

        pipeline.Dispose();
        constantBuffer.Dispose();
        indexBuffer.Dispose();
        vertexBuffer.Dispose();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 256)]
file struct RasterizationConstants
{
    [FieldOffset(0)]
    public Matrix4x4 Model;

    [FieldOffset(64)]
    public Matrix4x4 View;

    [FieldOffset(128)]
    public Matrix4x4 Projection;

    [FieldOffset(192)]
    public Vector3 LightPosition;

    [FieldOffset(208)]
    public Vector3 LightColor;

    [FieldOffset(224)]
    public Vector3 CameraPosition;

    [FieldOffset(240)]
    public ResourceHandle Materials;
}
