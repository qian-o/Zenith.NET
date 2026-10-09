namespace Zenith.NET.Extensions.Skia;

public static class Extensions
{
    private static readonly Lock @lock = new();
    private static readonly Dictionary<GraphicsContext, SKRenderer> renderers = [];

    extension(GraphicsContext context)
    {
        public void InitializeSkia()
        {
            using Lock.Scope _ = @lock.EnterScope();

            if (!renderers.TryGetValue(context, out SKRenderer? renderer))
            {
                renderers[context] = renderer = new(context);

                context.Disposing += (_, _) =>
                {
                    using Lock.Scope _ = @lock.EnterScope();

                    if (renderers.Remove(context))
                    {
                        renderer.Dispose();
                    }
                };
            }
        }

        public SKTexture CreateSKTexture(SKTextureDesc desc)
        {
            using Lock.Scope _ = @lock.EnterScope();

            if (!renderers.TryGetValue(context, out SKRenderer? renderer))
            {
                throw new InvalidOperationException("The graphics context has not been initialized for Skia.");
            }

            return new(renderer, desc);
        }
    }
}
