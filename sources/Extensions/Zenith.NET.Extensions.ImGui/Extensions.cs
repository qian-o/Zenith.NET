using Hexa.NET.ImGui;

namespace Zenith.NET.Extensions.ImGui;

public static class Extensions
{
    private static readonly Lock @lock = new();
    private static readonly Dictionary<GraphicsContext, ImGuiBackend> backends = [];

    extension(GraphicsContext context)
    {
        public void InitializeImGui(IImGuiPlatform platform, AttachmentFormats attachmentFormats, ImGuiColorSpace colorSpace)
        {
            using Lock.Scope _ = @lock.EnterScope();

            if (!backends.TryGetValue(context, out ImGuiBackend? backend))
            {
                backends[context] = backend = new(context, platform, attachmentFormats, colorSpace);

                context.Disposing += (_, _) =>
                {
                    using Lock.Scope _ = @lock.EnterScope();

                    if (backends.Remove(context))
                    {
                        backend.Dispose();
                    }
                };
            }
        }

        public void BeginImGuiFrame(double delta, uint width, uint height)
        {
            GetBackend(context).Update(delta, width, height);
        }

        public void EndImGuiFrame(CommandBuffer commandBuffer, ColorAttachment colorAttachment)
        {
            GetBackend(context).Render(commandBuffer, colorAttachment);
        }
    }

    extension(Texture texture)
    {
        public ImTextureRef ImGuiBinding => GetBackend(texture.Context).Binding(texture);
    }

    extension(TextureView textureView)
    {
        public ImTextureRef ImGuiBinding => GetBackend(textureView.Context).Binding(textureView);
    }

    private static ImGuiBackend GetBackend(GraphicsContext context)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (!backends.TryGetValue(context, out ImGuiBackend? backend))
        {
            throw new InvalidOperationException("The graphics context has not been initialized for ImGui.");
        }

        return backend;
    }
}
