using Hexa.NET.ImGui;

namespace Zenith.NET.Extensions.ImGui;

internal class ImGuiRenderer(GraphicsContext context, Output output, ImGuiColorSpace colorSpace) : DisposableObject
{
    public void Initialize()
    {
    }

    public void Render(CommandBuffer commandBuffer, ImDrawDataPtr drawData)
    {
    }

    protected override void Destroy()
    {
        throw new NotImplementedException();
    }
}
