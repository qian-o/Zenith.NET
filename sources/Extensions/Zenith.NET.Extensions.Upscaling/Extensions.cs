namespace Zenith.NET.Extensions.Upscaling;

public static class Extensions
{
    private static readonly Lock @lock = new();
    private static readonly Dictionary<GraphicsContext, UpscalingBridge> bridges = [];

    extension(GraphicsContext context)
    {
        public void InitializeUpscaling(params ReadOnlySpan<UpscalingApi> apis)
        {
            using Lock.Scope _ = @lock.EnterScope();

            if (!bridges.ContainsKey(context))
            {
                bridges[context] = new(context, apis);

                context.Disposing += static (sender, _) =>
                {
                    using Lock.Scope scope = @lock.EnterScope();

                    if (bridges.Remove((GraphicsContext)sender!, out UpscalingBridge? disposedBridge))
                    {
                        disposedBridge.Dispose();
                    }
                };
            }
        }

        public UpscalingCapabilities UpscalingCapabilities => GetBridge(context).Capabilities;

        public UpscalingInputSize GetTemporalUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
        {
            return GetBridge(context).GetTemporalUpscalerInputSize(outputWidth, outputHeight, mode);
        }

        public UpscalingInputSize GetDenoisingUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
        {
            return GetBridge(context).GetDenoisingUpscalerInputSize(outputWidth, outputHeight, mode);
        }

        public TemporalUpscaler CreateTemporalUpscaler(TemporalUpscalerDesc desc)
        {
            return GetBridge(context).CreateTemporalUpscaler(desc);
        }

        public DenoisingUpscaler CreateDenoisingUpscaler(DenoisingUpscalerDesc desc)
        {
            return GetBridge(context).CreateDenoisingUpscaler(desc);
        }

        public FrameGenerator CreateFrameGenerator(FrameGeneratorDesc desc)
        {
            return GetBridge(context).CreateFrameGenerator(desc);
        }
    }

    private static UpscalingBridge GetBridge(GraphicsContext context)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (!bridges.TryGetValue(context, out UpscalingBridge? bridge))
        {
            throw new InvalidOperationException("The graphics context has not been initialized for upscaling.");
        }

        return bridge;
    }
}
