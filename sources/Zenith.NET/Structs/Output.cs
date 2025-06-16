namespace Zenith.NET;

public struct Output
{
    public PixelFormat[] ColorAttachments { get; set; }

    public PixelFormat? DepthStencilAttachment { get; set; }

    public SampleCount SampleCount { get; set; }
}
