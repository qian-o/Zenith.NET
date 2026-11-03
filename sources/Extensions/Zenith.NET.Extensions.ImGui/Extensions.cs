using Hexa.NET.ImGui;

namespace Zenith.NET.Extensions.ImGui;

public static class Extensions
{
    private static readonly Lock @lock = new();
    private static readonly Dictionary<GraphicsContext, ImGuiBridge> bridges = [];

    extension(GraphicsContext context)
    {
        public void InitializeImGui(IImGuiPlatform platform, AttachmentFormats attachmentFormats, ImGuiColorSpace colorSpace)
        {
            using Lock.Scope _ = @lock.EnterScope();

            if (!bridges.ContainsKey(context))
            {
                bridges[context] = new(context, platform, attachmentFormats, colorSpace);

                context.Disposing += static (sender, _) =>
                {
                    using Lock.Scope scope = @lock.EnterScope();

                    if (bridges.Remove((GraphicsContext)sender!, out ImGuiBridge? disposedBridge))
                    {
                        disposedBridge.Dispose();
                    }
                };
            }
        }

        public void BeginImGuiFrame(double delta, uint width, uint height)
        {
            GetBridge(context).Update(delta, width, height);
        }

        public void EndImGuiFrame(CommandBuffer commandBuffer, ColorAttachment colorAttachment)
        {
            GetBridge(context).Render(commandBuffer, colorAttachment);
        }
    }

    extension(Texture texture)
    {
        public ImTextureRef ImGuiBinding => GetBridge(texture.Context).Binding(texture);
    }

    extension(TextureView textureView)
    {
        public ImTextureRef ImGuiBinding => GetBridge(textureView.Context).Binding(textureView);
    }

    private static ImGuiBridge GetBridge(GraphicsContext context)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (!bridges.TryGetValue(context, out ImGuiBridge? bridge))
        {
            throw new InvalidOperationException("The graphics context has not been initialized for ImGui.");
        }

        return bridge;
    }
}
