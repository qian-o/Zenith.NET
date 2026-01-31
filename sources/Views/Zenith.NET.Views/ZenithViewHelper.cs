namespace Zenith.NET.Views;

public static class ZenithViewHelper
{
    public static PixelFormat ColorFormat { get; } = OperatingSystem.IsAndroid() ? PixelFormat.R8G8B8A8UNorm : PixelFormat.B8G8R8A8UNorm;

    public static PixelFormat DepthStencilFormat { get; } = PixelFormat.D32FloatS8UInt;

    public static Output Output { get; } = new()
    {
        ColorAttachments = [ColorFormat],
        DepthStencilAttachment = DepthStencilFormat,
        SampleCount = SampleCount.Count1
    };
}
