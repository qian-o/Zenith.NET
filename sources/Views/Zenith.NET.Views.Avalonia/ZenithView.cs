using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using AvaloniaPixelFormat = Avalonia.Platform.PixelFormat;

namespace Zenith.NET.Views.Avalonia;

public unsafe class ZenithView : TemplatedControl
{
    public static readonly StyledProperty<GraphicsContext?> GraphicsContextProperty = AvaloniaProperty.Register<ZenithView, GraphicsContext?>(nameof(GraphicsContext));

    private readonly Stopwatch updateStopwatch = new();
    private readonly Stopwatch renderStopwatch = new();
    private readonly Stopwatch lifetimeStopwatch = new();

    private Texture? color;
    private Texture? depthStencil;
    private FrameBuffer? frameBuffer;
    private Texture? present;
    private WriteableBitmap? bitmap;

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

            DestroyFrameBuffer();

            updateStopwatch.Reset();
            renderStopwatch.Reset();
            lifetimeStopwatch.Reset();

            bitmap?.Dispose();
            bitmap = null;
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
        set
        {
            if (GraphicsContext != value)
            {
                DestroyFrameBuffer();

                SetValue(GraphicsContextProperty, value);
            }
        }
    }

    public event EventHandler<UpdateEventArgs>? UpdateRequested;

    public event EventHandler<RenderEventArgs>? RenderRequested;

    public override void Render(DrawingContext context)
    {
        if (Design.IsDesignMode || GraphicsContext is null)
        {
            LinearGradientBrush brush = new()
            {
                StartPoint = new(0, 0, RelativeUnit.Relative),
                EndPoint = new(1, 1, RelativeUnit.Relative),
                SpreadMethod = GradientSpreadMethod.Reflect,
                GradientStops = [new(Color.FromRgb(0x51, 0x2B, 0xD4), 0.0), new(Color.FromRgb(0x8A, 0x58, 0xFF), 0.45), new(Color.FromRgb(0x00, 0xA4, 0xEF), 1.0)],
                Transform = new TranslateTransform(lifetimeStopwatch.Elapsed.TotalSeconds * 0.06 % 1.0, lifetimeStopwatch.Elapsed.TotalSeconds * 0.06 % 1.0)
            };

            context.DrawRectangle(brush, null, new(0, 0, Bounds.Width, Bounds.Height));

            string text = Design.IsDesignMode ? "ZenithView (Design Mode)" : "ZenithView (No GraphicsContext)";
            Typeface typeface = new(FontFamily, FontStyle, FontWeight, FontStretch);
            double fontSize = Math.Clamp(Bounds.Height / 15.0, 14.0, 48.0);
            double dpi = VisualRoot?.RenderScaling ?? 1.0;

            FormattedText shadowText = new(text,
                                           CultureInfo.CurrentCulture,
                                           FlowDirection.LeftToRight,
                                           typeface,
                                           fontSize * dpi,
                                           new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)));

            FormattedText mainText = new(text,
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
        else
        {
            uint width = Math.Clamp((uint)Math.Ceiling(Bounds.Width), 1, uint.MaxValue);
            uint height = Math.Clamp((uint)Math.Ceiling(Bounds.Height), 1, uint.MaxValue);

            if (color is null || depthStencil is null || frameBuffer is null || frameBuffer.Width != width || frameBuffer.Height != height || present is null || bitmap is null)
            {
                DestroyFrameBuffer();

                color = GraphicsContext.CreateTexture(new()
                {
                    Type = TextureType.Texture2D,
                    Format = PixelFormat.R8G8B8A8UNorm,
                    Width = width,
                    Height = height,
                    Depth = 1,
                    MipLevels = 1,
                    ArrayLayers = 1,
                    SampleCount = SampleCount.Count1,
                    Flags = TextureUsageFlags.RenderTarget
                });

                depthStencil = GraphicsContext.CreateTexture(new()
                {
                    Type = TextureType.Texture2D,
                    Format = PixelFormat.D24UNormS8UInt,
                    Width = width,
                    Height = height,
                    Depth = 1,
                    MipLevels = 1,
                    ArrayLayers = 1,
                    SampleCount = SampleCount.Count1,
                    Flags = TextureUsageFlags.DepthStencil
                });

                frameBuffer = GraphicsContext.CreateFrameBuffer(new()
                {
                    ColorAttachments = [new() { Target = color }],
                    DepthStencilAttachment = new() { Target = depthStencil }
                });

                present = GraphicsContext.CreateTexture(new()
                {
                    Type = TextureType.Texture2D,
                    Format = PixelFormat.R8G8B8A8UNorm,
                    Width = width,
                    Height = height,
                    Depth = 1,
                    MipLevels = 1,
                    ArrayLayers = 1,
                    SampleCount = SampleCount.Count1,
                    Flags = TextureUsageFlags.Dynamic
                });

                bitmap?.Dispose();
                bitmap = new(new((int)width, (int)height), new(96, 96), AvaloniaPixelFormat.Rgba8888, AlphaFormat.Premul);
            }

            UpdateRequested?.Invoke(this, new(updateStopwatch.Elapsed.TotalSeconds, lifetimeStopwatch.Elapsed.TotalSeconds));
            updateStopwatch.Restart();

            RenderRequested?.Invoke(this, new(renderStopwatch.Elapsed.TotalSeconds, lifetimeStopwatch.Elapsed.TotalSeconds, frameBuffer));
            renderStopwatch.Restart();

            CommandBuffer commandBuffer = GraphicsContext.Graphics.CommandBuffer();
            commandBuffer.CopyTexture(color, default, default, present, default, default, new() { Width = width, Height = height, Depth = 1 });
            commandBuffer.Submit();

            GraphicsContext.Graphics.WaitIdle();

            using (ILockedFramebuffer lockedFramebuffer = bitmap.Lock())
            {
                MappedMemory mappedMemory = present.Map(default);

                if (mappedMemory.RowPitch == lockedFramebuffer.RowBytes)
                {
                    Unsafe.CopyBlock((void*)lockedFramebuffer.Address, (void*)mappedMemory.Pointer, mappedMemory.SizeInBytes);
                }
                else
                {
                    byte* srcPtr = (byte*)mappedMemory.Pointer;
                    byte* dstPtr = (byte*)lockedFramebuffer.Address;

                    for (uint y = 0; y < height; y++)
                    {
                        Unsafe.CopyBlock(dstPtr, srcPtr, (uint)lockedFramebuffer.RowBytes);

                        srcPtr += mappedMemory.RowPitch;
                        dstPtr += lockedFramebuffer.RowBytes;
                    }
                }

                present.Unmap();
            }

            context.DrawImage(bitmap, new(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), new(0, 0, Bounds.Width, Bounds.Height));
        }

        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
    }

    private void DestroyFrameBuffer()
    {
        present?.Dispose();
        present = null;

        frameBuffer?.Dispose();
        frameBuffer = null;

        color?.Dispose();
        color = null;

        depthStencil?.Dispose();
        depthStencil = null;
    }
}
