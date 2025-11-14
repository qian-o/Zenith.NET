using Microsoft.UI.Xaml.Controls;

namespace Zenith.NET.Views.Maui.Platforms.Windows;

internal partial class MauiZenithView(ZenithViewHandler handler) : Control
{
    public ZenithView ZenithView => handler.VirtualView;
}
