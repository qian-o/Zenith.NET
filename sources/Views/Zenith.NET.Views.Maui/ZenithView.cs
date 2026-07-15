namespace Zenith.NET.Views.Maui;

public partial class ZenithView : View, IZenithView
{
    public static readonly BindableProperty GraphicsContextProperty = BindableProperty.Create(nameof(GraphicsContext), typeof(GraphicsContext), typeof(ZenithView));

    private readonly FrameScheduler scheduler;

    public ZenithView()
    {
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

    internal void OnUpdateRequested()
    {
        UpdateRequested?.Invoke(this, new(scheduler.UpdateSeconds, scheduler.TotalSeconds));
    }

    internal void OnRenderRequested(CommandBuffer commandBuffer, Texture drawable)
    {
        RenderRequested?.Invoke(this, new(scheduler.RenderSeconds, scheduler.TotalSeconds, commandBuffer, drawable));
    }

    void IZenithView.UI(Action action)
    {
        MainThread.BeginInvokeOnMainThread(action);
    }

    void IZenithView.EnsureResources()
    {
        Handler?.Invoke(nameof(IZenithView.EnsureResources), null);
    }

    void IZenithView.Tick()
    {
        Handler?.Invoke(nameof(IZenithView.Tick), null);
    }

    void IZenithView.Present()
    {
        Handler?.Invoke(nameof(IZenithView.Present), null);
    }

    void IZenithView.ReleaseResources()
    {
        Handler?.Invoke(nameof(IZenithView.ReleaseResources), null);
    }
}
