namespace Zenith.NET.Helpers;

internal static class Validation
{
    public static bool IsNullOrEmpty<T>(T[]? array)
    {
        return array is null || array.Length is 0;
    }

    public static bool IsValidDescs<T>(T[]? descs, bool emptyAllowed = false) where T : IDesc
    {
        if (descs is null)
        {
            return false;
        }

        if (descs.Length is 0)
        {
            return emptyAllowed;
        }

        return descs.All(static desc => desc.Validate());
    }

    public static bool IsValidDepthStencilFormat(PixelFormat? format)
    {
        return format is null or PixelFormat.D24UNormS8UInt or PixelFormat.D32FloatS8UInt;
    }
}
