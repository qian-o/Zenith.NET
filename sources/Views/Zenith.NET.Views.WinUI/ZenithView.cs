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

        Loaded += async (_, _) => await dispatcher.StartAsync();

        Unloaded += async (_, _) =>
        {
            await dispatcher.StopAsync();

            Destroy();
        };
    }

    public static Output Output { get; } = new()
    {
        ColorAttachments = [PixelFormat.B8G8R8A8UNorm],
        DepthStencilAttachment = PixelFormat.D24UNormS8UInt,
        SampleCount = SampleCount.Count1
    };

    public GraphicsContext? GraphicsContext
    {
        get => (GraphicsContext?)GetValue(GraphicsContextProperty);
        set => SetValue(GraphicsContextProperty, value);
    }

    public event EventHandler<UpdateEventArgs>? UpdateRequested;

    public event EventHandler<RenderEventArgs>? RenderRequested;

    void IZenithView.UI(Action action)
    {
        using ManualResetEventSlim signal = new(false);

        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                action();
            }
            finally
            {
                signal.Set();
            }
        });

        signal.Wait();
    }
}
