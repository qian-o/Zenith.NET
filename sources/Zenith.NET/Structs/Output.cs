namespace Zenith.NET;

public record struct Output
{
    public PixelFormat[] ColorAttachments;

    public PixelFormat? DepthStencilAttachment;

    public SampleCount SampleCount;
}
