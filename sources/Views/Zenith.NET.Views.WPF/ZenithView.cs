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
                                                                                                    new PropertyMetadata(null, (d, _) => ((ZenithView)d).DestroySwapChain()));

    private readonly D3DImage image = new();

    private D3DTexture? texture;
    private SwapChain? swapChain;

    public GraphicsContext? GraphicsContext
    {
        get => (GraphicsContext?)GetValue(GraphicsContextProperty);
        set => SetValue(GraphicsContextProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (GraphicsContext is null)
        {
            drawingContext.DrawRectangle(new LinearGradientBrush(
            [
                new(Color.FromRgb(0x51, 0x2B, 0xD4), 0.0),
                new(Color.FromRgb(0x8A, 0x58, 0xFF), 0.45),
                new(Color.FromRgb(0x00, 0xA4, 0xEF), 1.0)
            ], 45), null, new Rect(0, 0, ActualWidth, ActualHeight));

            Typeface typeface = FontFamily.GetTypefaces().FirstOrDefault() ?? new(FontFamily, FontStyle, FontWeight, FontStretch);
            double fontSize = Math.Clamp(ActualHeight / 15.0, 14.0, 48.0);
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            FormattedText shadowText = new("No GraphicsContext assigned.",
                                           CultureInfo.CurrentCulture,
                                           FlowDirection.LeftToRight,
                                           typeface,
                                           fontSize,
                                           new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)),
                                           dpi);

            FormattedText mainText = new("No GraphicsContext assigned.",
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
            uint width = (uint)Math.Ceiling(ActualWidth);
            uint height = (uint)Math.Ceiling(ActualHeight);

            if (width is 0 || height is 0)
            {
                return;
            }

            image.Lock();

            if (texture is null || texture.Width != width || texture.Height != height)
            {
                texture?.Dispose();
                texture = new(width, height);

                image.SetBackBuffer(D3DResourceType.IDirect3DSurface9, texture.Handle);

                DestroySwapChain();
            }

            swapChain ??= GraphicsContext.CreateSwapChain(new()
            {
                Surface = Surface.D3D11Interop(texture.SharedHandle, texture.Width, texture.Height),
                ColorTargetFormat = PixelFormat.B8G8R8A8UNorm,
                DepthStencilTargetFormat = PixelFormat.D24UNormS8UInt
            });

            swapChain.Present();

            image.AddDirtyRect(new Int32Rect(0, 0, (int)Math.Ceiling(ActualWidth), (int)Math.Ceiling(ActualHeight)));

            image.Unlock();

            drawingContext.DrawImage(image, new Rect(0, 0, ActualWidth, ActualHeight));
        }
    }

    private void DestroySwapChain()
    {
        swapChain?.Dispose();
        swapChain = null;
    }
}
