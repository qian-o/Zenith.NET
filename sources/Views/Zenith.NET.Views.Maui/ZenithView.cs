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
    private static readonly PropertyMapper<ZenithView, ZenithViewHandler> mapper = new(ViewMapper);

    private static readonly CommandMapper<ZenithView, ZenithViewHandler> commandMapper = new(ViewCommandMapper);

    protected override MauiZenithView CreatePlatformView()
    {
        return new(this);
    }
}

public partial class ZenithView : View, IZenithView
{
    public static Output Output { get; } = new()
    {
        ColorAttachments = [PixelFormat.B8G8R8A8UNorm],
        DepthStencilAttachment = PixelFormat.D24UNormS8UInt,
        SampleCount = SampleCount.Count1
    };

    public GraphicsContext? GraphicsContext { get; set; }

    public event EventHandler<UpdateEventArgs>? UpdateRequested;

    public event EventHandler<RenderEventArgs>? RenderRequested;
}
