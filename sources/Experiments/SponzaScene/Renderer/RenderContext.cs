using System.Numerics;
using SponzaScene.Models;
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

    public static float[] CSMSplits { get; } = [0.1f, 0.3f, 0.6f, 1.0f];

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
    public CSMData[] CSMDatas { get; } = new CSMData[CSMSplits.Length];

    public Texture? CSMDepths { get; private set; }

    public Texture[]? CSMNormalizedDepths { get; private set; }

    public FrameBuffer[]? CSMFrameBuffers { get; private set; }
    #endregion

    #region Intermediate Textures
    public Texture? GTAO { get; private set; }

    public Texture? GTAOBlurred { get; private set; }

    public Texture? VolumetricLight { get; private set; }

    public Texture? VolumetricLightBlurred { get; private set; }

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

        CSMDepths = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2DArray,
            Format = PixelFormat.D32Float,
            Width = 4096,
            Height = 4096,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = (uint)CSMSplits.Length,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.DepthStencil | TextureUsageFlags.ShaderResource
        });

        CSMNormalizedDepths = new Texture[CSMSplits.Length];
        CSMFrameBuffers = new FrameBuffer[CSMSplits.Length];
        for (int i = 0; i < CSMSplits.Length; i++)
        {
            CSMNormalizedDepths[i] = App.Context.CreateTexture(new()
            {
                Type = TextureType.Texture2D,
                Format = PixelFormat.R8G8B8A8UNorm,
                Width = 4096,
                Height = 4096,
                Depth = 1,
                MipLevels = 1,
                ArrayLayers = 1,
                SampleCount = SampleCount.Count1,
                Flags = TextureUsageFlags.RenderTarget | TextureUsageFlags.ShaderResource
            });

            CSMFrameBuffers[i] = App.Context.CreateFrameBuffer(new()
            {
                ColorAttachments = [new() { Target = CSMNormalizedDepths[i] }],
                DepthStencilAttachment = new() { Target = CSMDepths, Slice = new() { ArrayLayer = (uint)i } }
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

        GTAO = App.Context.CreateTexture(new()
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

        GTAOBlurred = App.Context.CreateTexture(new()
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

        VolumetricLight = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R16Float,
            Width = width / 4,
            Height = height / 4,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.ShaderResource | TextureUsageFlags.UnorderedAccess
        });

        VolumetricLightBlurred = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R16Float,
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
        VolumetricLightBlurred?.Dispose();
        VolumetricLight?.Dispose();
        GTAOBlurred?.Dispose();
        GTAO?.Dispose();

        if (CSMFrameBuffers is not null)
        {
            foreach (FrameBuffer frameBuffer in CSMFrameBuffers)
            {
                frameBuffer.Dispose();
            }
        }
        if (CSMNormalizedDepths is not null)
        {
            foreach (Texture texture in CSMNormalizedDepths)
            {
                texture.Dispose();
            }
        }
        CSMDepths?.Dispose();

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
