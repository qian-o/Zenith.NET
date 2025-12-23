using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Zenith.NET.Views.WinUI;

public partial class ZenithView : SwapChainPanel
{
    public static readonly DependencyProperty GraphicsContextProperty = DependencyProperty.Register(nameof(GraphicsContext),
                                                                                                    typeof(GraphicsContext),
                                                                                                    typeof(ZenithView),
                                                                                                    new(null, (d, _) => ((ZenithView)d).Destroy()));

    private readonly ViewTimer timer = new();

    public ZenithView()
    {
        Loaded += (_, _) =>
        {
            timer.Start();

            CompositionTarget.Rendering += OnRendering;
        };

        Unloaded += (_, _) =>
        {
            CompositionTarget.Rendering -= OnRendering;

            timer.Reset();

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

    private void OnRendering(object? sender, object e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (GraphicsContext is not null)
            {
                OnRender(GraphicsContext);
            }
        });
    }
}
