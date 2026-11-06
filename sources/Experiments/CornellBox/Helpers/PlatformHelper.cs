using Silk.NET.Windowing;
using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Metal;
using Zenith.NET.Vulkan;

namespace CornellBox.Helpers;

internal static class PlatformHelper
{
    public static GraphicsContext CreateContext()
    {
        if (OperatingSystem.IsWindows())
        {
            return GraphicsContext.CreateDirectX12(useValidationLayer: true);
        }

        if (OperatingSystem.IsMacOS())
        {
            return GraphicsContext.CreateMetal(useValidationLayer: true);
        }

        if (OperatingSystem.IsLinux())
        {
            return GraphicsContext.CreateVulkan(useValidationLayer: true);
        }

        throw new PlatformNotSupportedException("This application only supports Windows, macOS, and Linux.");
    }

    public static Surface CreateSurface(IWindow window)
    {
        uint width = (uint)window.FramebufferSize.X;
        uint height = (uint)window.FramebufferSize.Y;

        if (OperatingSystem.IsWindows())
        {
            return Surface.Win32(window.Native!.Win32!.Value.Hwnd, width, height);
        }

        if (OperatingSystem.IsMacOS())
        {
            return Surface.Apple(CocoaHelper.CreateLayer(window.Native!.Cocoa!.Value), width, height);
        }

        return Surface.Xlib(window.Native!.X11!.Value.Display, (nint)window.Native.X11.Value.Window, width, height);
    }
}
