namespace Zenith.NET;

public abstract class TextureView(GraphicsContext context, TextureViewDesc desc) : GraphicsResource(context), ITexture
{
    private TextureViewDesc desc = desc;

    public ref readonly TextureViewDesc Desc => ref desc;

    public void Upload<T>(ReadOnlySpan<T> data, TextureSlice slice, TextureOffset offset, TextureExtent extent)
    {
        slice = new()
        {
            Face = slice.Face,
            Layer = Desc.FirstLayer + slice.Layer,
            MipLevel = Desc.FirstMipLevel + slice.MipLevel
        };

        Desc.Texture.Upload(data, slice, offset, extent);
    }
}
