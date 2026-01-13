namespace Zenith.NET.Views;

public interface IZenithView
{
    GraphicsContext? GraphicsContext { get; set; }

    event EventHandler<RenderEventArgs>? RenderRequested;

    void PrepareFrame();

    void Render();

    void Present();
}
