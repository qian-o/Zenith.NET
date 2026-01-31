using System.Numerics;
using Zenith.NET;
using Zenith.NET.Views;

namespace Sandbox.Maui;

public partial class MainPage : ContentPage
{
    private const float gradientSpeed = 0.01f;

    private float gradientT = 0f;

    int count = 0;

    public MainPage()
    {
        InitializeComponent();
    }

    private void OnCounterClicked(object? sender, EventArgs e)
    {
        count++;

        CounterBtn.Text = count is 1 ? $"Clicked {count} time" : $"Clicked {count} times";

        SemanticScreenReader.Announce(CounterBtn.Text);
    }

    private void ZenithView_RenderRequested(object sender, RenderEventArgs e)
    {
        gradientT += gradientSpeed;
        if (gradientT > 1f) gradientT -= 1f;

        var cmd = App.Context.Graphics.CommandBuffer();

        cmd.BeginRenderPass(e.FrameBuffer, new()
        {
            ColorValues = [ColorFromHSV(gradientT * 360, 0.7f, 1.0f)],
            Depth = 1.0f,
            Stencil = 0,
            Flags = ClearFlags.All
        });

        cmd.EndRenderPass();

        cmd.Submit(true);
    }

    private static Vector4 ColorFromHSV(double hue, double saturation, double value)
    {
        int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
        double f = hue / 60 - Math.Floor(hue / 60);

        value = value * 255;
        int v = Convert.ToInt32(value);
        int p = Convert.ToInt32(value * (1 - saturation));
        int q = Convert.ToInt32(value * (1 - f * saturation));
        int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

        return hi switch
        {
            0 => new(v / 255f, t / 255f, p / 255f, 1f),
            1 => new(q / 255f, v / 255f, p / 255f, 1f),
            2 => new(p / 255f, v / 255f, t / 255f, 1f),
            3 => new(p / 255f, q / 255f, v / 255f, 1f),
            4 => new(t / 255f, p / 255f, v / 255f, 1f),
            _ => new(v / 255f, p / 255f, q / 255f, 1f),
        };
    }
}
