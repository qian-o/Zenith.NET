namespace Zenith.NET;

/// <summary>
/// Describes a native rendering surface, including its type, native handles, and dimensions.
/// </summary>
public record struct SurfaceDesc : IDesc
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurfaceDesc"/> struct with default values.
    /// </summary>
    public SurfaceDesc()
    {
        Type = SurfaceType.Win32;
        Handles = [];
        Width = 0;
        Height = 0;
    }

    /// <summary>
    /// The surface type.
    /// </summary>
    public SurfaceType Type { get; set; }

    /// <summary>
    /// Surface native handles.
    /// <para>
    /// <b>Handle requirements by <see cref="Type"/>:</b>
    /// <list type="table">
    /// <item>
    ///   <term>Win32</term>
    ///   <description>Single handle to the window.</description>
    /// </item>
    /// <item>
    ///   <term>Wayland</term>
    ///   <description>Two handles: display and surface.</description>
    /// </item>
    /// <item>
    ///   <term>Xlib</term>
    ///   <description>Two handles: display and window.</description>
    /// </item>
    /// <item>
    ///   <term>Android, IOS, MacOS</term>
    ///   <description>Single handle to the native window or view.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </summary>
    public nint[] Handles { get; set; }

    /// <summary>
    /// The width of the surface, in pixels.
    /// </summary>
    public uint Width { get; set; }

    /// <summary>
    /// The height of the surface, in pixels.
    /// </summary>
    public uint Height { get; set; }

    /// <summary>
    /// Validates the current <see cref="SurfaceDesc"/> instance.
    /// </summary>
    /// <returns><c>true</c> if valid; otherwise, <c>false</c>.</returns>
    public readonly bool Validate()
    {
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

    public static SurfaceDesc CreateWin32(nint hwnd, uint width, uint height)
    {
        return new()
        {
            Type = SurfaceType.Win32,
            Handles = [hwnd],
            Width = width,
            Height = height
        };
    }

    public static SurfaceDesc CreateWayland(nint display, nint surface, uint width, uint height)
    {
        return new()
        {
            Type = SurfaceType.Wayland,
            Handles = [display, surface],
            Width = width,
            Height = height
        };
    }

    public static SurfaceDesc CreateXlib(nint display, nint window, uint width, uint height)
    {
        return new()
        {
            Type = SurfaceType.Xlib,
            Handles = [display, window],
            Width = width,
            Height = height
        };
    }

    public static SurfaceDesc CreateAndroid(nint nativeWindow, uint width, uint height)
    {
        return new()
        {
            Type = SurfaceType.Android,
            Handles = [nativeWindow],
            Width = width,
            Height = height
        };
    }

    public static SurfaceDesc CreateIOS(nint view, uint width, uint height)
    {
        return new()
        {
            Type = SurfaceType.IOS,
            Handles = [view],
            Width = width,
            Height = height
        };
    }

    public static SurfaceDesc CreateMacOS(nint view, uint width, uint height)
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
