using Sponza.Handlers;
using Sponza.Models;
using Zenith.NET;

namespace Sponza;

// 组装 Pass 和渲染流程, 最终给外部返回一个 Color，其余的资源都在内部使用。
internal class Renderer : IDisposable
{
    public RenderSettings Settings;

    public Texture Color => null!;

    public void Update(CameraHandler camera)
    {
    }

    public void Render(CommandBuffer commandBuffer)
    {
    }

    public void Resize(uint width, uint height)
    {
    }

    public void Dispose()
    {
    }
}
