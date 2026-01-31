namespace Zenith.NET.Views;

public static class ZenithViewHelper
{
    public static PixelFormat ColorTargetFormat { get; } = OperatingSystem.IsAndroid() ? PixelFormat.R8G8B8A8UNorm : PixelFormat.B8G8R8A8UNorm;

    public static PixelFormat DepthStencilTargetFormat { get; } = PixelFormat.D32FloatS8UInt;

    public static Output Output { get; } = new()
    {
        ColorAttachments = [ColorTargetFormat],
        DepthStencilAttachment = DepthStencilTargetFormat,
        SampleCount = SampleCount.Count1
    };
}
