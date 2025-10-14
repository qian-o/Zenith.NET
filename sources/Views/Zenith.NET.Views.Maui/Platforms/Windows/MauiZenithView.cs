using Microsoft.UI.Xaml.Controls;

namespace Zenith.NET.Views;

internal partial class MauiZenithView(ZenithViewHandler handler) : SwapChainPanel
{
    public ZenithView ZenithView => handler.VirtualView;
}
