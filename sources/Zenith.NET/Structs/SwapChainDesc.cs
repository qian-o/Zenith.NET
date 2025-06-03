using Zenith.NET.Helpers;

namespace Zenith.NET;

public record struct SwapChainDesc : IDesc
{
    public SwapChainDesc()
    {
        Surface = null!;
        ColorTargetFormat = PixelFormat.R8G8B8A8UNorm;
        DepthStencilTargetFormat = null;
        VerticalSync = true;
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

    public readonly bool Validate()
    {
        if (Surface is null)
        {
            return false;
        }

        if (ColorTargetFormat is not PixelFormat.R8G8B8A8UNorm
            or PixelFormat.R8G8B8A8UNormSRgb
            or PixelFormat.R16G16B16A16Float
            or PixelFormat.B8G8R8A8UNorm
            or PixelFormat.B8G8R8A8UNormSRgb)
        {
            return false;
        }

        if (Validation.IsValidDepthStencilFormat(DepthStencilTargetFormat))
        {
            return false;
        }

        return true;
    }
}
