namespace Zenith.NET;

public record struct SwapChainDesc
{
    public SurfaceDesc Surface { get; set; }

    public PixelFormat ColorTargetFormat { get; set; }

    public PixelFormat? DepthStencilTargetFormat { get; set; }

    public bool VerticalSync { get; set; }
}
