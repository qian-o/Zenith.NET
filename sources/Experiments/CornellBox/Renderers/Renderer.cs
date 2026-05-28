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

    public AttachmentFormats AttachmentFormats => new()
    {
        ColorFormats = [Color.Desc.Format],
        DepthStencilFormat = DepthStencil.Desc.Format,
        SampleCount = Color.Desc.SampleCount
    };

    public abstract void Update(CameraHandler camera);

    public abstract void Render(CommandBuffer commandBuffer);

    public virtual void Resize(uint width, uint height)
    {
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
            Usages = TextureUsages.Sampled | TextureUsages.Storage | TextureUsages.ColorAttachment
        });

        DepthStencil = App.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = PixelFormat.D32FloatS8UInt,
            Width = width,
            Height = height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Usages = TextureUsages.DepthStencilAttachment
        });

    }

    public virtual void Dispose()
    {
        DepthStencil.Dispose();
        Color.Dispose();
    }

    protected static string ShaderPath(params string[] paths)
    {
        return Path.Combine([AppContext.BaseDirectory, "Assets", "Shaders", .. paths]);
    }
}
