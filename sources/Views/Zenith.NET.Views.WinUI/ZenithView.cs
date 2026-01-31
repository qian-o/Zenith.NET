using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Zenith.NET.Views.WinUI;

public partial class ZenithView : SwapChainPanel, IZenithView
{
    public static readonly DependencyProperty GraphicsContextProperty = DependencyProperty.Register(nameof(GraphicsContext),
                                                                                                    typeof(GraphicsContext),
                                                                                                    typeof(ZenithView),
                                                                                                    new(null));

    private readonly FrameScheduler scheduler;

    public ZenithView()
    {
        scheduler = new(this);

        Loaded += async (_, _) => await scheduler.StartAsync();
        Unloaded += async (_, _) => await scheduler.StopAsync();
    }

    public GraphicsContext? GraphicsContext
    {
        get => (GraphicsContext?)GetValue(GraphicsContextProperty);
        set => SetValue(GraphicsContextProperty, value);
    }

    public event EventHandler<UpdateEventArgs>? UpdateRequested;

    public event EventHandler<RenderEventArgs>? RenderRequested;

    void IZenithView.UI(Action action)
    {
        DispatcherQueue.TryEnqueue(action.Invoke);
    }
}
