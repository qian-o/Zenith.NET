namespace Zenith.NET.Helpers;

/// <summary>
/// Provides helper methods for validating resource formats and configurations.
/// </summary>
internal static class Validation
{
    /// <summary>
    /// Determines whether the specified pixel format is a valid depth-stencil format.
    /// </summary>
    /// <param name="format">The pixel format to validate.</param>
    /// <returns>
    /// <c>true</c> if the format is <c>null</c>, <see cref="PixelFormat.D24UNormS8UInt"/>, or <see cref="PixelFormat.D32FloatS8UInt"/>;
    /// otherwise, <c>false</c>.
    /// </returns>
    public static bool IsValidDepthStencilFormat(PixelFormat? format)
    {
        return format is null or PixelFormat.D24UNormS8UInt or PixelFormat.D32FloatS8UInt;
    }
}
