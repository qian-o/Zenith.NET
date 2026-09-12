using System.Numerics;
using System.Runtime.InteropServices;
using FluidTank.Helpers;
using FluidTank.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank.Passes;

internal unsafe class ScenePass : IDisposable
{
    private readonly GraphicsContext context;

    private readonly Buffer constantBuffer;

    private readonly Buffer backgroundConstantBuffer;

    private readonly GraphicsPipeline pipeline;

    private readonly GraphicsPipeline backgroundPipeline;

    private bool initialized;

    public ScenePass(GraphicsContext context)
    {
        this.context = context;
        constantBuffer = GraphicsHelper.CreateConstantBuffer<SceneConstants>(context);
        backgroundConstantBuffer = GraphicsHelper.CreateConstantBuffer<BackgroundConstants>(context);

        InputLayout inputLayout = new();
        inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Position });
        inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Normal });

        pipeline = GraphicsHelper.CreateGraphicsPipeline(context, "Scene.slang", "VSMain", "FSMain", [inputLayout], new()
        {
            ColorFormats = [PixelFormat.R16G16B16A16Float, PixelFormat.R32Float],
            DepthStencilFormat = PixelFormat.D32FloatS8UInt,
            SampleCount = SampleCount.Count1
        }, RasterizerState.CullBack(), DepthStencilState.DepthReadWrite(), BlendState.Opaque());

        backgroundPipeline = GraphicsHelper.CreateGraphicsPipeline(context, "SceneBackground.slang", "FullscreenVS", "BackgroundFS", [], new()
        {
            ColorFormats = [PixelFormat.R16G16B16A16Float, PixelFormat.R32Float],
            DepthStencilFormat = PixelFormat.D32FloatS8UInt,
            SampleCount = SampleCount.Count1
        }, RasterizerState.CullNone(), DepthStencilState.DepthNone(), BlendState.Opaque());
    }

    public Texture Color { get; private set; } = null!;

    public Texture LinearDepth { get; private set; } = null!;

    public Texture DepthStencil { get; private set; } = null!;

    public void Render(CommandBuffer commandBuffer, FrameData frame, SceneResources scene)
    {
        SceneConstants constants = new()
        {
            View = frame.View,
            Projection = frame.Projection,
            CameraPosition = frame.Position,
            Time = frame.Time,
            LightDirection = frame.SunDirection,
            LightIntensity = frame.LightIntensity,
            Materials = scene.Materials.StorageReadOnlyHandle
        };
        GraphicsHelper.Upload(constantBuffer, 0, &constants, (uint)sizeof(SceneConstants));

        BackgroundConstants background = new()
        {
            InvView = frame.InvView,
            InvProjection = frame.InvProjection,
            SunDirection = frame.SunDirection
        };
        GraphicsHelper.Upload(backgroundConstantBuffer, 0, &background, (uint)sizeof(BackgroundConstants));

        TextureLayout colorLayout = initialized ? TextureLayout.Sampled : TextureLayout.Undefined;
        TextureLayout depthLayout = initialized ? TextureLayout.DepthStencilAttachment : TextureLayout.Undefined;
        commandBuffer.Transition(Color, default, colorLayout, TextureLayout.ColorAttachment);
        commandBuffer.Transition(LinearDepth, default, colorLayout, TextureLayout.ColorAttachment);
        commandBuffer.Transition(DepthStencil, default, depthLayout, TextureLayout.DepthStencilAttachment);

        commandBuffer.BeginRenderPass([ColorAttachment.Clear(Color, Vector4.Zero), ColorAttachment.Clear(LinearDepth, Vector4.Zero)], DepthStencilAttachment.Clear(DepthStencil, 1.0f, 0));
        commandBuffer.SetPipeline(backgroundPipeline);
        commandBuffer.SetConstantBuffer(backgroundConstantBuffer, 0);
        commandBuffer.Draw(3, 1, 0, 0);

        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetVertexBuffer(scene.Vertices, 0, 0);
        commandBuffer.SetIndexBuffer(scene.Indices, 0, IndexFormat.UInt32);
        commandBuffer.SetConstantBuffer(constantBuffer, 0);
        commandBuffer.DrawIndexed(scene.SceneIndexCount, 1, 0, 0, 0);
        commandBuffer.EndRenderPass();

        commandBuffer.Transition(Color, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
        commandBuffer.Transition(LinearDepth, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
        initialized = true;
    }

    public void Resize(uint width, uint height)
    {
        DisposeTargets();

        Color = GraphicsHelper.CreateTexture(context, PixelFormat.R16G16B16A16Float, width, height, TextureUsages.Sampled | TextureUsages.ColorAttachment);
        LinearDepth = GraphicsHelper.CreateTexture(context, PixelFormat.R32Float, width, height, TextureUsages.Sampled | TextureUsages.ColorAttachment);
        DepthStencil = GraphicsHelper.CreateTexture(context, PixelFormat.D32FloatS8UInt, width, height, TextureUsages.DepthStencilAttachment);
        initialized = false;
    }

    public void Dispose()
    {
        backgroundPipeline.Dispose();
        pipeline.Dispose();
        backgroundConstantBuffer.Dispose();
        constantBuffer.Dispose();
        DisposeTargets();
    }

    private void DisposeTargets()
    {
        DepthStencil?.Dispose();
        LinearDepth?.Dispose();
        Color?.Dispose();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 144)]
file struct BackgroundConstants
{
    [FieldOffset(0)]
    public Matrix4x4 InvView;

    [FieldOffset(64)]
    public Matrix4x4 InvProjection;

    [FieldOffset(128)]
    public Vector3 SunDirection;
}

[StructLayout(LayoutKind.Explicit, Size = 176)]
file struct SceneConstants
{
    [FieldOffset(0)]
    public Matrix4x4 View;

    [FieldOffset(64)]
    public Matrix4x4 Projection;

    [FieldOffset(128)]
    public Vector3 CameraPosition;

    [FieldOffset(140)]
    public float Time;

    [FieldOffset(144)]
    public Vector3 LightDirection;

    [FieldOffset(156)]
    public float LightIntensity;

    [FieldOffset(160)]
    public ResourceHandle Materials;
}
