namespace Zenith.NET;

/// <summary>
/// Specifies the type of native surface for rendering or presentation.
/// </summary>
public enum SurfaceType
{
    /// <summary>
    /// Win32 window surface.
    /// </summary>
    Win32,

    /// <summary>
    /// Wayland surface.
    /// </summary>
    Wayland,

    /// <summary>
    /// Xlib window surface.
    /// </summary>
    Xlib,

    /// <summary>
    /// Android native window surface.
    /// </summary>
    Android,

    /// <summary>
    /// iOS native view surface.
    /// </summary>
    IOS,

    /// <summary>
    /// macOS native view surface.
    /// </summary>
    MacOS
}
