namespace Zenith.NET;

public record struct FrameBufferDesc
{
    public FrameBufferAttachment[] ColorTargets;

    public FrameBufferAttachment? DepthStencilTarget;
}
