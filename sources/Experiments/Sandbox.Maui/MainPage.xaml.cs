using Zenith.NET;
using Zenith.NET.Views;

namespace Sandbox.Maui;

public partial class MainPage : ContentPage
{
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
        var cmd = App.Context.Graphics.CommandBuffer();

        cmd.BeginRenderPass(e.FrameBuffer, new()
        {
            ColorValues = [new(0.1f, 0.2f, 0.3f, 1.0f)],
            Depth = 1.0f,
            Stencil = 0,
            Flags = ClearFlags.All
        });

        cmd.EndRenderPass();

        cmd.Submit(true);
    }
}
