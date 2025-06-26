namespace Zenith.NET;

public abstract class TextureSubresource(GraphicsContext context, TextureSubresourceDesc desc) : GraphicsResource(context), IBindableResource, ITextureResource
{
    private TextureSubresourceDesc desc = desc;

    public ref readonly TextureSubresourceDesc Desc => ref desc;

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
