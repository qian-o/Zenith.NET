namespace Zenith.NET.Views;

public interface IZenithView
{
    static Output Output { get; }

    GraphicsContext? GraphicsContext { get; set; }

    event EventHandler<RenderEventArgs>? RenderRequested;
}
