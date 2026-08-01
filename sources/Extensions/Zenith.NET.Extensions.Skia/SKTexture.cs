using SkiaSharp;

namespace Zenith.NET.Extensions.Skia;

public class SKTexture : DisposableObject
{
    private SKTextureDesc desc;

    internal SKTexture(GraphicsContext context, GRContext grContext, SKTextureDesc desc)
    {
        throw new NotImplementedException();
    }

    public ref readonly SKTextureDesc Desc => ref desc;

    public void Render(TextureLayout currentLayout, TextureLayout finalLayout, Action<SKCanvas> render)
    {
        throw new NotImplementedException();
    }

    protected override void Destroy()
    {
        throw new NotImplementedException();
    }

    public static implicit operator Texture(SKTexture texture)
    {
        throw new NotImplementedException();
    }
}
