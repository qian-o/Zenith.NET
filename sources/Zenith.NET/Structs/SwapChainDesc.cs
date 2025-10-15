namespace Zenith.NET;

public record struct SwapChainDesc
{
    public Surface Surface;

    public PixelFormat ColorTargetFormat;

    public PixelFormat? DepthStencilTargetFormat;

    public bool VerticalSync;
}
