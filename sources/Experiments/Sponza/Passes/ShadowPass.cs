using System.Numerics;
using System.Runtime.InteropServices;
using Sponza.Helpers;
using Sponza.Models;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace Sponza.Passes;

internal class ShadowPass : IDisposable
{
    private readonly GraphicsContext context;
    private readonly Texture shadow;
    private readonly TextureView sampledView;
    private readonly Sampler materialSampler;
    private readonly GraphicsPipeline opaquePipeline;
    private readonly GraphicsPipeline opaqueDoubleSidedPipeline;
    private readonly GraphicsPipeline maskPipeline;
    private readonly GraphicsPipeline maskDoubleSidedPipeline;

    private Buffer[] constantBuffers = [];
    private Matrix4x4 viewProjection;
    private bool dirty = true;
    private bool initialized;

    public ShadowPass(GraphicsContext context)
    {
        this.context = context;
        shadow = context.CreateTexture(TextureDesc.DepthStencilAttachment(PixelFormat.D32Float, 4096, 4096, SampleCount.Count1));
        sampledView = context.CreateTextureView(TextureViewDesc.Texture2D(shadow, PixelFormat.R32Float, 0, 1));
        materialSampler = context.CreateSampler(SamplerDesc.Anisotropic(8));

        InputLayout inputLayout = new()
        {
            Elements =
            [
                new() { Format = ElementFormat.Float3, Semantic = ElementSemantic.Position, OffsetInBytes = 0 },
                new() { Format = ElementFormat.Float2, Semantic = ElementSemantic.TexCoord, OffsetInBytes = 40 }
            ],
            StrideInBytes = 48
        };

        using Shader vertexShader = GraphicsHelper.LoadShader(context, "Shadow.slang", "VSMain");
        using Shader opaqueShader = GraphicsHelper.LoadShader(context, "Shadow.slang", "OpaqueFS");
        using Shader maskShader = GraphicsHelper.LoadShader(context, "Shadow.slang", "MaskFS");

        opaquePipeline = CreatePipeline(vertexShader, opaqueShader, inputLayout, CullMode.Back);
        opaqueDoubleSidedPipeline = CreatePipeline(vertexShader, opaqueShader, inputLayout, CullMode.None);
        maskPipeline = CreatePipeline(vertexShader, maskShader, inputLayout, CullMode.Back);
        maskDoubleSidedPipeline = CreatePipeline(vertexShader, maskShader, inputLayout, CullMode.None);
    }

    public void Invalidate()
    {
        dirty = true;
    }

    public ShadowData Record(CommandBuffer commandBuffer, ShadowPassArgs args)
    {
        if (!dirty)
        {
            return Data();
        }

        SceneData scene = args.Scene;
        if (constantBuffers.Length != scene.Draws.Length)
        {
            DisposeConstantBuffers();
            constantBuffers = new Buffer[scene.Draws.Length];
            for (int index = 0; index < constantBuffers.Length; index++)
            {
                constantBuffers[index] = GraphicsHelper.CreateConstantBuffer<ShadowConstants>(context);
            }
        }

        viewProjection = CreateViewProjection(scene.BoundsMinimumWorld, scene.BoundsMaximumWorld, args.Sky.SunDirectionWorld);

        for (int index = 0; index < scene.Draws.Length; index++)
        {
            DrawData draw = scene.Draws[index];
            ShadowConstants constants = new()
            {
                World = draw.World,
                ViewProjection = viewProjection,
                Materials = scene.MaterialBuffer.StorageReadOnlyHandle,
                MaterialSampler = materialSampler.Handle,
                MaterialIndex = draw.MaterialIndex,
                Padding0 = 0,
                Padding1 = 0,
                Padding2 = 0
            };
            GraphicsHelper.Upload(constantBuffers[index], constants);
        }

        commandBuffer.Transition(shadow, default, initialized ? TextureLayout.Sampled : TextureLayout.Undefined, TextureLayout.DepthStencilAttachment);
        commandBuffer.BeginRenderPass([], DepthStencilAttachment.Clear(shadow, 1.0f, 0));

        for (int index = 0; index < scene.Draws.Length; index++)
        {
            DrawData draw = scene.Draws[index];
            MaterialData material = scene.Materials[draw.MaterialIndex];
            GraphicsPipeline pipeline = material.AlphaMasked ? material.DoubleSided ? maskDoubleSidedPipeline : maskPipeline : material.DoubleSided ? opaqueDoubleSidedPipeline : opaquePipeline;

            commandBuffer.SetPipeline(pipeline);
            commandBuffer.SetVertexBuffer(scene.VertexBuffer, 0, 0);
            commandBuffer.SetIndexBuffer(scene.IndexBuffer, 0, IndexFormat.UInt16);
            commandBuffer.SetConstantBuffer(constantBuffers[index], 0);
            commandBuffer.DrawIndexed(draw.IndexCount, 1, draw.FirstIndex, draw.VertexOffset, 0);
        }

        commandBuffer.EndRenderPass();
        commandBuffer.Transition(shadow, default, TextureLayout.DepthStencilAttachment, TextureLayout.Sampled);

        dirty = false;
        initialized = true;

        return Data();
    }

    public void Dispose()
    {
        DisposeConstantBuffers();
        maskDoubleSidedPipeline.Dispose();
        maskPipeline.Dispose();
        opaqueDoubleSidedPipeline.Dispose();
        opaquePipeline.Dispose();
        materialSampler.Dispose();
        sampledView.Dispose();
        shadow.Dispose();
    }

    private GraphicsPipeline CreatePipeline(Shader vertexShader, Shader fragmentShader, InputLayout inputLayout, CullMode cullMode)
    {
        return context.CreateGraphicsPipeline(new()
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            InputLayouts = [inputLayout],
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            AttachmentFormats = new()
            {
                ColorFormats = [],
                DepthStencilFormat = PixelFormat.D32Float,
                SampleCount = SampleCount.Count1
            },
            RenderState = new()
            {
                Rasterizer = RasterizerState.CullBack() with { CullMode = cullMode, DepthBiasSlopeScale = 1.1f },
                DepthStencil = DepthStencilState.DepthReadWrite(),
                Blend = BlendState.ColorDisabled()
            }
        });
    }

    private ShadowData Data()
    {
        return new()
        {
            SampledView = sampledView,
            ViewProjection = viewProjection,
            NormalBiasInMeters = 0.015f,
            DepthBias = 0.0002f,
            TexelSize = 1.0f / shadow.Desc.Width
        };
    }

    private void DisposeConstantBuffers()
    {
        for (int index = constantBuffers.Length - 1; index >= 0; index--)
        {
            constantBuffers[index].Dispose();
        }

        constantBuffers = [];
    }

    private static Matrix4x4 CreateViewProjection(Vector3 minimumWorld, Vector3 maximumWorld, Vector3 sunDirectionWorld)
    {
        Vector3 center = (minimumWorld + maximumWorld) * 0.5f;
        float span = MathF.Max(Vector3.Distance(minimumWorld, maximumWorld), 0.1f);
        Vector3 referenceUp = Vector3.UnitX;
        Matrix4x4 view = Matrix4x4.CreateLookAt(center + sunDirectionWorld * span, center, referenceUp);
        Vector3 minimumLight = new(float.MaxValue);
        Vector3 maximumLight = new(float.MinValue);

        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 position = new((corner & 1) is 0 ? minimumWorld.X : maximumWorld.X, (corner & 2) is 0 ? minimumWorld.Y : maximumWorld.Y, (corner & 4) is 0 ? minimumWorld.Z : maximumWorld.Z);
            Vector3 positionLight = Vector3.Transform(position, view);
            minimumLight = Vector3.Min(minimumLight, positionLight);
            maximumLight = Vector3.Max(maximumLight, positionLight);
        }

        const float MarginInMeters = 0.4f;
        float nearPlane = MathF.Max(0.1f, -maximumLight.Z - MarginInMeters);
        float farPlane = MathF.Max(nearPlane + 0.1f, -minimumLight.Z + MarginInMeters);
        Matrix4x4 projection = Matrix4x4.CreateOrthographicOffCenter(minimumLight.X - MarginInMeters, maximumLight.X + MarginInMeters, minimumLight.Y - MarginInMeters, maximumLight.Y + MarginInMeters, nearPlane, farPlane);

        return view * projection;
    }
}

[StructLayout(LayoutKind.Explicit, Size = 160)]
file struct ShadowConstants
{
    [FieldOffset(0)]
    public Matrix4x4 World;

    [FieldOffset(64)]
    public Matrix4x4 ViewProjection;

    [FieldOffset(128)]
    public ResourceHandle Materials;

    [FieldOffset(136)]
    public ResourceHandle MaterialSampler;

    [FieldOffset(144)]
    public uint MaterialIndex;

    [FieldOffset(148)]
    public uint Padding0;

    [FieldOffset(152)]
    public uint Padding1;

    [FieldOffset(156)]
    public uint Padding2;
}
