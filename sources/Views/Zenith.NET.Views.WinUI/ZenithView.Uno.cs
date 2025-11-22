#if !WINDOWS
namespace Zenith.NET.Views.WinUI;

public partial class ZenithView
{
    public static Output Output { get; } = new()
    {
        ColorAttachments = [PixelFormat.R8G8B8A8UNorm],
        DepthStencilAttachment = PixelFormat.D24UNormS8UInt,
        SampleCount = SampleCount.Count1
    };

    private void OnRender(GraphicsContext graphicsContext)
    {
    }

    private void Destroy()
    {
    }
}
#endif