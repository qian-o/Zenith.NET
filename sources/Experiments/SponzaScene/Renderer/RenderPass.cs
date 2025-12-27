using Zenith.NET;

namespace SponzaScene.Renderer;

internal abstract class RenderPass(string name) : DisposableObject
{
    public string Name { get; } = name;

    public bool Enabled { get; set; } = true;

    public abstract void Execute(CommandBuffer commandBuffer, RenderContext context);
}
