namespace Zenith.NET;

public struct FrameBufferAttachment
{
    public Texture Target { get; set; }

    public uint FaceIndex { get; set; }

    public uint ArrayLayer { get; set; }

    public uint MipLevel { get; set; }
}
