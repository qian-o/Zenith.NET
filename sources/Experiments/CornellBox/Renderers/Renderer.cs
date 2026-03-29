using CornellBox.Handlers;
using Zenith.NET;

namespace CornellBox.Renderers;

internal abstract class Renderer : IDisposable
{
    protected Renderer()
    {
        Resize(App.Width, App.Height);
    }

    public Texture Color { get; private set; } = null!;

    public Texture DepthStencil { get; private set; } = null!;

    public FrameBuffer FrameBuffer { get; private set; } = null!;

    public abstract void Update(CameraHandler camera);

    public abstract void Render(CommandBuffer commandBuffer);

    public virtual void Resize(uint width, uint height)
    {
        FrameBuffer?.Dispose();
        DepthStencil?.Dispose();
        Color?.Dispose();

        Color = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.B8G8R8A8UNorm,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.RenderTarget | TextureUsageFlags.ShaderResource | TextureUsageFlags.UnorderedAccess
        });

        DepthStencil = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.D24UNormS8UInt,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.DepthStencil
        });

        FrameBuffer = App.Context.CreateFrameBuffer(new()
        {
            ColorAttachments = [new() { Target = Color }],
            DepthStencilAttachment = new() { Target = DepthStencil }
        });
    }

    public virtual void Dispose()
    {
        FrameBuffer.Dispose();
        DepthStencil.Dispose();
        Color.Dispose();
    }
}
