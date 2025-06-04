namespace Zenith.NET;

public record struct SurfaceDesc : IDesc
{
    public SurfaceDesc()
    {
        SurfaceType = SurfaceType.Win32;
        Handles = [];
        Width = 0;
        Height = 0;
    }

    public SurfaceType SurfaceType { get; set; }

    public nint[] Handles { get; set; }

    public uint Width { get; set; }

    public uint Height { get; set; }

    public readonly bool Validate()
    {
        if (Handles is null)
        {
            return false;
        }

        if (SurfaceType is SurfaceType.Win32 && Handles.Length is not 1)
        {
            return false;
        }

        if (SurfaceType is SurfaceType.Wayland && Handles.Length is not 2)
        {
            return false;
        }

        if (SurfaceType is SurfaceType.Xlib && Handles.Length is not 2)
        {
            return false;
        }

        if (SurfaceType is SurfaceType.Android && Handles.Length is not 1)
        {
            return false;
        }

        if (SurfaceType is SurfaceType.IOS && Handles.Length is not 1)
        {
            return false;
        }

        if (SurfaceType is SurfaceType.MacOS && Handles.Length is not 1)
        {
            return false;
        }

        if (Width is 0 || Height is 0)
        {
            return false;
        }

        return true;
    }

    public static SurfaceDesc CreateWin32(nint hwnd, uint width, uint height)
    {
        return new()
        {
            SurfaceType = SurfaceType.Win32,
            Handles = [hwnd],
            Width = width,
            Height = height
        };
    }

    public static SurfaceDesc CreateWayland(nint display, nint surface, uint width, uint height)
    {
        return new()
        {
            SurfaceType = SurfaceType.Wayland,
            Handles = [display, surface],
            Width = width,
            Height = height
        };
    }

    public static SurfaceDesc CreateXlib(nint display, nint window, uint width, uint height)
    {
        return new()
        {
            SurfaceType = SurfaceType.Xlib,
            Handles = [display, window],
            Width = width,
            Height = height
        };
    }

    public static SurfaceDesc CreateAndroid(nint nativeWindow, uint width, uint height)
    {
        return new()
        {
            SurfaceType = SurfaceType.Android,
            Handles = [nativeWindow],
            Width = width,
            Height = height
        };
    }

    public static SurfaceDesc CreateIOS(nint view, uint width, uint height)
    {
        return new()
        {
            SurfaceType = SurfaceType.IOS,
            Handles = [view],
            Width = width,
            Height = height
        };
    }

    public static SurfaceDesc CreateMacOS(nint view, uint width, uint height)
    {
        return new()
        {
            SurfaceType = SurfaceType.MacOS,
            Handles = [view],
            Width = width,
            Height = height
        };
    }
}
