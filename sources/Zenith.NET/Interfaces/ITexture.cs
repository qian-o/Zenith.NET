namespace Zenith.NET;

public interface ITexture : IBindableResource
{
    void Upload<T>(TextureSlice slice, TextureOffset offset, TextureExtent extent, ReadOnlySpan<T> data);
}