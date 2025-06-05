using Zenith.NET.Helpers;

namespace Zenith.NET;

/// <summary>
/// Describes a swap chain, including the presentation surface, color and depth/stencil formats, and vertical sync setting.
/// </summary>
public record struct SwapChainDesc : IDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SwapChainDesc"/> struct with default values.
    /// </summary>
    public SwapChainDesc()
    {
        Surface = new();
        ColorTargetFormat = PixelFormat.R8G8B8A8UNorm;
        DepthStencilTargetFormat = null;
        VerticalSync = true;
    }

    /// <summary>
    /// The surface to present to.
    /// </summary>
    public SurfaceDesc Surface { get; set; }

    /// <summary>
    /// The pixel format of the color target.
    /// </summary>
    public PixelFormat ColorTargetFormat { get; set; }

    /// <summary>
    /// The pixel format of the depth/stencil target.
    /// </summary>
    public PixelFormat? DepthStencilTargetFormat { get; set; }

    /// <summary>
    /// Indicates whether vertical synchronization is enabled.
    /// </summary>
    public bool VerticalSync { get; set; }

    /// <summary>
    /// Validates the current <see cref="SwapChainDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
    public readonly bool Validate()
    {
        if (!Surface.Validate())
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
