using Zenith.NET.Helpers;

namespace Zenith.NET;

public record struct SwapChainDesc : IDesc
{
    public SwapChainDesc()
    {
        Surface = new();
        ColorTargetFormat = PixelFormat.R8G8B8A8UNorm;
        DepthStencilTargetFormat = null;
        VerticalSync = true;
    }

    public SurfaceDesc Surface { get; set; }

    public PixelFormat ColorTargetFormat { get; set; }

    public PixelFormat? DepthStencilTargetFormat { get; set; }

    public bool VerticalSync { get; set; }

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
