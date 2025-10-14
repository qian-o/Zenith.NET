using MetalKit;

namespace Zenith.NET.Views;

internal class MauiZenithView(ZenithViewHandler handler) : MTKView
{
    public ZenithView ZenithView => handler.VirtualView;
}
