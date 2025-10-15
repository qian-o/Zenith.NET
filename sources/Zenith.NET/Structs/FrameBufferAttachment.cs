namespace Zenith.NET;

public record struct FrameBufferAttachment
{
    public Texture Target;

    public TextureSlice Slice;
}
