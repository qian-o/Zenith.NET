using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Zenith.NET.Views.WinUI;

public partial class ZenithView : SwapChainPanel, IZenithView
{
    public static readonly DependencyProperty GraphicsContextProperty = DependencyProperty.Register(nameof(GraphicsContext),
                                                                                                    typeof(GraphicsContext),
                                                                                                    typeof(ZenithView),
                                                                                                    new(null, (d, _) => ((ZenithView)d).Destroy()));

    private readonly FrameDispatcher dispatcher;

    public ZenithView()
    {
        dispatcher = new(this);

        Loaded += (_, _) => dispatcher.Start();

        Unloaded += (_, _) =>
        {
            dispatcher.Stop();

            Destroy();
        };
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
        bool wasCalled = false;

        DispatcherQueue.TryEnqueue(() =>
        {
            action();

            wasCalled = true;
        });

        while (!wasCalled)
        {
            Thread.Yield();
        }
    }
}
