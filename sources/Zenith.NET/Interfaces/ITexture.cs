namespace Zenith.NET;

public interface ITexture : IBindableResource, IDisposableObject
{
    void Upload<T>(ReadOnlySpan<T> data, TextureSlice slice, TextureOffset offset, TextureExtent extent);
}