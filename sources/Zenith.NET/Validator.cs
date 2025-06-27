namespace Zenith.NET;

internal class Validator(GraphicsContext context)
{
    private static readonly PixelFormat[] swapChainFormats =
    [
        PixelFormat.R8G8B8A8UNorm,
        PixelFormat.R8G8B8A8UNormSRgb,
        PixelFormat.R16G16B16A16Float,
        PixelFormat.B8G8R8A8UNorm,
        PixelFormat.B8G8R8A8UNormSRgb
    ];

    private static readonly PixelFormat[] depthStencilFormats =
    [
        PixelFormat.D24UNormS8UInt,
        PixelFormat.D32FloatS8UInt
    ];

    public void ValidateSwapChainDesc(SwapChainDesc desc)
    {
        ValidateSurface(desc.Surface);

        if (!swapChainFormats.Contains(desc.ColorTargetFormat))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid color target format: {desc.ColorTargetFormat}. Supported formats are: {string.Join(", ", swapChainFormats.Select(f => f.ToString()))}.");
        }

        if (desc.DepthStencilTargetFormat.HasValue && !depthStencilFormats.Contains(desc.DepthStencilTargetFormat.Value))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid depth-stencil target format: {desc.DepthStencilTargetFormat.Value}. Supported formats are: {string.Join(", ", depthStencilFormats.Select(f => f.ToString()))}.");
        }
    }

    private void ValidateSurface(Surface surface)
    {
        if (!Enum.IsDefined(surface.Type))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, $"Invalid surface type: {surface.Type}. Supported types are: {string.Join(", ", Enum.GetNames<SurfaceType>())}.");

            return;
        }

        if (surface.Handles is null)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Surface handles cannot be null.");

            return;
        }

        if (surface.Type is SurfaceType.Win32 && surface.Handles.Length is not 1)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Win32 surface must have exactly one handle (HWND).");
        }
        else if (surface.Type is SurfaceType.Wayland && surface.Handles.Length is not 2)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Wayland surface must have exactly two handles (display and surface).");
        }
        else if (surface.Type is SurfaceType.Xlib && surface.Handles.Length is not 2)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Xlib surface must have exactly two handles (display and window).");
        }
        else if (surface.Type is SurfaceType.Android && surface.Handles.Length is not 1)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Android surface must have exactly one handle (native window).");
        }
        else if (surface.Type is SurfaceType.IOS && surface.Handles.Length is not 1)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "iOS surface must have exactly one handle (view).");
        }
        else if (surface.Type is SurfaceType.MacOS && surface.Handles.Length is not 1)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "MacOS surface must have exactly one handle (view).");
        }

        if (surface.Handles.Any(static item => item is 0))
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Surface handles cannot contain zero values.");
        }

        if (surface.Width is 0 || surface.Height is 0)
        {
            context.PublishDebugCallback(MessageCategory.System, MessageSeverity.Error, "Surface width and height must be greater than zero.");
        }
    }
}
