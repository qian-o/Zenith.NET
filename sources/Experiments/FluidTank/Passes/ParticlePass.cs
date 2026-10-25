using System.Numerics;
using System.Runtime.InteropServices;
using FluidTank.Handlers;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace FluidTank.Passes;

internal unsafe class ParticlePass : DisposableObject
{
    private readonly Buffer constants;
    private readonly GraphicsPipeline pipeline;

    public ParticlePass()
    {
        constants = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(ParticleConstants),
            Usages = BufferUsages.Constant,
            Residency = MemoryResidency.CpuWriteOnly
        });

        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", "Particles.slang");
        using Shader vertexShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "ParticleVS"));
        using Shader fragmentShader = App.Context.CreateShader(ZenithCompiler.CompileFromFile(App.Context.GraphicsApi, shaderPath, "ParticleFS"));

        pipeline = App.Context.CreateGraphicsPipeline(new()
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            InputLayouts = [],
            PrimitiveTopology = PrimitiveTopology.TriangleStrip,
            AttachmentFormats = new()
            {
                ColorFormats = [PixelFormat.R16G16B16A16Float],
                DepthStencilFormat = PixelFormat.D32FloatS8UInt,
                SampleCount = SampleCount.Count1
            },
            RenderState = new()
            {
                Rasterizer = RasterizerState.CullNone(),
                DepthStencil = DepthStencilState.DepthReadWrite(),
                Blend = BlendState.Opaque()
            }
        });
    }

    public void Record(CommandBuffer commandBuffer, Simulation simulation, CameraHandler camera, Texture color, Texture depthStencil, float interpolationAlpha)
    {
        ParticleConstants particleData = new()
        {
            View = camera.View,
            Projection = camera.Projection,
            CameraRight = camera.Right,
            Radius = Simulation.ParticleRadius * 0.78f,
            CameraUp = camera.Up,
            InterpolationAlpha = interpolationAlpha,
            Particles = simulation.Particles.StorageReadOnlyHandle,
            PreviousPositions = simulation.PreviousPositions.StorageReadOnlyHandle
        };

        constants.Upload(0, new()
        {
            Pointer = (nint)(&particleData),
            SizeInBytes = (uint)sizeof(ParticleConstants)
        });

        commandBuffer.Transition(color, default, TextureLayout.Sampled, TextureLayout.ColorAttachment);
        commandBuffer.Transition(depthStencil, default, TextureLayout.DepthStencilAttachment, TextureLayout.DepthStencilAttachment);
        commandBuffer.BeginRenderPass([ColorAttachment.Load(color)], DepthStencilAttachment.Load(depthStencil));
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.SetConstantBuffer(constants, 0);
        commandBuffer.Draw(4, Simulation.ParticleCount, 0, 0);
        commandBuffer.EndRenderPass();
        commandBuffer.Transition(color, default, TextureLayout.ColorAttachment, TextureLayout.Sampled);
    }

    protected override void Destroy()
    {
        pipeline?.Dispose();
        constants?.Dispose();
    }
}

[StructLayout(LayoutKind.Explicit, Size = 176)]
file struct ParticleConstants
{
    [FieldOffset(0)]
    public Matrix4x4 View;

    [FieldOffset(64)]
    public Matrix4x4 Projection;

    [FieldOffset(128)]
    public Vector3 CameraRight;

    [FieldOffset(140)]
    public float Radius;

    [FieldOffset(144)]
    public Vector3 CameraUp;

    [FieldOffset(156)]
    public float InterpolationAlpha;

    [FieldOffset(160)]
    public ResourceHandle Particles;

    [FieldOffset(168)]
    public ResourceHandle PreviousPositions;
}
