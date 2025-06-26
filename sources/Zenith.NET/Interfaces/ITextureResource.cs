namespace Zenith.NET;

public interface ITextureResource
{
    void Upload<T>(ReadOnlySpan<T> data, TextureSlice slice, TextureOffset offset, TextureExtent extent);
}