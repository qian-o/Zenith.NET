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
        [nameof(ZenithView.Background)] = MapBackground,
        [nameof(ZenithView.GraphicsContext)] = MapGraphicsContext
    };

    private static readonly CommandMapper<ZenithView, ZenithViewHandler> commandMapper = new(ViewCommandMapper);

    protected override MauiZenithView CreatePlatformView()
    {
        return new(this);
    }

    protected override void DisconnectHandler(MauiZenithView platformView)
    {
        platformView.Destroy();

        base.DisconnectHandler(platformView);
    }

    private static void MapBackground(ZenithViewHandler handler, ZenithView view)
    {
        // ZenithView does not support Background property.
    }

    private static void MapGraphicsContext(ZenithViewHandler handler, ZenithView view)
    {
        handler.PlatformView.Destroy();
    }
}

public partial class ZenithView : View
{
    public static readonly BindableProperty GraphicsContextProperty = BindableProperty.Create(nameof(GraphicsContext), typeof(GraphicsContext), typeof(ZenithView));

    public static Output Output => MauiZenithView.Output;

    public GraphicsContext? GraphicsContext
    {
        get => (GraphicsContext?)GetValue(GraphicsContextProperty);
        set => SetValue(GraphicsContextProperty, value);
    }

    public event EventHandler<UpdateEventArgs>? UpdateRequested;

    public event EventHandler<RenderEventArgs>? RenderRequested;

    internal void OnUpdateRequested(UpdateEventArgs e)
    {
        UpdateRequested?.Invoke(this, e);
    }

    internal void OnRenderRequested(RenderEventArgs e)
    {
        RenderRequested?.Invoke(this, e);
    }
}
