using System.Numerics;
using Zenith.NET;

namespace SponzaScene.Renderer;

internal class RenderContext : DisposableObject
{
    public static Output GBufferOutput { get; } = new()
    {
        ColorAttachments =
        [
            PixelFormat.R8G8B8A8UNorm,
            PixelFormat.R16G16B16A16Float,
            PixelFormat.R16G16B16A16Float,
            PixelFormat.R8G8B8A8UNorm,
            PixelFormat.R8G8B8A8UNorm,
            PixelFormat.R16G16B16A16Float
        ],
        DepthStencilAttachment = PixelFormat.D32FloatS8UInt,
        SampleCount = SampleCount.Count1
    };

    public static float[] CSMSplits { get; } = [0.05f, 0.15f, 0.3f, 0.5f, 1.0f];

    public static Output CSMOutput { get; } = new()
    {
        ColorAttachments = [PixelFormat.R8G8B8A8UNorm],
        DepthStencilAttachment = PixelFormat.D32Float,
        SampleCount = SampleCount.Count1
    };

    #region Frame Information
    public uint Width { get; private set; }

    public uint Height { get; private set; }

    public Matrix4x4 View { get; set; }

    public Matrix4x4 Projection { get; set; }

    public float NearPlane { get; set; }

    public float FarPlane { get; set; }

    public Vector3 CameraPosition { get; set; }

    public float Fov { get; set; }

    public float AspectRatio { get; set; }
    #endregion

    #region G-Buffer
    public Texture? Albedo { get; private set; }

    public Texture? Normal { get; private set; }

    public Texture? Position { get; private set; }

    public Texture? Depth { get; private set; }

    public Texture? NormalizedDepth { get; private set; }

    public Texture? MetallicRoughness { get; private set; }

    public Texture? Emissive { get; private set; }

    public FrameBuffer? GBufferFrameBuffer { get; private set; }
    #endregion

    #region Cascaded Shadow Maps
    public Texture? CSMDepthArray { get; private set; }

    public Texture? CSMNormalizedDepthArray { get; private set; }

    public FrameBuffer[]? CSMFrameBuffers { get; private set; }
    #endregion

    #region Intermediate Textures
    public Texture? SSAO { get; private set; }

    public Texture? SSAOBlurred { get; private set; }

    public Texture? HorizontalBloom { get; private set; }

    public Texture? VerticalBloom { get; private set; }

    public Texture? LitColor { get; private set; }
    #endregion

    public Texture? FinalColor { get; private set; }

    public void Initialize(uint width, uint height)
    {
        Destroy();

        Albedo = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R8G8B8A8UNorm,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.RenderTarget | TextureUsageFlags.ShaderResource
        });

        Normal = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R16G16B16A16Float,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.RenderTarget | TextureUsageFlags.ShaderResource
        });

        Position = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R16G16B16A16Float,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.RenderTarget | TextureUsageFlags.ShaderResource
        });

        Depth = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.D32FloatS8UInt,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.DepthStencil | TextureUsageFlags.ShaderResource
        });

        NormalizedDepth = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R8G8B8A8UNorm,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.RenderTarget | TextureUsageFlags.ShaderResource
        });

        CSMDepthArray = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2DArray,
            Format = PixelFormat.D32Float,
            Width = 4096,
            Height = 4096,
            Depth = (uint)CSMSplits.Length,
            MipLevels = 1,
            ArrayLayers = (uint)CSMSplits.Length,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.DepthStencil | TextureUsageFlags.ShaderResource
        });

        CSMNormalizedDepthArray = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2DArray,
            Format = PixelFormat.R8G8B8A8UNorm,
            Width = 4096,
            Height = 4096,
            Depth = (uint)CSMSplits.Length,
            MipLevels = 1,
            ArrayLayers = (uint)CSMSplits.Length,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.RenderTarget | TextureUsageFlags.ShaderResource
        });

        CSMFrameBuffers = new FrameBuffer[CSMSplits.Length];
        for (int i = 0; i < CSMSplits.Length; i++)
        {
            CSMFrameBuffers[i] = App.Context.CreateFrameBuffer(new()
            {
                ColorAttachments = [new() { Target = CSMNormalizedDepthArray, Slice = new() { ArrayLayer = (uint)i } }],
                DepthStencilAttachment = new() { Target = CSMDepthArray, Slice = new() { ArrayLayer = (uint)i } }
            });
        }

        MetallicRoughness = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R8G8B8A8UNorm,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.RenderTarget | TextureUsageFlags.ShaderResource
        });

        Emissive = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R16G16B16A16Float,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.RenderTarget | TextureUsageFlags.ShaderResource
        });

        GBufferFrameBuffer = App.Context.CreateFrameBuffer(new()
        {
            ColorAttachments =
            [
                new() { Target = Albedo },
                new() { Target = Normal },
                new() { Target = Position },
                new() { Target = NormalizedDepth },
                new() { Target = MetallicRoughness },
                new() { Target = Emissive }
            ],
            DepthStencilAttachment = new() { Target = Depth }
        });

        SSAO = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R8UNorm,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.ShaderResource | TextureUsageFlags.UnorderedAccess
        });

        SSAOBlurred = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R8UNorm,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.ShaderResource | TextureUsageFlags.UnorderedAccess
        });

        HorizontalBloom = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R16G16B16A16Float,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.ShaderResource | TextureUsageFlags.UnorderedAccess
        });

        VerticalBloom = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R16G16B16A16Float,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.ShaderResource | TextureUsageFlags.UnorderedAccess
        });

        LitColor = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R16G16B16A16Float,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.ShaderResource | TextureUsageFlags.UnorderedAccess
        });

        FinalColor = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R8G8B8A8UNorm,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.ShaderResource | TextureUsageFlags.UnorderedAccess
        });

        Width = width;
        Height = height;
    }

    protected override void Destroy()
    {
        FinalColor?.Dispose();

        LitColor?.Dispose();
        VerticalBloom?.Dispose();
        HorizontalBloom?.Dispose();
        SSAOBlurred?.Dispose();
        SSAO?.Dispose();

        if (CSMFrameBuffers is not null)
        {
            foreach (FrameBuffer frameBuffer in CSMFrameBuffers)
            {
                frameBuffer.Dispose();
            }
        }
        CSMNormalizedDepthArray?.Dispose();
        CSMDepthArray?.Dispose();

        GBufferFrameBuffer?.Dispose();
        Emissive?.Dispose();
        MetallicRoughness?.Dispose();
        NormalizedDepth?.Dispose();
        Depth?.Dispose();
        Position?.Dispose();
        Normal?.Dispose();
        Albedo?.Dispose();
    }
}
