using UIKit;

namespace Zenith.NET.Views;

internal class MauiZenithView(ZenithViewHandler handler) : UIView
{
    public ZenithView ZenithView => handler.VirtualView;
}
