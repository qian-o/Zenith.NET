namespace Zenith.NET.Extensions.Skia;

public static class Extensions
{
    private static readonly Lock @lock = new();
    private static readonly Dictionary<GraphicsContext, SkiaBackend> backends = [];

    extension(GraphicsContext context)
    {
        public void InitializeSkia()
        {
            using Lock.Scope _ = @lock.EnterScope();

            if (!backends.TryGetValue(context, out SkiaBackend? backend))
            {
                backends[context] = backend = new(context);

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

        public SkiaTexture CreateSkiaTexture(SkiaTextureDesc desc)
        {
            using Lock.Scope _ = @lock.EnterScope();

            if (!backends.TryGetValue(context, out SkiaBackend? backend))
            {
                throw new InvalidOperationException("The graphics context has not been initialized for Skia.");
            }

            return new(backend, desc);
        }
    }
}
