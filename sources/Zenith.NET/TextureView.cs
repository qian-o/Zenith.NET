namespace Zenith.NET;

public abstract class TextureView(GraphicsContext context, TextureViewDesc desc) : GraphicsResource(context), IBindableResource, ITexture
{
    private TextureViewDesc desc = desc;

    public ref readonly TextureViewDesc Desc => ref desc;

    public void Upload<T>(ReadOnlySpan<T> data, TextureSlice slice, TextureOffset offset, TextureExtent extent)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Texture subresource upload validation is not implemented yet.");
        }

        slice = new()
        {
            Face = slice.Face,
            Layer = Desc.FirstLayer + slice.Layer,
            MipLevel = Desc.MipLevel + slice.MipLevel
        };

        Desc.Texture.Upload(data, slice, offset, extent);
    }
}
