namespace Zenith.NET;

public struct FrameBufferDesc
{
    public FrameBufferAttachment[] ColorTargets { get; set; }

    public FrameBufferAttachment? DepthStencilTarget { get; set; }
}
