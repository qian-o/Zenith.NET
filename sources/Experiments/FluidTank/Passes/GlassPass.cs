using System.Numerics;
using System.Runtime.InteropServices;
using FluidTank.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank.Passes;

internal unsafe class GlassPass(uint renderWidth, uint renderHeight, uint displayWidth, uint displayHeight) : Pass(renderWidth, renderHeight, displayWidth, displayHeight)
{
    private Buffer constantBuffer = null!;
    private GraphicsPipeline pipeline = null!;

    protected override void Initialize()
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(GlassConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        InputLayout inputLayout = new();
        inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Position });
        inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Normal });

        BlendState blend = BlendState.AlphaBlend();
        blend.ColorAttachment0.ColorWrites = ColorWrites.Red | ColorWrites.Green | ColorWrites.Blue;

        using Shader vertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("Glass.slang"), "GlassVS"));
        using Shader fragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("Glass.slang"), "GlassFS"));

        pipeline = App.Context.CreateGraphicsPipeline(new()
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            InputLayouts = [inputLayout],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            AttachmentFormats = new()
            {
                ColorFormats = [PixelFormat.R16G16B16A16Float],
                DepthStencilFormat = PixelFormat.D32FloatS8UInt,
                SampleCount = SampleCount.Count1
            },
            RenderState = new()
            {
                Rasterizer = RasterizerState.CullNone(),
                DepthStencil = DepthStencilState.DepthRead(),
                Blend = blend
            }
        });
    }

    protected override void RecordImpl(CommandBuffer commandBuffer, in PassArgs args)
    {
        SceneResources scene = args.Scene;
        Texture color = args.Color;
        Texture depthStencil = args.DepthStencil;
        ReadOnlySpan<(Vector3 Center, Vector3 Normal)> faces = scene.GlassFaces;
        Span<(uint FirstIndex, float Depth)> sortedFaces = stackalloc (uint FirstIndex, float Depth)[faces.Length];
        int faceCount = 0;

        for (int i = 0; i < faces.Length; i++)
        {
            (Vector3 center, Vector3 normal) = faces[i];
            bool frontFace = Vector3.Dot(normal, args.CameraPosition - center) < 0.0f;

            if (frontFace == args.FrontFaces)
            {
                float depth = -Vector3.Transform(center, args.View).Z;
                int position = faceCount;

                while (position > 0 && sortedFaces[position - 1].Depth < depth)
                {
                    sortedFaces[position] = sortedFaces[position - 1];
                    position--;
                }

                sortedFaces[position] = ((uint)i * 6, depth);
                faceCount++;
            }
        }

        GlassConstants constants = new()
        {
            View = args.View,
            Projection = args.Projection,
            CameraPosition = args.CameraPosition,
            Time = args.Time
        };

        constantBuffer.Upload(0, new()
        {
            Pointer = (nint)(&constants),
            SizeInBytes = (uint)sizeof(GlassConstants)
        });

        commandBuffer.Transition(color, default, TextureLayout.Sampled, TextureLayout.ColorAttachment);
        commandBuffer.Transition(depthStencil, default, TextureLayout.DepthStencilAttachment, TextureLayout.DepthStencilAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.Load(color)], DepthStencilAttachment.Load(depthStencil));
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetVertexBuffer(scene.GlassVertices, 0, 0);
        commandBuffer.SetIndexBuffer(scene.GlassIndices, 0, IndexFormat.UInt32);
        commandBuffer.SetConstantBuffer(constantBuffer, 0);

        for (int i = 0; i < faceCount; i++)
        {
            commandBuffer.DrawIndexed(6, 1, sortedFaces[i].FirstIndex, 0, 0);
        }

        commandBuffer.EndRenderPass();
        commandBuffer.Transition(color, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
    }

    protected override void ResizeImpl()
    {
    }

    protected override void Destroy()
    {
        pipeline.Dispose();
        constantBuffer.Dispose();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 144)]
file struct GlassConstants
{
    [FieldOffset(0)]
    public Matrix4x4 View;

    [FieldOffset(64)]
    public Matrix4x4 Projection;

    [FieldOffset(128)]
    public Vector3 CameraPosition;

    [FieldOffset(140)]
    public float Time;
}
