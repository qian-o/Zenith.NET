using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Zenith.NET.Views.WinForms;

public class ZenithView : Control, IZenithView
{
    private readonly FrameScheduler scheduler;

    private SwapChain? swapChain;

    public ZenithView()
    {
        scheduler = new(this);

        HandleCreated += async (_, _) => await scheduler.StartAsync();
        HandleDestroyed += async (_, _) => await scheduler.StopAsync();
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public GraphicsContext? GraphicsContext { get; set; }

    public event EventHandler<UpdateEventArgs>? UpdateRequested;

    public event EventHandler<RenderEventArgs>? RenderRequested;

    protected override void OnPaint(PaintEventArgs e)
    {
        if (DesignMode)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            float t = (float)(scheduler.TotalSeconds * 0.06 % 1.0);

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

            using Font font = new(Font.FontFamily, (float)Math.Clamp(ClientRectangle.Height / 15.0, 14.0, 48.0), Font.Style, GraphicsUnit.Pixel);
            using SolidBrush shadow = new(Color.FromArgb(0x66, 0x00, 0x00, 0x00));
            using SolidBrush white = new(Color.FromArgb((int)(0.98 * 255), 0xFF, 0xFF, 0xFF));

            SizeF size = e.Graphics.MeasureString("ZenithView", font, int.MaxValue);
            float x = (ClientRectangle.Width - size.Width) / 2.0f;
            float y = (ClientRectangle.Height - size.Height) / 2.0f;

            e.Graphics.DrawString("ZenithView", font, shadow, x + 1.0f, y + 1.0f);
            e.Graphics.DrawString("ZenithView", font, white, x, y);
        }
    }

    void IZenithView.UI(Action action)
    {
        Invoke(action);
    }

    void IZenithView.EnsureResources()
    {
        if (GraphicsContext is null || ClientSize.Width is 0 || ClientSize.Height is 0)
        {
            return;
        }

        uint width = Math.Clamp((uint)ClientSize.Width, 1, uint.MaxValue);
        uint height = Math.Clamp((uint)ClientSize.Height, 1, uint.MaxValue);

        if (swapChain is null)
        {
            swapChain = GraphicsContext.CreateSwapChain(new()
            {
                Surface = Surface.Win32(Handle, width, height),
                ColorTargetFormat = ZenithViewHelper.ColorFormat
            });
        }
        else if (swapChain.Desc.Surface.Width != width || swapChain.Desc.Surface.Height != height)
        {
            swapChain.Resize(width, height);
        }
    }

    void IZenithView.Tick()
    {
        if (swapChain is null)
        {
            return;
        }

        UpdateRequested?.Invoke(this, new(scheduler.UpdateSeconds, scheduler.TotalSeconds));
        RenderRequested?.Invoke(this, new(scheduler.RenderSeconds, scheduler.TotalSeconds, swapChain.CurrentColorTarget));
    }

    void IZenithView.Present()
    {
        swapChain?.Present();
    }

    void IZenithView.ReleaseResources()
    {
        swapChain?.Dispose();
        swapChain = null;
    }
}
