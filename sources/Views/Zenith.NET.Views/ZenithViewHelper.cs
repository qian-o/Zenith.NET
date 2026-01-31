namespace Zenith.NET.Views;

public static class ZenithViewHelper
{
    static ZenithViewHelper()
    {
        if (OperatingSystem.IsAndroid())
        {
            ColorTargetFormat = PixelFormat.R8G8B8A8UNorm;
        }
        else
        {
            ColorTargetFormat = PixelFormat.B8G8R8A8UNorm;
        }

        DepthStencilTargetFormat = PixelFormat.D32FloatS8UInt;
    }

    public static PixelFormat ColorTargetFormat { get; }

    public static PixelFormat DepthStencilTargetFormat { get; }

    public static Output Output { get; } = new()
    {
        ColorAttachments = [ColorTargetFormat],
        DepthStencilAttachment = DepthStencilTargetFormat,
        SampleCount = SampleCount.Count1
    };
}
