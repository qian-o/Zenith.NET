namespace Zenith.NET.Extensions.Skia;

public static class Extensions
{
    private static readonly Lock @lock = new();
    private static readonly Dictionary<GraphicsContext, SkiaContext> contexts = [];

    extension(GraphicsContext context)
    {
        public void InitializeSkia()
        {
            using Lock.Scope _ = @lock.EnterScope();

            if (!contexts.TryGetValue(context, out SkiaContext? skiaContext))
            {
                contexts[context] = skiaContext = new(context);

                context.Disposing += (_, _) =>
                {
                    using Lock.Scope _ = @lock.EnterScope();

                    if (contexts.Remove(context))
                    {
                        skiaContext.Dispose();
                    }
                };
            }
        }

        public SkiaTexture CreateSkiaTexture(SkiaTextureDesc desc)
        {
            using Lock.Scope _ = @lock.EnterScope();

            if (!contexts.TryGetValue(context, out SkiaContext? skiaContext))
            {
                throw new InvalidOperationException("The graphics context has not been initialized for Skia.");
            }

            return new(skiaContext, desc);
        }
    }
}
