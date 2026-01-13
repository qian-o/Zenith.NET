using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Zenith.NET.Views.WinUI;

public partial class ZenithView : SwapChainPanel
{
    public static readonly DependencyProperty GraphicsContextProperty = DependencyProperty.Register(nameof(GraphicsContext),
                                                                                                    typeof(GraphicsContext),
                                                                                                    typeof(ZenithView),
                                                                                                    new(null, (d, _) => ((ZenithView)d).DestroyResources()));

    private readonly FrameDispatcher dispatcher;

    public ZenithView()
    {
        dispatcher = new(Frame, Present);

        Loaded += (_, _) => dispatcher.Start();

        Unloaded += (_, _) =>
        {
            dispatcher.Stop();

            DestroyResources();
        };
    }

    public GraphicsContext? GraphicsContext
    {
        get => (GraphicsContext?)GetValue(GraphicsContextProperty);
        set => SetValue(GraphicsContextProperty, value);
    }

    public event EventHandler<UpdateEventArgs>? UpdateRequested;

    public event EventHandler<RenderEventArgs>? RenderRequested;
}
