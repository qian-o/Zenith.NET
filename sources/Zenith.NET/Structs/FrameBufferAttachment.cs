namespace Zenith.NET;

public record struct FrameBufferAttachment
{
    public Texture Target;

    public uint FaceIndex;

    public uint ArrayLayer;

    public uint MipLevel;
}
