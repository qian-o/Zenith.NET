using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Zenith.NET.Views.WPF;

public class ZenithView : Control, IZenithView
{
    public static readonly DependencyProperty GraphicsContextProperty = DependencyProperty.Register(nameof(GraphicsContext),
                                                                                                    typeof(GraphicsContext),
                                                                                                    typeof(ZenithView),
                                                                                                    new(null));

    private readonly D3DImage image;
    private readonly FrameScheduler scheduler;

    private Surface? surface;

    public ZenithView()
    {
        image = new();
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

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (image.IsFrontBufferAvailable)
        {
            drawingContext.DrawImage(image, new(0, 0, ActualWidth, ActualHeight));
        }

        if (DesignerProperties.GetIsInDesignMode(this))
        {
            LinearGradientBrush brush = new()
            {
                StartPoint = new(0.0, 0.0),
                EndPoint = new(1.0, 1.0),
                GradientStops = [new(Color.FromRgb(0x51, 0x2B, 0xD4), 0.0), new(Color.FromRgb(0x8A, 0x58, 0xFF), 0.45), new(Color.FromRgb(0x00, 0xA4, 0xEF), 1.0)],
                SpreadMethod = GradientSpreadMethod.Reflect,
                RelativeTransform = new TranslateTransform(scheduler.TotalSeconds * 0.06 % 1.0, scheduler.TotalSeconds * 0.06 % 1.0)
            };

            drawingContext.DrawRectangle(brush, null, new(0, 0, ActualWidth, ActualHeight));

            Typeface typeface = FontFamily.GetTypefaces().FirstOrDefault() ?? new(FontFamily, FontStyle, FontWeight, FontStretch);
            double fontSize = Math.Clamp(ActualHeight / 15.0, 14.0, 48.0);
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            FormattedText shadowText = new("ZenithView",
                                           CultureInfo.CurrentCulture,
                                           FlowDirection.LeftToRight,
                                           typeface,
                                           fontSize,
                                           new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)),
                                           dpi);

            FormattedText mainText = new("ZenithView",
                                         CultureInfo.CurrentCulture,
                                         FlowDirection.LeftToRight,
                                         typeface,
                                         fontSize,
                                         new SolidColorBrush(Colors.White) { Opacity = 0.98 },
                                         dpi);

            double x = (ActualWidth - mainText.Width) / 2.0;
            double y = (ActualHeight - mainText.Height) / 2.0;

            drawingContext.DrawText(shadowText, new(x + 1.0, y + 1.0));
            drawingContext.DrawText(mainText, new(x, y));
        }
    }

    void IZenithView.UI(Action action)
    {
        Dispatcher.Invoke(action);
    }

    void IZenithView.EnsureResources()
    {
        if (GraphicsContext is null)
        {
            return;
        }

        uint width = Math.Clamp((uint)Math.Ceiling(ActualWidth), 1, uint.MaxValue);
        uint height = Math.Clamp((uint)Math.Ceiling(ActualHeight), 1, uint.MaxValue);

        if (surface is null || surface.Width != width || surface.Height != height)
        {
            ((IZenithView)this).ReleaseResources();

            surface = new(GraphicsContext, width, height);
        }
    }

    void IZenithView.Tick()
    {
        if (GraphicsContext is null || surface is null)
        {
            return;
        }

        surface.AcquireSync();

        CommandBuffer commandBuffer = GraphicsContext.GraphicsQueue.CommandBuffer();

        commandBuffer.Transition(surface.Drawable, default, TextureLayout.Undefined, TextureLayout.ColorAttachment);

        UpdateRequested?.Invoke(this, new(scheduler.UpdateSeconds, scheduler.TotalSeconds));
        RenderRequested?.Invoke(this, new(scheduler.RenderSeconds, scheduler.TotalSeconds, commandBuffer, surface.Drawable));

        commandBuffer.Transition(surface.Drawable, default, TextureLayout.ColorAttachment, TextureLayout.Common);

        commandBuffer.Submit().Wait();

        surface.ReleaseSync();
    }

    void IZenithView.Present()
    {
        surface?.Present(image);

        InvalidateVisual();
    }

    void IZenithView.ReleaseResources()
    {
        surface?.Dispose();
        surface = null;
    }
}
