using Avalonia.Controls;

namespace Zenith.NET.Views.Avalonia;

public class ZenithView : Control, IZenithView
{
    public static Output Output { get; } = new()
    {
        ColorAttachments = [PixelFormat.B8G8R8A8UNorm],
        DepthStencilAttachment = PixelFormat.D32FloatS8UInt,
        SampleCount = SampleCount.Count1
    };

    public GraphicsContext? GraphicsContext { get; set; }

    public event EventHandler<UpdateEventArgs>? UpdateRequested;

    public event EventHandler<RenderEventArgs>? RenderRequested;
}
