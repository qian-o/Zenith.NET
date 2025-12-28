using System.Numerics;
using Hexa.NET.ImGui;
using Zenith.NET;
using Buffer = Zenith.NET.Buffer;

namespace SponzaScene.Renderer;

internal unsafe class SSAOPass : FullscreenPass
{
    private const int KernelSize = 64;
    private const int NoiseSize = 4;

    private readonly Buffer constantBuffer;
    private readonly Buffer kernelBuffer;
    private readonly Texture noiseTexture;

    private ResourceSet? resourceSet;

    private float radius = 0.5f;
    private float bias = 0.025f;
    private float intensity = 1.5f;

    public SSAOPass() : base("SSAOPass")
    {
        constantBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)sizeof(SSAOConstants),
            StrideInBytes = (uint)sizeof(SSAOConstants),
            Flags = BufferUsageFlags.Constant | BufferUsageFlags.MapWrite
        });

        Vector4[] kernel = GenerateKernel(KernelSize);
        kernelBuffer = App.Context.CreateBuffer(new()
        {
            SizeInBytes = (uint)(sizeof(Vector4) * KernelSize),
            StrideInBytes = (uint)sizeof(Vector4),
            Flags = BufferUsageFlags.ShaderResource
        });
        kernelBuffer.Upload(kernel, 0);

        noiseTexture = CreateNoiseTexture(NoiseSize);
    }

    protected override string ShaderName => "SSAO";

    protected override Output Output => RenderContext.SSAOOutput;

    protected override ResourceLayout? CreateResourceLayout()
    {
        return App.Context.CreateResourceLayout(new()
        {
            Bindings =
            [
                new() { Type = ResourceType.ConstantBuffer, Index = 0, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.StructuredBuffer, Index = 0, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Texture, Index = 1, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Texture, Index = 2, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Texture, Index = 3, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Texture, Index = 4, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Sampler, Index = 0, Count = 1, StageFlags = ShaderStageFlags.Pixel },
                new() { Type = ResourceType.Sampler, Index = 1, Count = 1, StageFlags = ShaderStageFlags.Pixel }
            ]
        });
    }

    protected override ResourceSet EnsureResourceSet(ResourceLayout resourceLayout, RenderContext context)
    {
        return resourceSet ??= App.Context.CreateResourceSet(new()
        {
            Layout = resourceLayout,
            Resources =
            [
                constantBuffer,
                kernelBuffer,
                context.Position!,
                context.Normal!,
                context.LinearDepth!,
                noiseTexture,
                App.PointSampler,
                App.LinearSampler
            ]
        });
    }

    protected override (FrameBuffer? FrameBuffer, ClearValue ClearValue) GetTarget(RenderContext context)
    {
        return (context.SSAOFrameBuffer, ClearValues.Default);
    }

    protected override void UpdateResources(RenderContext context)
    {
        constantBuffer?.Upload([new SSAOConstants
        {
            ViewProjection = context.View * context.Projection,
            ScreenSize = new Vector2(context.Width, context.Height),
            NoiseScale = new Vector2(context.Width / (float)NoiseSize, context.Height / (float)NoiseSize),
            Radius = radius,
            Bias = bias,
            Intensity = intensity,
            KernelSize = KernelSize
        }], 0);
    }

    public override void DebugUI(RenderContext context)
    {
        if (ImGui.Begin("SSAO"))
        {
            ImGui.SliderFloat("Radius", ref radius, 0.01f, 2.0f);
            ImGui.SliderFloat("Bias", ref bias, 0.001f, 0.1f);
            ImGui.SliderFloat("Intensity", ref intensity, 0.1f, 5.0f);

            ImGui.Separator();

            Vector2 size = new(ImGui.GetContentRegionAvail().X);
            size = size with { Y = size.X * context.Height / context.Width };

            ImGui.Image(App.Binding(context.SSAO!), size);
        }
        ImGui.End();
    }

    public override void Resize(uint width, uint height)
    {
        resourceSet?.Dispose();
        resourceSet = null;
    }

    protected override void Destroy()
    {
        resourceSet?.Dispose();

        noiseTexture.Dispose();
        kernelBuffer.Dispose();
        constantBuffer.Dispose();

        base.Destroy();
    }

    #region Kernel & Noise Generation
    private static Vector4[] GenerateKernel(int size)
    {
        Random random = new(42);
        Vector4[] kernel = new Vector4[size];

        for (int i = 0; i < size; i++)
        {
            Vector3 sample = new((float)((random.NextDouble() * 2.0) - 1.0),
                                 (float)((random.NextDouble() * 2.0) - 1.0),
                                 (float)random.NextDouble());

            sample = Vector3.Normalize(sample) * (float)random.NextDouble();

            float scale = (float)i / size;
            scale = float.Lerp(0.1f, 1.0f, scale * scale);
            sample *= scale;

            kernel[i] = new Vector4(sample, 0);
        }

        return kernel;
    }

    private static Texture CreateNoiseTexture(int size)
    {
        Random random = new(42);
        byte[] pixels = new byte[size * size * 4];

        for (int i = 0; i < size * size; i++)
        {
            float x = (float)((random.NextDouble() * 2.0) - 1.0);
            float y = (float)((random.NextDouble() * 2.0) - 1.0);

            pixels[(i * 4) + 0] = (byte)(((x * 0.5f) + 0.5f) * 255);
            pixels[(i * 4) + 1] = (byte)(((y * 0.5f) + 0.5f) * 255);
            pixels[(i * 4) + 2] = 128;
            pixels[(i * 4) + 3] = 255;
        }

        Texture texture = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R8G8B8A8UNorm,
            Width = (uint)size,
            Height = (uint)size,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.ShaderResource
        });
        texture.Upload(pixels, default, default, new() { Width = (uint)size, Height = (uint)size, Depth = 1 });

        return texture;
    }
    #endregion

    private struct SSAOConstants
    {
        public Matrix4x4 ViewProjection;

        public Vector2 ScreenSize;

        public Vector2 NoiseScale;

        public float Radius;

        public float Bias;

        public float Intensity;

        public int KernelSize;
    }
}
