namespace Zenith.NET;

public record struct SurfaceDesc : IDesc
{
    public SurfaceDesc()
    {
        Type = SurfaceType.Win32;
        Handles = [];
        Width = 0;
        Height = 0;
    }

    public SurfaceType Type { get; set; }

    public nint[] Handles { get; set; }

    public uint Width { get; set; }

    public uint Height { get; set; }

    public readonly bool Validate()
    {
        if (!Enum.IsDefined(Type))
        {
            return false;
        }

        if (Handles is null)
        {
            return false;
        }

        if (Type is SurfaceType.Win32 && Handles.Length is not 1)
        {
            return false;
        }

        if (Type is SurfaceType.Wayland && Handles.Length is not 2)
        {
            return false;
        }

        if (Type is SurfaceType.Xlib && Handles.Length is not 2)
        {
            return false;
        }

        if (Type is SurfaceType.Android && Handles.Length is not 1)
        {
            return false;
        }

        if (Type is SurfaceType.IOS && Handles.Length is not 1)
        {
            return false;
        }

        if (Type is SurfaceType.MacOS && Handles.Length is not 1)
        {
            return false;
        }

        if (Handles.Any(static item => item is 0))
        {
            return false;
        }

        if (Width is 0 || Height is 0)
        {
            return false;
        }

        return true;
    }

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
