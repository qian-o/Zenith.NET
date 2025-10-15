namespace Zenith.NET;

public record struct FrameBufferDesc
{
    public FrameBufferAttachment[] ColorAttachments;

    public FrameBufferAttachment? DepthStencilAttachment;
}
