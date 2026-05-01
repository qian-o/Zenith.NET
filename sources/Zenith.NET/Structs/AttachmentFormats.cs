namespace Zenith.NET;

public record struct AttachmentFormats
{
    public PixelFormat[] Colors;

    public PixelFormat? DepthStencil;

    public SampleCount SampleCount;
}
