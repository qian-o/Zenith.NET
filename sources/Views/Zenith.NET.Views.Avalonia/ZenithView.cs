using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;

namespace Zenith.NET.Views.Avalonia;

public class ZenithView : TemplatedControl, IZenithView
{
    public static readonly StyledProperty<GraphicsContext?> GraphicsContextProperty = AvaloniaProperty.Register<ZenithView, GraphicsContext?>(nameof(GraphicsContext));

    private readonly FrameDispatcher dispatcher;

    private Surface? surface;

    static ZenithView()
    {
        GraphicsContextProperty.Changed.AddClassHandler<ZenithView>((view, _) => view.Destroy());
    }

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
        ColorAttachments = [PixelFormat.R8G8B8A8UNorm],
        DepthStencilAttachment = PixelFormat.D24UNormS8UInt,
        SampleCount = SampleCount.Count1
    };

    public GraphicsContext? GraphicsContext
    {
        get => GetValue(GraphicsContextProperty);
        set => SetValue(GraphicsContextProperty, value);
    }

    public event EventHandler<UpdateEventArgs>? UpdateRequested;

    public event EventHandler<RenderEventArgs>? RenderRequested;

    public override void Render(DrawingContext context)
    {
        if (surface is not null)
        {
            context.DrawImage(surface.WriteableBitmap, new(0, 0, Bounds.Width, Bounds.Height));
        }

        if (Design.IsDesignMode)
        {
            LinearGradientBrush brush = new()
            {
                StartPoint = new(0.0, 0.0, RelativeUnit.Relative),
                EndPoint = new(1.0, 1.0, RelativeUnit.Relative),
                SpreadMethod = GradientSpreadMethod.Reflect,
                GradientStops = [new(Color.FromRgb(0x51, 0x2B, 0xD4), 0.0), new(Color.FromRgb(0x8A, 0x58, 0xFF), 0.45), new(Color.FromRgb(0x00, 0xA4, 0xEF), 1.0)],
                Transform = new TranslateTransform(dispatcher.TotalSeconds * 0.06 % 1.0, dispatcher.TotalSeconds * 0.06 % 1.0)
            };

            context.DrawRectangle(brush, null, new(0.0, 0.0, Bounds.Width, Bounds.Height));

            Typeface typeface = new(FontFamily, FontStyle, FontWeight, FontStretch);
            double fontSize = Math.Clamp(Bounds.Height / 15.0, 14.0, 48.0);
            double dpi = VisualRoot?.RenderScaling ?? 1.0;

            FormattedText shadowText = new("ZenithView",
                                           CultureInfo.CurrentCulture,
                                           FlowDirection.LeftToRight,
                                           typeface,
                                           fontSize * dpi,
                                           new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)));

            FormattedText mainText = new("ZenithView",
                                         CultureInfo.CurrentCulture,
                                         FlowDirection.LeftToRight,
                                         typeface,
                                         fontSize * dpi,
                                         new SolidColorBrush(Colors.White) { Opacity = 0.98 });

            float x = (float)(Bounds.Width - mainText.Width) / 2;
            float y = (float)(Bounds.Height - mainText.Height) / 2;

            context.DrawText(shadowText, new(x + 1.0, y + 1.0));
            context.DrawText(mainText, new(x, y));
        }
    }

    void IZenithView.UI(Action action)
    {
        Dispatcher.UIThread.Invoke(action);
    }

    void IZenithView.EnsureResources()
    {
        if (GraphicsContext is null)
        {
            return;
        }

        uint width = Math.Clamp((uint)Math.Ceiling(Bounds.Width), 1, uint.MaxValue);
        uint height = Math.Clamp((uint)Math.Ceiling(Bounds.Height), 1, uint.MaxValue);

        if (surface is null || surface.Width != width || surface.Height != height)
        {
            Destroy();

            surface = new(GraphicsContext, width, height);
        }
    }

    void IZenithView.Frame()
    {
        if (surface is null)
        {
            return;
        }

        UpdateRequested?.Invoke(this, new(dispatcher.UpdateSeconds, dispatcher.TotalSeconds));
        RenderRequested?.Invoke(this, new(dispatcher.RenderSeconds, dispatcher.TotalSeconds, surface.FrameBuffer));
    }

    void IZenithView.Present()
    {
        surface?.Present();
    }

    private void Destroy()
    {
        surface?.Dispose();
        surface = null;
    }
}
