namespace Zenith.NET;

public record struct FrameBufferAttachment
{
    public ITextureResource Target;

    public TextureSlice Slice;
}
