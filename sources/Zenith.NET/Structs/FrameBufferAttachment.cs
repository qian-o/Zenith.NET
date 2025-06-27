namespace Zenith.NET;

public record struct FrameBufferAttachment
{
    public ITexture Target;

    public TextureSlice Slice;
}
