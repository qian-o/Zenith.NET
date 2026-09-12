using System.Numerics;
using System.Runtime.InteropServices;
using FluidTank.Helpers;
using FluidTank.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank.Passes;

internal unsafe class GlassPass : IDisposable
{
    private readonly Buffer constantBuffer;

    private readonly GraphicsPipeline pipeline;

    public GlassPass(GraphicsContext context)
    {
        constantBuffer = GraphicsHelper.CreateConstantBuffer<GlassConstants>(context);

        InputLayout inputLayout = new();
        inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Position });
        inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Normal });

        BlendState blend = BlendState.AlphaBlend();
        blend.ColorAttachment0.ColorWrites = ColorWrites.Red | ColorWrites.Green | ColorWrites.Blue;

        pipeline = GraphicsHelper.CreateGraphicsPipeline(context, "Glass.slang", "GlassVS", "GlassFS", [inputLayout], new()
        {
            ColorFormats = [PixelFormat.R16G16B16A16Float],
            DepthStencilFormat = PixelFormat.D32FloatS8UInt,
            SampleCount = SampleCount.Count1
        }, RasterizerState.CullNone(), DepthStencilState.DepthRead(), blend);
    }

    public void Render(CommandBuffer commandBuffer, FrameData frame, SceneResources scene, Texture color, Texture depthStencil, bool frontFaces)
    {
        ReadOnlySpan<(Vector3 Center, Vector3 Normal)> faces = scene.GlassFaces;
        Span<(uint FirstIndex, float Depth)> sortedFaces = stackalloc (uint FirstIndex, float Depth)[faces.Length];
        int faceCount = 0;

        for (int i = 0; i < faces.Length; i++)
        {
            (Vector3 center, Vector3 normal) = faces[i];
            bool frontFace = Vector3.Dot(normal, frame.Position - center) < 0.0f;

            if (frontFace == frontFaces)
            {
                float depth = -Vector3.Transform(center, frame.View).Z;
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
            View = frame.View,
            Projection = frame.Projection,
            CameraPosition = frame.Position,
            Time = frame.Time
        };
        GraphicsHelper.Upload(constantBuffer, 0, &constants, (uint)sizeof(GlassConstants));

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

    public void Dispose()
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
