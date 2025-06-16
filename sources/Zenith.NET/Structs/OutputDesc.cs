namespace Zenith.NET;

public record struct OutputDesc
{
    public PixelFormat[] ColorAttachments { get; set; }

    public PixelFormat? DepthStencilAttachment { get; set; }

    public SampleCount SampleCount { get; set; }
}
