namespace Zenith.NET;

public struct SwapChainDesc
{
    public Surface Surface { get; set; }

    public PixelFormat ColorTargetFormat { get; set; }

    public PixelFormat? DepthStencilTargetFormat { get; set; }

    public bool VerticalSync { get; set; }
}
