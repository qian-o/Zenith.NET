namespace Zenith.NET;

public record struct FrameBufferAttachmentDesc
{
    public Texture Target { get; set; }

    public uint FaceIndex { get; set; }

    public uint ArrayLayer { get; set; }

    public uint MipLevel { get; set; }
}
