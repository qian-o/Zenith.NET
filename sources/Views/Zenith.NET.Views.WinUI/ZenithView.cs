using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Zenith.NET.Views.WinUI;

public partial class ZenithView : SwapChainPanel
{
    public static readonly DependencyProperty GraphicsContextProperty = DependencyProperty.Register(nameof(GraphicsContext),
                                                                                                    typeof(GraphicsContext),
                                                                                                    typeof(ZenithView),
                                                                                                    new(null));

    private readonly Grid previewGrid;
    private readonly LinearGradientBrush previewBrush;
    private readonly TextBlock previewTextBlock;
    private readonly Stopwatch updateStopwatch = new();
    private readonly Stopwatch renderStopwatch = new();
    private readonly Stopwatch lifetimeStopwatch = new();

    private TimeSpan lastRender;

    public ZenithView()
    {
        Loaded += (_, _) =>
        {
            updateStopwatch.Start();
            renderStopwatch.Start();
            lifetimeStopwatch.Start();
        };

        Unloaded += (_, _) =>
        {
            updateStopwatch.Stop();
            renderStopwatch.Stop();
            lifetimeStopwatch.Stop();

            Destroy();

            updateStopwatch.Reset();
            renderStopwatch.Reset();
            lifetimeStopwatch.Reset();
        };

        EffectiveViewportChanged += (_, e) =>
        {
            CompositionTarget.Rendering -= OnRendering;

            if (e.EffectiveViewport.Width is not 0 && e.EffectiveViewport.Height is not 0)
            {
                CompositionTarget.Rendering += OnRendering;
            }
        };

        previewGrid = new Grid()
        {
            Background = previewBrush = new LinearGradientBrush()
            {
                GradientStops =
                [
                    new() { Color = Color.FromArgb(0xFF, 0x51, 0x2B, 0xD4), Offset = 0.0 },
                    new() { Color = Color.FromArgb(0xFF, 0x8A, 0x58, 0xFF), Offset = 0.45 },
                    new() { Color = Color.FromArgb(0xFF, 0x00, 0xA4, 0xEF), Offset = 1.0 }
                ],
                StartPoint = new(0, 0),
                EndPoint = new(1, 1),
                SpreadMethod = GradientSpreadMethod.Reflect
            },
            IsHitTestVisible = false
        };
        previewGrid.Children.Add(previewTextBlock = new()
        {
            Text = "ZenithView (No GraphicsContext)",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });

        Children.Add(previewGrid);
    }

    public GraphicsContext? GraphicsContext
    {
        get => (GraphicsContext?)GetValue(GraphicsContextProperty);
        set
        {
            if (GraphicsContext != value)
            {
                Destroy();

                SetValue(GraphicsContextProperty, value);
            }
        }
    }

    public event EventHandler<UpdateEventArgs>? UpdateRequested;

    public event EventHandler<RenderEventArgs>? RenderRequested;

    private void OnRendering(object? sender, object e)
    {
        RenderingEventArgs args = (RenderingEventArgs)e;

        if (lastRender != args.RenderingTime)
        {
            if (GraphicsContext is null)
            {
                previewGrid.Visibility = Visibility.Visible;

                previewBrush.RelativeTransform = new TranslateTransform()
                {
                    X = lifetimeStopwatch.Elapsed.TotalSeconds * 0.06 % 1.0,
                    Y = lifetimeStopwatch.Elapsed.TotalSeconds * 0.06 % 1.0
                };

                previewTextBlock.FontSize = Math.Clamp(ActualHeight / 15.0, 14.0, 48.0);
            }
            else
            {
                previewGrid.Visibility = Visibility.Collapsed;

                OnRender(GraphicsContext);
            }

            lastRender = args.RenderingTime;
        }
    }
}
