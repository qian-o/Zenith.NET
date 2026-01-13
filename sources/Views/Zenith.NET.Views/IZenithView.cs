namespace Zenith.NET.Views;

public interface IZenithView
{
    GraphicsContext? GraphicsContext { get; set; }

    event EventHandler<UpdateEventArgs>? UpdateRequested;

    event EventHandler<RenderEventArgs>? RenderRequested;

    void UI(Action action);

    void Prepare();

    void Frame();

    void Present();
}
