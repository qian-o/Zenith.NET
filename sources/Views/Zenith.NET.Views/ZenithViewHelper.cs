namespace Zenith.NET.Views;

public static class ZenithViewHelper
{
    public static PixelFormat DrawableFormat { get; } = OperatingSystem.IsAndroid() ? PixelFormat.R8G8B8A8UNorm : PixelFormat.B8G8R8A8UNorm;
}
