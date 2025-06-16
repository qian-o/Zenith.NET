namespace Zenith.NET;

public record struct SurfaceDesc
{
    public SurfaceType Type { get; set; }

    public nint[] Handles { get; set; }

    public uint Width { get; set; }

    public uint Height { get; set; }

    public static SurfaceDesc Win32(nint hwnd, uint width, uint height)
    {
        return new()
        {
            Type = SurfaceType.Win32,
            Handles = [hwnd],
            Width = width,
            Height = height
        };
    }

    public static SurfaceDesc Wayland(nint display, nint surface, uint width, uint height)
    {
        return new()
        {
            Type = SurfaceType.Wayland,
            Handles = [display, surface],
            Width = width,
            Height = height
        };
    }

    public static SurfaceDesc Xlib(nint display, nint window, uint width, uint height)
    {
        return new()
        {
            Type = SurfaceType.Xlib,
            Handles = [display, window],
            Width = width,
            Height = height
        };
    }

    public static SurfaceDesc Android(nint nativeWindow, uint width, uint height)
    {
        return new()
        {
            Type = SurfaceType.Android,
            Handles = [nativeWindow],
            Width = width,
            Height = height
        };
    }

    public static SurfaceDesc IOS(nint view, uint width, uint height)
    {
        return new()
        {
            Type = SurfaceType.IOS,
            Handles = [view],
            Width = width,
            Height = height
        };
    }

    public static SurfaceDesc MacOS(nint view, uint width, uint height)
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
