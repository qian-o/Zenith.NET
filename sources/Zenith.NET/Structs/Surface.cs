namespace Zenith.NET;

public record struct Surface
{
    public SurfaceType Type;

    public nint[] NativeHandles;

    public uint Width;

    public uint Height;

    public static Surface Win32(nint hwnd, uint width, uint height)
    {
        return new()
        {
            Type = SurfaceType.Win32,
            NativeHandles = [hwnd],
            Width = width,
            Height = height
        };
    }

    public static Surface Wayland(nint display, nint surface, uint width, uint height)
    {
        return new()
        {
            Type = SurfaceType.Wayland,
            NativeHandles = [display, surface],
            Width = width,
            Height = height
        };
    }

    public static Surface Xlib(nint display, nint window, uint width, uint height)
    {
        return new()
        {
            Type = SurfaceType.Xlib,
            NativeHandles = [display, window],
            Width = width,
            Height = height
        };
    }

    public static Surface Android(nint nativeWindow, uint width, uint height)
    {
        return new()
        {
            Type = SurfaceType.Android,
            NativeHandles = [nativeWindow],
            Width = width,
            Height = height
        };
    }

    public static Surface Apple(nint layer, uint width, uint height)
    {
        return new()
        {
            Type = SurfaceType.Apple,
            NativeHandles = [layer],
            Width = width,
            Height = height
        };
    }

    public static Surface D3D11Interop(nint sharedHandle, uint width, uint height)
    {
        return new()
        {
            Type = SurfaceType.D3D11Interop,
            NativeHandles = [sharedHandle],
            Width = width,
            Height = height
        };
    }
}
