namespace Zenith.NET;

public record struct AttachmentFormats
{
    public PixelFormat[] ColorFormats;

    public PixelFormat? DepthStencilFormat;

    public SampleCount SampleCount;
}
