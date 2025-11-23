using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace Zenith.NET.Views.WPF;

public class ZenithView : Control
{
    public static readonly DependencyProperty GraphicsContextProperty = DependencyProperty.Register(nameof(GraphicsContext),
                                                                                                    typeof(GraphicsContext),
                                                                                                    typeof(ZenithView),
                                                                                                    new(null, (d, _) => ((ZenithView)d).Destroy()));

    private readonly D3DImage image = new();
    private readonly Stopwatch updateStopwatch = new();
    private readonly Stopwatch renderStopwatch = new();
    private readonly Stopwatch lifetimeStopwatch = new();

    private TimeSpan lastRender;
    private D3DTexture? texture;
    private SwapChain? swapChain;

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

        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue)
            {
                CompositionTarget.Rendering += OnRendering;
            }
            else
            {
                CompositionTarget.Rendering -= OnRendering;
            }
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

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (DesignerProperties.GetIsInDesignMode(this) || GraphicsContext is null)
        {
            LinearGradientBrush brush = new([new(Color.FromRgb(0x51, 0x2B, 0xD4), 0.0), new(Color.FromRgb(0x8A, 0x58, 0xFF), 0.45), new(Color.FromRgb(0x00, 0xA4, 0xEF), 1.0)], 45.0)
            {
                SpreadMethod = GradientSpreadMethod.Reflect,
                RelativeTransform = new TranslateTransform(lifetimeStopwatch.Elapsed.TotalSeconds * 0.06 % 1.0, lifetimeStopwatch.Elapsed.TotalSeconds * 0.06 % 1.0)
            };

            drawingContext.DrawRectangle(brush, null, new(0, 0, ActualWidth, ActualHeight));

            string text = DesignerProperties.GetIsInDesignMode(this) ? "ZenithView (Design Mode)" : "ZenithView (No GraphicsContext)";
            Typeface typeface = FontFamily.GetTypefaces().FirstOrDefault() ?? new(FontFamily, FontStyle, FontWeight, FontStretch);
            double fontSize = Math.Clamp(ActualHeight / 15.0, 14.0, 48.0);
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            FormattedText shadowText = new(text,
                                           CultureInfo.CurrentCulture,
                                           FlowDirection.LeftToRight,
                                           typeface,
                                           fontSize,
                                           new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)),
                                           dpi);

            FormattedText mainText = new(text,
                                         CultureInfo.CurrentCulture,
                                         FlowDirection.LeftToRight,
                                         typeface,
                                         fontSize,
                                         new SolidColorBrush(Colors.White) { Opacity = 0.98 },
                                         dpi);

            float x = (float)(ActualWidth - mainText.Width) / 2;
            float y = (float)(ActualHeight - mainText.Height) / 2;

            drawingContext.DrawText(shadowText, new(x + 1.0, y + 1.0));
            drawingContext.DrawText(mainText, new(x, y));
        }
        else
        {
            uint width = Math.Clamp((uint)Math.Ceiling(ActualWidth), 1, uint.MaxValue);
            uint height = Math.Clamp((uint)Math.Ceiling(ActualHeight), 1, uint.MaxValue);

            if (texture is null || texture.Width != width || texture.Height != height || swapChain is null)
            {
                Destroy();

                texture = new(width, height);

                swapChain = GraphicsContext.CreateSwapChain(new()
                {
                    Surface = Surface.D3D11Interop(texture.SharedHandle, width, height),
                    ColorTargetFormat = PixelFormat.B8G8R8A8UNorm,
                    DepthStencilTargetFormat = PixelFormat.D24UNormS8UInt
                });

                image.Lock();
                image.SetBackBuffer(D3DResourceType.IDirect3DSurface9, texture.Handle);
                image.Unlock();
            }

            UpdateRequested?.Invoke(this, new(updateStopwatch.Elapsed.TotalSeconds, lifetimeStopwatch.Elapsed.TotalSeconds));
            updateStopwatch.Restart();

            RenderRequested?.Invoke(this, new(renderStopwatch.Elapsed.TotalSeconds, lifetimeStopwatch.Elapsed.TotalSeconds, swapChain.FrameBuffer));
            renderStopwatch.Restart();

            swapChain.Present();

            image.Lock();
            image.AddDirtyRect(new(0, 0, (int)width, (int)height));
            image.Unlock();

            drawingContext.DrawImage(image, new(0, 0, ActualWidth, ActualHeight));
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        RenderingEventArgs args = (RenderingEventArgs)e;

        if (lastRender != args.RenderingTime)
        {
            InvalidateVisual();

            lastRender = args.RenderingTime;
        }
    }

    private void Destroy()
    {
        swapChain?.Dispose();
        swapChain = null;

        texture?.Dispose();
        texture = null;
    }
}
