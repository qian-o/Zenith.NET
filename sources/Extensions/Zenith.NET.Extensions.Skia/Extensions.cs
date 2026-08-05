namespace Zenith.NET.Extensions.Skia;

public static class Extensions
{
    private static readonly Lock @lock = new();
    private static readonly Dictionary<GraphicsContext, SKRenderer> renderers = [];

    extension(GraphicsContext context)
    {
        public SKTexture CreateSKTexture(SKTextureDesc desc)
        {
            using Lock.Scope _ = @lock.EnterScope();

            if (!renderers.TryGetValue(context, out SKRenderer? renderer))
            {
                renderers[context] = renderer = new(context);
            }

            renderer.AddReference();

            return new(renderer, desc);
        }
    }

    internal static void ReleaseRenderer(SKRenderer renderer)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (renderer.RemoveReference() && renderers.Remove(renderer.Context))
        {
            renderer.Dispose();
        }
    }
}
