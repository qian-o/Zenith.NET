namespace Zenith.NET;

public abstract class TextureView(GraphicsContext context, TextureViewDesc desc) : GraphicsResource(context), ITexture
{
    private TextureViewDesc desc = desc;

    public ref readonly TextureViewDesc Desc => ref desc;

    public void Upload<T>(TextureSlice slice, TextureOffset offset, TextureExtent extent, ReadOnlySpan<T> data)
    {
        slice = new()
        {
            Face = slice.Face,
            Layer = Desc.FirstLayer + slice.Layer,
            MipLevel = Desc.FirstMipLevel + slice.MipLevel
        };

        Desc.Texture.Upload(slice, offset, extent, data);
    }
}
