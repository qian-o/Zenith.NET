using System.Numerics;
using System.Runtime.InteropServices;
using Sponza.Helpers;
using Sponza.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace Sponza.Passes;

internal class ScenePass : IDisposable
{
    private readonly GraphicsContext context;
    private readonly GraphicsPipeline opaquePipeline;
    private readonly GraphicsPipeline doubleSidedPipeline;
    private readonly GraphicsPipeline skyPipeline;
    private readonly Buffer skyConstants;
    private readonly Sampler materialSampler;
    private readonly Sampler shadowSampler;
    private readonly Sampler environmentSampler;

    private Buffer[] drawConstants = [];
    private Texture hdrColor = null!;
    private Texture indirectDiffuse = null!;
    private Texture deviceDepth = null!;
    private Texture encodedMotion = null!;
    private Texture hardwareDepth = null!;
    private TextureLayout colorLayout;
    private TextureLayout depthLayout;

    public ScenePass(GraphicsContext context)
    {
        this.context = context;

        InputLayout inputLayout = new();
        inputLayout.Add(new() { Semantic = ElementSemantic.Position, Format = ElementFormat.Float3 });
        inputLayout.Add(new() { Semantic = ElementSemantic.Normal, Format = ElementFormat.Float3 });
        inputLayout.Add(new() { Semantic = ElementSemantic.Tangent, Format = ElementFormat.Float4 });
        inputLayout.Add(new() { Semantic = ElementSemantic.TexCoord, Format = ElementFormat.Float2 });

        using Shader vertex = GraphicsHelper.LoadShader(context, "Scene.slang", "VSMain");
        using Shader fragment = GraphicsHelper.LoadShader(context, "Scene.slang", "FSMain");
        using Shader skyVertex = GraphicsHelper.LoadShader(context, "Scene.slang", "SkyVSMain");
        using Shader skyFragment = GraphicsHelper.LoadShader(context, "Scene.slang", "SkyFSMain");

        GraphicsPipelineDesc desc = new()
        {
            VertexShader = vertex,
            FragmentShader = fragment,
            InputLayouts = [inputLayout],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            AttachmentFormats = new()
            {
                ColorFormats = [PixelFormat.R16G16B16A16Float, PixelFormat.R16G16B16A16Float, PixelFormat.R32Float, PixelFormat.R16G16UNorm],
                DepthStencilFormat = PixelFormat.D32Float,
                SampleCount = SampleCount.Count1
            },
            RenderState = new()
            {
                Rasterizer = RasterizerState.CullBack(),
                DepthStencil = DepthStencilState.DepthReadWrite(),
                Blend = BlendState.Opaque()
            }
        };

        opaquePipeline = context.CreateGraphicsPipeline(desc);
        desc.RenderState.Rasterizer = RasterizerState.CullNone();
        doubleSidedPipeline = context.CreateGraphicsPipeline(desc);

        desc.VertexShader = skyVertex;
        desc.FragmentShader = skyFragment;
        desc.InputLayouts = [];
        desc.RenderState.DepthStencil = DepthStencilState.DepthRead();
        skyPipeline = context.CreateGraphicsPipeline(desc);

        skyConstants = GraphicsHelper.CreateConstantBuffer<SceneConstants>(context);
        materialSampler = context.CreateSampler(SamplerDesc.Anisotropic(8));
        shadowSampler = context.CreateSampler(SamplerDesc.PointClamp());
        environmentSampler = context.CreateSampler(SamplerDesc.LinearClamp());
    }

    public void Resize(uint width, uint height)
    {
        DestroyTargets();

        TextureUsages usages = TextureUsages.ColorAttachment | TextureUsages.Sampled;
        hdrColor = GraphicsHelper.CreateTexture(context, PixelFormat.R16G16B16A16Float, width, height, usages);
        indirectDiffuse = GraphicsHelper.CreateTexture(context, PixelFormat.R16G16B16A16Float, width, height, usages);
        deviceDepth = GraphicsHelper.CreateTexture(context, PixelFormat.R32Float, width, height, usages);
        encodedMotion = GraphicsHelper.CreateTexture(context, PixelFormat.R16G16UNorm, width, height, usages);
        hardwareDepth = GraphicsHelper.CreateTexture(context, PixelFormat.D32Float, width, height, TextureUsages.DepthStencilAttachment);
        colorLayout = TextureLayout.Undefined;
        depthLayout = TextureLayout.Undefined;
    }

    public SceneOutput Record(CommandBuffer commandBuffer, ScenePassArgs args)
    {
        if (drawConstants.Length != args.Scene.Draws.Length)
        {
            foreach (Buffer buffer in drawConstants)
            {
                buffer.Dispose();
            }

            drawConstants = new Buffer[args.Scene.Draws.Length];
            for (int i = 0; i < drawConstants.Length; i++)
            {
                drawConstants[i] = GraphicsHelper.CreateConstantBuffer<SceneConstants>(context);
            }
        }

        SceneFrameConstants frame = new()
        {
            ViewProjection = args.Frame.ViewProjection,
            UnjitteredViewProjection = args.Frame.UnjitteredViewProjection,
            PreviousViewProjection = args.Frame.PreviousViewProjection,
            InverseViewProjection = args.Frame.InverseViewProjection,
            SunDirectionAndIntensity = new(args.Sky.SunDirectionWorld, args.Sky.SkyIntensity),
            SunRadiance = new(args.Sky.SunRadiance, 0.0f),
            ZenithColor = new(args.Sky.ZenithColor, 0.0f),
            HorizonColor = new(args.Sky.HorizonColor, 0.0f),
            GroundColor = new(args.Sky.GroundColor, 0.0f),
            CameraPositionWorld = new(args.Frame.CameraPositionWorld, 1.0f),
            ShadowViewProjection = args.Shadow.ViewProjection,
            ShadowParameters = new(args.Shadow.NormalBiasInMeters, args.Shadow.DepthBias, args.Shadow.TexelSize, args.Environment.PrefilteredEnvironment.Desc.MipLevels - 1),
            Materials = args.Scene.MaterialBuffer.StorageReadOnlyHandle,
            MaterialSampler = materialSampler.Handle,
            ShadowTexture = args.Shadow.Texture.SampledHandle,
            ShadowSampler = shadowSampler.Handle,
            EnvironmentTexture = args.Environment.PrefilteredEnvironment.SampledHandle,
            EnvironmentSampler = environmentSampler.Handle,
            RenderSize = new(args.Frame.RenderWidth, args.Frame.RenderHeight, 0.0f, 0.0f)
        };

        GraphicsHelper.Upload<SceneConstants>(skyConstants, new() { World = Matrix4x4.Identity, NormalWorld = Matrix4x4.Identity, Frame = frame, MaterialIndex = 0, WorldOrientation = 1.0f, Padding0 = 0, Padding1 = 0 });

        for (int i = 0; i < args.Scene.Draws.Length; i++)
        {
            DrawData draw = args.Scene.Draws[i];
            GraphicsHelper.Upload<SceneConstants>(drawConstants[i], new()
            {
                World = draw.World,
                NormalWorld = draw.NormalWorld,
                Frame = frame,
                MaterialIndex = draw.MaterialIndex,
                WorldOrientation = draw.World.GetDeterminant() < 0.0f ? -1.0f : 1.0f,
                Padding0 = 0,
                Padding1 = 0
            });
        }

        commandBuffer.Transition(hdrColor, default, colorLayout, TextureLayout.ColorAttachment);
        commandBuffer.Transition(indirectDiffuse, default, colorLayout, TextureLayout.ColorAttachment);
        commandBuffer.Transition(deviceDepth, default, colorLayout, TextureLayout.ColorAttachment);
        commandBuffer.Transition(encodedMotion, default, colorLayout, TextureLayout.ColorAttachment);
        commandBuffer.Transition(hardwareDepth, default, depthLayout, TextureLayout.DepthStencilAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.DontCare(hdrColor), ColorAttachment.DontCare(indirectDiffuse), ColorAttachment.DontCare(deviceDepth), ColorAttachment.DontCare(encodedMotion)], DepthStencilAttachment.Clear(hardwareDepth, 1.0f, 0));

        commandBuffer.SetPipeline(skyPipeline);
        commandBuffer.SetConstantBuffer(skyConstants, 0);
        commandBuffer.Draw(3, 1, 0, 0);

        for (int i = 0; i < args.Scene.Draws.Length; i++)
        {
            DrawData draw = args.Scene.Draws[i];
            commandBuffer.SetPipeline(args.Scene.Materials[draw.MaterialIndex].DoubleSided ? doubleSidedPipeline : opaquePipeline);
            commandBuffer.SetVertexBuffer(args.Scene.VertexBuffer, 0, 0);
            commandBuffer.SetIndexBuffer(args.Scene.IndexBuffer, 0, IndexFormat.UInt16);
            commandBuffer.SetConstantBuffer(drawConstants[i], 0);
            commandBuffer.DrawIndexed(draw.IndexCount, 1, draw.FirstIndex, draw.VertexOffset, 0);
        }

        commandBuffer.EndRenderPass();
        commandBuffer.Transition(hdrColor, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
        commandBuffer.Transition(indirectDiffuse, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
        commandBuffer.Transition(deviceDepth, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
        commandBuffer.Transition(encodedMotion, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
        colorLayout = TextureLayout.Sampled;
        depthLayout = TextureLayout.DepthStencilAttachment;

        return new()
        {
            HdrColor = hdrColor,
            IndirectDiffuse = indirectDiffuse,
            DeviceDepth = deviceDepth,
            EncodedMotion = encodedMotion
        };
    }

    public void Dispose()
    {
        DestroyTargets();

        foreach (Buffer buffer in drawConstants)
        {
            buffer.Dispose();
        }

        environmentSampler.Dispose();
        shadowSampler.Dispose();
        materialSampler.Dispose();
        skyConstants.Dispose();
        skyPipeline.Dispose();
        doubleSidedPipeline.Dispose();
        opaquePipeline.Dispose();
    }

    private void DestroyTargets()
    {
        hardwareDepth?.Dispose();
        encodedMotion?.Dispose();
        deviceDepth?.Dispose();
        indirectDiffuse?.Dispose();
        hdrColor?.Dispose();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 640)]
file struct SceneConstants
{
    [FieldOffset(0)]
    public Matrix4x4 World;

    [FieldOffset(64)]
    public Matrix4x4 NormalWorld;

    [FieldOffset(128)]
    public SceneFrameConstants Frame;

    [FieldOffset(624)]
    public uint MaterialIndex;

    [FieldOffset(628)]
    public float WorldOrientation;

    [FieldOffset(632)]
    public uint Padding0;

    [FieldOffset(636)]
    public uint Padding1;
}

[StructLayout(LayoutKind.Explicit, Size = 496)]
file struct SceneFrameConstants
{
    [FieldOffset(0)]
    public Matrix4x4 ViewProjection;

    [FieldOffset(64)]
    public Matrix4x4 UnjitteredViewProjection;

    [FieldOffset(128)]
    public Matrix4x4 PreviousViewProjection;

    [FieldOffset(192)]
    public Matrix4x4 InverseViewProjection;

    [FieldOffset(256)]
    public Vector4 SunDirectionAndIntensity;

    [FieldOffset(272)]
    public Vector4 SunRadiance;

    [FieldOffset(288)]
    public Vector4 ZenithColor;

    [FieldOffset(304)]
    public Vector4 HorizonColor;

    [FieldOffset(320)]
    public Vector4 GroundColor;

    [FieldOffset(336)]
    public Vector4 CameraPositionWorld;

    [FieldOffset(352)]
    public Matrix4x4 ShadowViewProjection;

    [FieldOffset(416)]
    public Vector4 ShadowParameters;

    [FieldOffset(432)]
    public ResourceHandle Materials;

    [FieldOffset(440)]
    public ResourceHandle MaterialSampler;

    [FieldOffset(448)]
    public ResourceHandle ShadowTexture;

    [FieldOffset(456)]
    public ResourceHandle ShadowSampler;

    [FieldOffset(464)]
    public ResourceHandle EnvironmentTexture;

    [FieldOffset(472)]
    public ResourceHandle EnvironmentSampler;

    [FieldOffset(480)]
    public Vector4 RenderSize;
}
