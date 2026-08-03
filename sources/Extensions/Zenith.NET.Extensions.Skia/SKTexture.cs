using SkiaSharp;

namespace Zenith.NET.Extensions.Skia;

public class SKTexture : DisposableObject
{
    private readonly Texture texture;
    private readonly SKSurface surface;

    internal SKTexture(SKRenderer renderer, SKTextureDesc desc)
    {
        Renderer = renderer;

        using GRBackendTexture backendTexture = renderer.CreateBackendTexture(texture = renderer.Context.CreateTexture(new()
        {
            Type = TextureType.Texture2D,
            Format = desc.Format,
            Width = desc.Width,
            Height = desc.Height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Usages = TextureUsages.Sampled | TextureUsages.ColorAttachment | TextureUsages.TransferSrc | TextureUsages.TransferDst
        }));

        surface = SKSurface.Create(renderer.GRContext, backendTexture, GRSurfaceOrigin.TopLeft, (int)SKFormats.Skia(desc.SampleCount), SKFormats.Skia(desc.Format));
    }

    internal SKRenderer Renderer { get; }

    public ref readonly TextureDesc Desc => ref texture.Desc;

    public void Render(Action<SKCanvas> render)
    {
        Renderer.Render(surface, render);
    }

    protected override void Destroy()
    {
        surface.Dispose();
        texture.Dispose();

        Extensions.ReleaseRenderer(Renderer);
    }

    public static implicit operator Texture(SKTexture texture)
    {
        return texture.texture;
    }
}
