namespace Zenith.NET;

public struct Surface
{
    public SurfaceType Type { get; set; }

    public nint[] Handles { get; set; }

    public uint Width { get; set; }

    public uint Height { get; set; }

    public static Surface Win32(nint hwnd, uint width, uint height)
    {
        return new()
        {
            Type = SurfaceType.Win32,
            Handles = [hwnd],
            Width = width,
            Height = height
        };
    }

    public static Surface Wayland(nint display, nint surface, uint width, uint height)
    {
        return new()
        {
            Type = SurfaceType.Wayland,
            Handles = [display, surface],
            Width = width,
            Height = height
        };
    }

    public static Surface Xlib(nint display, nint window, uint width, uint height)
    {
        return new()
        {
            Type = SurfaceType.Xlib,
            Handles = [display, window],
            Width = width,
            Height = height
        };
    }

    public static Surface Android(nint nativeWindow, uint width, uint height)
    {
        return new()
        {
            Type = SurfaceType.Android,
            Handles = [nativeWindow],
            Width = width,
            Height = height
        };
    }

    public static Surface IOS(nint view, uint width, uint height)
    {
        return new()
        {
            Type = SurfaceType.IOS,
            Handles = [view],
            Width = width,
            Height = height
        };
    }

    public static Surface MacOS(nint view, uint width, uint height)
    {
        return new()
        {
            Type = SurfaceType.MacOS,
            Handles = [view],
            Width = width,
            Height = height
        };
    }
}
