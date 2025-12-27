using System.Numerics;
using Zenith.NET;

namespace SponzaScene.Renderer;

internal class RenderContext : DisposableObject
{
    #region Frame Information
    public uint Width { get; private set; }

    public uint Height { get; private set; }

    public Matrix4x4 View { get; set; }

    public Matrix4x4 Projection { get; set; }

    public Vector3 CameraPosition { get; set; }
    #endregion

    #region G-Buffer Textures
    public Texture? Albedo { get; private set; }

    public Texture? Normal { get; private set; }

    public Texture? Position { get; private set; }

    public Texture? Depth { get; private set; }
    #endregion

    #region Intermediate Textures
    public Texture? SSAOResult { get; private set; }
    #endregion

    #region Final Composite
    public Texture? FinalColor { get; private set; }
    #endregion

    #region Frame Buffers
    public static Output GBufferOutput { get; } = new()
    {
        ColorAttachments = [PixelFormat.R8G8B8A8UNorm, PixelFormat.R16G16B16A16Float, PixelFormat.R16G16B16A16Float],
        DepthStencilAttachment = PixelFormat.D32FloatS8UInt,
        SampleCount = SampleCount.Count1
    };

    public static Output SSAOOutput { get; } = new()
    {
        ColorAttachments = [PixelFormat.R8UNorm],
        SampleCount = SampleCount.Count1
    };

    public static Output ComposeOutput { get; } = new()
    {
        ColorAttachments = [PixelFormat.R8G8B8A8UNorm],
        SampleCount = SampleCount.Count1
    };

    public FrameBuffer? GBufferFrameBuffer { get; private set; }

    public FrameBuffer? SSAOFrameBuffer { get; private set; }

    public FrameBuffer? ComposeFrameBuffer { get; private set; }
    #endregion

    public void Initialize(uint width, uint height)
    {
        if (Width == width || Height == height)
        {
            return;
        }

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

        SSAOResult = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.R8UNorm,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.RenderTarget | TextureUsageFlags.ShaderResource
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
            Flags = TextureUsageFlags.RenderTarget | TextureUsageFlags.ShaderResource
        });

        GBufferFrameBuffer = App.Context.CreateFrameBuffer(new()
        {
            ColorAttachments =
            [
                new() { Target = Albedo },
                new() { Target = Normal },
                new() { Target = Position }
            ],
            DepthStencilAttachment = new() { Target = Depth }
        });

        SSAOFrameBuffer = App.Context.CreateFrameBuffer(new()
        {
            ColorAttachments =
            [
                new() { Target = SSAOResult }
            ]
        });

        ComposeFrameBuffer = App.Context.CreateFrameBuffer(new()
        {
            ColorAttachments =
            [
                new() { Target = FinalColor }
            ]
        });

        Width = width;
        Height = height;
    }

    protected override void Destroy()
    {
        ComposeFrameBuffer?.Dispose();
        SSAOFrameBuffer?.Dispose();
        GBufferFrameBuffer?.Dispose();

        FinalColor?.Dispose();

        SSAOResult?.Dispose();

        Depth?.Dispose();
        Position?.Dispose();
        Normal?.Dispose();
        Albedo?.Dispose();
    }
}
