namespace Zenith.NET.Helpers;

internal static class Validation
{
    public static bool IsValidDepthStencilFormat(PixelFormat? format)
    {
        return format is null or PixelFormat.D24UNormS8UInt or PixelFormat.D32FloatS8UInt;
    }
}
