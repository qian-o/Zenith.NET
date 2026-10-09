using Hexa.NET.ImGui;

namespace Zenith.NET.Extensions.ImGui;

public static class Extensions
{
    private static readonly Lock @lock = new();
    private static readonly Dictionary<GraphicsContext, ImGuiContext> contexts = [];

    extension(GraphicsContext context)
    {
        public void InitializeImGui(IImGuiPlatform platform, AttachmentFormats attachmentFormats, ImGuiColorSpace colorSpace)
        {
            using Lock.Scope _ = @lock.EnterScope();

            if (!contexts.TryGetValue(context, out ImGuiContext? imGuiContext))
            {
                contexts[context] = imGuiContext = new(context, platform, attachmentFormats, colorSpace);

                context.Disposing += (_, _) =>
                {
                    using Lock.Scope _ = @lock.EnterScope();

                    if (contexts.Remove(context))
                    {
                        imGuiContext.Dispose();
                    }
                };
            }
        }

        public void BeginImGuiFrame(double delta, uint width, uint height)
        {
            GetContext(context).Update(delta, width, height);
        }

        public void EndImGuiFrame(CommandBuffer commandBuffer, ColorAttachment colorAttachment)
        {
            GetContext(context).Render(commandBuffer, colorAttachment);
        }
    }

    extension(Texture texture)
    {
        public ImTextureRef ImGuiBinding => GetContext(texture.Context).Binding(texture);
    }

    extension(TextureView textureView)
    {
        public ImTextureRef ImGuiBinding => GetContext(textureView.Context).Binding(textureView);
    }

    private static ImGuiContext GetContext(GraphicsContext context)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (!contexts.TryGetValue(context, out ImGuiContext? imGuiContext))
        {
            throw new InvalidOperationException("The graphics context has not been initialized for ImGui.");
        }

        return imGuiContext;
    }
}
