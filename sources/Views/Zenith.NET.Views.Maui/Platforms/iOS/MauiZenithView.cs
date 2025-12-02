using UIKit;

namespace Zenith.NET.Views.Maui.Platforms.iOS;

internal class MauiZenithView(ZenithViewHandler handler) : UIView
{
    public static Output Output { get; } = new()
    {
        ColorAttachments = [PixelFormat.R8G8B8A8UNorm],
        DepthStencilAttachment = PixelFormat.D24UNormS8UInt,
        SampleCount = SampleCount.Count1
    };

    public ZenithView ZenithView => handler.VirtualView;

    public void Destroy()
    {
    }
}
