using System.Numerics;
using System.Runtime.InteropServices;
using FluidTank.Handlers;
using FluidTank.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank.Passes;

internal unsafe class ScenePass : DisposableObject
{
    private readonly Buffer constantBuffer;
    private readonly Buffer backgroundConstantBuffer;
    private readonly GraphicsPipeline pipeline;
    private readonly GraphicsPipeline backgroundPipeline;

    private bool isInitialized;

    public ScenePass(uint width, uint height)
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
        inputLayout.Add(new()
        {
            Format = ElementFormat.Float4,
            Semantic = ElementSemantic.Position
        });
        inputLayout.Add(new()
        {
            Format = ElementFormat.Float4,
            Semantic = ElementSemantic.Normal
        });

        using Shader vertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "Scene.slang"), "VSMain"));
        using Shader fragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "Scene.slang"), "FSMain"));

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

        using Shader backgroundVertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "SceneBackground.slang"), "FullscreenVS"));
        using Shader backgroundFragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "SceneBackground.slang"), "BackgroundFS"));

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

        CreateTextures(width, height);
    }

    public Texture Color { get; private set; } = null!;

    public Texture LinearDepth { get; private set; } = null!;

    public Texture DepthStencil { get; private set; } = null!;

    public void Record(CommandBuffer commandBuffer, SceneResources scene, CameraHandler camera)
    {
        SceneConstants sceneConstants = new()
        {
            View = camera.View,
            Projection = camera.Projection,
            CameraPosition = camera.Position,
            LightDirection = SceneResources.SunDirection,
            LightIntensity = SceneResources.LightIntensity,
            Materials = scene.Materials.StorageReadOnlyHandle
        };

        constantBuffer.Upload(0, new()
        {
            Pointer = (nint)(&sceneConstants),
            SizeInBytes = (uint)sizeof(SceneConstants)
        });

        Matrix4x4.Invert(camera.View, out Matrix4x4 inverseView);
        Matrix4x4.Invert(camera.Projection, out Matrix4x4 inverseProjection);

        BackgroundConstants backgroundConstants = new()
        {
            InvView = inverseView,
            InvProjection = inverseProjection,
            SunDirection = SceneResources.SunDirection
        };

        backgroundConstantBuffer.Upload(0, new()
        {
            Pointer = (nint)(&backgroundConstants),
            SizeInBytes = (uint)sizeof(BackgroundConstants)
        });

        TextureLayout colorLayout = isInitialized ? TextureLayout.Sampled : TextureLayout.Undefined;
        TextureLayout depthLayout = isInitialized ? TextureLayout.DepthStencilAttachment : TextureLayout.Undefined;
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
        isInitialized = true;
    }

    public void Resize(uint width, uint height)
    {
        DestroyTextures();
        CreateTextures(width, height);
    }

    protected override void Destroy()
    {
        DestroyTextures();
        backgroundPipeline?.Dispose();
        pipeline?.Dispose();
        backgroundConstantBuffer?.Dispose();
        constantBuffer?.Dispose();
    }

    private void CreateTextures(uint width, uint height)
    {
        Color = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R16G16B16A16Float, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.ColorAttachment
        });
        LinearDepth = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.R32Float, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.Sampled | TextureUsages.ColorAttachment
        });
        DepthStencil = App.Context.CreateTexture(TextureDesc.Texture2D(PixelFormat.D32FloatS8UInt, width, height, 1, SampleCount.Count1) with
        {
            Usages = TextureUsages.DepthStencilAttachment
        });
    }

    private void DestroyTextures()
    {
        DepthStencil?.Dispose();
        DepthStencil = null!;
        LinearDepth?.Dispose();
        LinearDepth = null!;
        Color?.Dispose();
        Color = null!;

        isInitialized = false;
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

    [FieldOffset(144)]
    public Vector3 LightDirection;

    [FieldOffset(156)]
    public float LightIntensity;

    [FieldOffset(160)]
    public ResourceHandle Materials;
}
