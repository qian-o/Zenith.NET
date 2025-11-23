using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Zenith.NET.Views.WinForms;

public class ZenithView : Control
{
    private readonly Stopwatch updateStopwatch = new();
    private readonly Stopwatch renderStopwatch = new();
    private readonly Stopwatch lifetimeStopwatch = new();

    private SwapChain? swapChain;

    public ZenithView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.Opaque | ControlStyles.UserPaint, true);

        HandleCreated += (_, _) =>
        {
            updateStopwatch.Start();
            renderStopwatch.Start();
            lifetimeStopwatch.Start();
        };

        HandleDestroyed += (_, _) =>
        {
            updateStopwatch.Stop();
            renderStopwatch.Stop();
            lifetimeStopwatch.Stop();

            Destroy();

            updateStopwatch.Reset();
            renderStopwatch.Reset();
            lifetimeStopwatch.Reset();
        };

        ClientSizeChanged += (_, _) =>
        {
            if (ClientSize.Width is 0 || ClientSize.Height is 0)
            {
                return;
            }

            swapChain?.Resize((uint)ClientSize.Width, (uint)ClientSize.Height);
        };
    }

    public static Output Output { get; } = new()
    {
        ColorAttachments = [PixelFormat.B8G8R8A8UNorm],
        DepthStencilAttachment = PixelFormat.D24UNormS8UInt,
        SampleCount = SampleCount.Count1
    };

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public GraphicsContext? GraphicsContext
    {
        get => field;
        set
        {
            if (field != value)
            {
                Destroy();

                field = value;
            }
        }
    }

    public event EventHandler<UpdateEventArgs>? UpdateRequested;

    public event EventHandler<RenderEventArgs>? RenderRequested;

    protected override void OnPaint(PaintEventArgs e)
    {
        if (ClientSize.Width is 0 || ClientSize.Height is 0)
        {
            return;
        }

        if (DesignMode || GraphicsContext is null)
        {
            DoubleBuffered = true;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            float t = (float)(lifetimeStopwatch.Elapsed.TotalSeconds * 0.06 % 1.0);

            using LinearGradientBrush brush = new(ClientRectangle, Color.Black, Color.Black, 45.0f, true)
            {
                InterpolationColors = new()
                {
                    Colors = [Color.FromArgb(0x51, 0x2B, 0xD4), Color.FromArgb(0x8A, 0x58, 0xFF), Color.FromArgb(0x00, 0xA4, 0xEF)],
                    Positions = [0.0f, 0.45f, 1.0f]
                },
                WrapMode = WrapMode.TileFlipXY
            };
            brush.TranslateTransform(t * ClientRectangle.Width, t * ClientRectangle.Height);

            e.Graphics.FillRectangle(brush, ClientRectangle);

            string text = DesignMode ? "ZenithView (Design Mode)" : "ZenithView (No GraphicsContext)";
            using Font font = new(Font.FontFamily, (float)Math.Clamp(ClientRectangle.Height / 15.0, 14.0, 48.0), Font.Style, GraphicsUnit.Pixel);
            using SolidBrush shadow = new(Color.FromArgb(0x66, 0x00, 0x00, 0x00));
            using SolidBrush white = new(Color.FromArgb((int)(0.98 * 255), 0xFF, 0xFF, 0xFF));

            SizeF size = e.Graphics.MeasureString(text, font, int.MaxValue);
            float x = (ClientRectangle.Width - size.Width) / 2.0f;
            float y = (ClientRectangle.Height - size.Height) / 2.0f;

            e.Graphics.DrawString(text, font, shadow, x + 1.0f, y + 1.0f);
            e.Graphics.DrawString(text, font, white, x, y);
        }
        else
        {
            DoubleBuffered = false;

            swapChain ??= GraphicsContext.CreateSwapChain(new()
            {
                Surface = Surface.Win32(Handle, (uint)ClientSize.Width, (uint)ClientSize.Height),
                ColorTargetFormat = PixelFormat.B8G8R8A8UNorm,
                DepthStencilTargetFormat = PixelFormat.D24UNormS8UInt
            });

            UpdateRequested?.Invoke(this, new(updateStopwatch.Elapsed.TotalSeconds, lifetimeStopwatch.Elapsed.TotalSeconds));
            updateStopwatch.Restart();

            RenderRequested?.Invoke(this, new(renderStopwatch.Elapsed.TotalSeconds, lifetimeStopwatch.Elapsed.TotalSeconds, swapChain.FrameBuffer));
            renderStopwatch.Restart();

            swapChain.Present();
        }

        BeginInvoke(Invalidate);
    }

    private void Destroy()
    {
        swapChain?.Dispose();
        swapChain = null;
    }
}
