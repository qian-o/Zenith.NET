using CornellBox.Handlers;
using Zenith.NET;

namespace CornellBox.Renderers;

internal interface IRenderer : IDisposable
{
    void Update(CameraHandler camera);

    void Render(CommandBuffer commandBuffer, FrameBuffer frameBuffer);

    void Resize(uint width, uint height);
}
