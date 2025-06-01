namespace Zenith.NET;

public record struct SwapChainDesc
{
    public SwapChainDesc()
    {
        Surface = null!;
        ColorTargetFormat = PixelFormat.R8G8B8A8UNorm;
        DepthStencilTargetFormat = PixelFormat.D24UNormS8UInt;
        VerticalSync = false;
    }

    /// <summary>
    /// The surface to present to.
    /// </summary>
    public ISurface Surface { get; set; }

    /// <summary>
    /// The pixel format of the color target.
    /// </summary>
    public PixelFormat ColorTargetFormat { get; set; }

    /// <summary>
    /// The pixel format of the depth stencil target.
    /// </summary>
    public PixelFormat? DepthStencilTargetFormat { get; set; }

    /// <summary>
    /// Vertical synchronization.
    /// </summary>
    public bool VerticalSync { get; set; }
}
