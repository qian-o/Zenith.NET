using UIKit;

namespace Zenith.NET.Views.Maui.Platforms.MacCatalyst;

internal class MauiZenithView(ZenithViewHandler handler) : UIView
{
    public ZenithView ZenithView => handler.VirtualView;

    public void Destroy()
    {
    }
}
