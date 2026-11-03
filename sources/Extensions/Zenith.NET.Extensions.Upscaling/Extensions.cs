namespace Zenith.NET.Extensions.Upscaling;

public static class Extensions
{
    private static readonly Lock @lock = new();
    private static readonly Dictionary<GraphicsContext, UpscalingBackend> backends = [];

    extension(GraphicsContext context)
    {
        public void InitializeUpscaling(params ReadOnlySpan<UpscalingApi> apis)
        {
            using Lock.Scope _ = @lock.EnterScope();

            if (!backends.ContainsKey(context))
            {
                backends[context] = new(context, apis);

                context.Disposing += static (sender, _) =>
                {
                    using Lock.Scope scope = @lock.EnterScope();

                    if (backends.Remove((GraphicsContext)sender!, out UpscalingBackend? disposedBackend))
                    {
                        disposedBackend.Dispose();
                    }
                };
            }
        }

        public UpscalingCapabilities UpscalingCapabilities => GetBackend(context).Capabilities;

        public UpscalingInputSize GetTemporalUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
        {
            return GetBackend(context).GetTemporalUpscalerInputSize(outputWidth, outputHeight, mode);
        }

        public UpscalingInputSize GetDenoisingUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
        {
            return GetBackend(context).GetDenoisingUpscalerInputSize(outputWidth, outputHeight, mode);
        }

        public TemporalUpscaler CreateTemporalUpscaler(TemporalUpscalerDesc desc)
        {
            return GetBackend(context).CreateTemporalUpscaler(desc);
        }

        public DenoisingUpscaler CreateDenoisingUpscaler(DenoisingUpscalerDesc desc)
        {
            return GetBackend(context).CreateDenoisingUpscaler(desc);
        }

        public FrameGenerator CreateFrameGenerator(FrameGeneratorDesc desc)
        {
            return GetBackend(context).CreateFrameGenerator(desc);
        }
    }

    private static UpscalingBackend GetBackend(GraphicsContext context)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (!backends.TryGetValue(context, out UpscalingBackend? backend))
        {
            throw new InvalidOperationException("The graphics context has not been initialized for upscaling.");
        }

        return backend;
    }
}
