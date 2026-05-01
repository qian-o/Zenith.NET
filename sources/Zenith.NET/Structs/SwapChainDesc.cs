namespace Zenith.NET;

public record struct SwapChainDesc
{
    public Surface Surface;

    public PixelFormat ColorFormat;

    public PixelFormat? DepthStencilFormat;
}
