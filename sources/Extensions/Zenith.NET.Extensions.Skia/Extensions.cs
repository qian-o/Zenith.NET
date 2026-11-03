namespace Zenith.NET.Extensions.Skia;

public static class Extensions
{
    private static readonly Lock @lock = new();
    private static readonly Dictionary<GraphicsContext, SkiaBridge> bridges = [];

    extension(GraphicsContext context)
    {
        public void InitializeSkia()
        {
            using Lock.Scope _ = @lock.EnterScope();

            if (!bridges.ContainsKey(context))
            {
                bridges[context] = new(context);

                context.Disposing += static (sender, _) =>
                {
                    using Lock.Scope scope = @lock.EnterScope();

                    if (bridges.Remove((GraphicsContext)sender!, out SkiaBridge? disposedBridge))
                    {
                        disposedBridge.Dispose();
                    }
                };
            }
        }

        public SkiaTexture CreateSkiaTexture(SkiaTextureDesc desc)
        {
            using Lock.Scope _ = @lock.EnterScope();

            if (!bridges.TryGetValue(context, out SkiaBridge? bridge))
            {
                throw new InvalidOperationException("The graphics context has not been initialized for Skia.");
            }

            return new(bridge, desc);
        }
    }
}
