namespace Zenith.NET;

public record struct FrameBufferDesc
{
    public FrameBufferAttachmentDesc[] ColorTargets { get; set; }

    public FrameBufferAttachmentDesc? DepthStencilTarget { get; set; }
}
