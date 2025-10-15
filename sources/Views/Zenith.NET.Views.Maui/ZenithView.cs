using Microsoft.Maui.Handlers;

namespace Zenith.NET.Views;

internal class ZenithViewHandler() : ViewHandler<ZenithView, MauiZenithView>(mapper, commandMapper)
{
    private static readonly PropertyMapper<ZenithView, ZenithViewHandler> mapper = new(ViewMapper);

    private static readonly CommandMapper<ZenithView, ZenithViewHandler> commandMapper = new(ViewCommandMapper);

    protected override MauiZenithView CreatePlatformView()
    {
        return new(this);
    }
}

public partial class ZenithView : View;
