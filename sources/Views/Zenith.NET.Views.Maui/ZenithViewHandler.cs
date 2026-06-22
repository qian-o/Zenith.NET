using Microsoft.Maui.Handlers;
#if ANDROID
using Zenith.NET.Views.Maui.Platforms.Android;
#elif IOS
using Zenith.NET.Views.Maui.Platforms.iOS;
#elif MACCATALYST
using Zenith.NET.Views.Maui.Platforms.MacCatalyst;
#elif WINDOWS
using Zenith.NET.Views.Maui.Platforms.Windows;
#endif

namespace Zenith.NET.Views.Maui;

internal class ZenithViewHandler() : ViewHandler<ZenithView, MauiZenithView>(mapper, commandMapper)
{
    private static readonly PropertyMapper<ZenithView, ZenithViewHandler> mapper = new(ViewMapper)
    {
        [nameof(ZenithView.Background)] = MapBackground
    };

    private static readonly CommandMapper<ZenithView, ZenithViewHandler> commandMapper = new(ViewCommandMapper)
    {
        [nameof(IZenithView.EnsureResources)] = MapEnsureResources,
        [nameof(IZenithView.Tick)] = MapTick,
        [nameof(IZenithView.Present)] = MapPresent,
        [nameof(IZenithView.ReleaseResources)] = MapReleaseResources
    };

    protected override MauiZenithView CreatePlatformView()
    {
        return new(this);
    }

    private static void MapBackground(ZenithViewHandler handler, ZenithView view)
    {
        // ZenithView does not support Background property.
    }

    private static void MapEnsureResources(ZenithViewHandler handler, ZenithView view, object? arg3)
    {
        handler.PlatformView.EnsureResources();
    }

    private static void MapTick(ZenithViewHandler handler, ZenithView view, object? arg3)
    {
        handler.PlatformView.Tick();
    }

    private static void MapPresent(ZenithViewHandler handler, ZenithView view, object? arg3)
    {
        handler.PlatformView.Present();
    }

    private static void MapReleaseResources(ZenithViewHandler handler, ZenithView view, object? arg3)
    {
        handler.PlatformView.ReleaseResources();
    }
}