using System.Numerics;
using System.Runtime.InteropServices;
using FluidTank.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank.Passes;

internal unsafe class ScenePass(uint width, uint height) : Pass(width, height)
{
    private Buffer constantBuffer = null!;
    private Buffer backgroundConstantBuffer = null!;
    private GraphicsPipeline pipeline = null!;
    private GraphicsPipeline backgroundPipeline = null!;
    private bool initialized;

    public Texture Color { get; private set; } = null!;

    public Texture LinearDepth { get; private set; } = null!;

    public Texture DepthStencil { get; private set; } = null!;

    protected override void Initialize()
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(SceneConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });
        backgroundConstantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(BackgroundConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        InputLayout inputLayout = new();
        inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Position });
        inputLayout.Add(new() { Format = ElementFormat.Float4, Semantic = ElementSemantic.Normal });

        using Shader vertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("Scene.slang"), "VSMain"));
        using Shader fragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("Scene.slang"), "FSMain"));

        pipeline = App.Context.CreateGraphicsPipeline(new()
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            InputLayouts = [inputLayout],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            AttachmentFormats = new()
            {
                ColorFormats = [PixelFormat.R16G16B16A16Float, PixelFormat.R32Float],
                DepthStencilFormat = PixelFormat.D32FloatS8UInt,
                SampleCount = SampleCount.Count1
            },
            RenderState = new()
            {
                Rasterizer = RasterizerState.CullBack(),
                DepthStencil = DepthStencilState.DepthReadWrite(),
                Blend = BlendState.Opaque()
            }
        });

        using Shader backgroundVertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("SceneBackground.slang"), "FullscreenVS"));
        using Shader backgroundFragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, ShaderPath("SceneBackground.slang"), "BackgroundFS"));

        backgroundPipeline = App.Context.CreateGraphicsPipeline(new()
        {
            VertexShader = backgroundVertexShader,
            FragmentShader = backgroundFragmentShader,
            InputLayouts = [],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            AttachmentFormats = new()
            {
                ColorFormats = [PixelFormat.R16G16B16A16Float, PixelFormat.R32Float],
                DepthStencilFormat = PixelFormat.D32FloatS8UInt,
                SampleCount = SampleCount.Count1
            },
            RenderState = new()
            {
                Rasterizer = RasterizerState.CullNone(),
                DepthStencil = DepthStencilState.DepthNone(),
                Blend = BlendState.Opaque()
            }
        });

        ResizeImpl();
    }

    protected override void RecordImpl(CommandBuffer commandBuffer, in PassArgs args)
    {
        SceneResources scene = args.Scene;
        SceneConstants sceneConstants = new()
        {
            View = args.View,
            Projection = args.Projection,
            CameraPosition = args.CameraPosition,
            Time = args.Time,
            LightDirection = args.SunDirection,
            LightIntensity = args.LightIntensity,
            Materials = scene.Materials.StorageReadOnlyHandle
        };

        constantBuffer.Upload(0, new()
        {
            Pointer = (nint)(&sceneConstants),
            SizeInBytes = (uint)sizeof(SceneConstants)
        });

        BackgroundConstants backgroundConstants = new()
        {
            InvView = args.InverseView,
            InvProjection = args.InverseProjection,
            SunDirection = args.SunDirection
        };

        backgroundConstantBuffer.Upload(0, new()
        {
            Pointer = (nint)(&backgroundConstants),
            SizeInBytes = (uint)sizeof(BackgroundConstants)
        });

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

    protected override void ResizeImpl()
    {
        DisposeTargets();

        Color = CreateTexture(Width, Height, PixelFormat.R16G16B16A16Float, TextureUsages.Sampled | TextureUsages.ColorAttachment);
        LinearDepth = CreateTexture(Width, Height, PixelFormat.R32Float, TextureUsages.Sampled | TextureUsages.ColorAttachment);
        DepthStencil = CreateTexture(Width, Height, PixelFormat.D32FloatS8UInt, TextureUsages.DepthStencilAttachment);
        initialized = false;
    }

    protected override void Destroy()
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
