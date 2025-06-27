namespace Zenith.NET;

public interface ITexture
{
    void Upload<T>(ReadOnlySpan<T> data, TextureSlice slice, TextureOffset offset, TextureExtent extent);
}