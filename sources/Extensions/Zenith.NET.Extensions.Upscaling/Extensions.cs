namespace Zenith.NET.Extensions.Upscaling;

public static class Extensions
{
    private static readonly Lock @lock = new();
    private static readonly Dictionary<GraphicsContext, UpscalingBridge> bridges = [];

    extension(GraphicsContext context)
    {
        public void InitializeUpscaling()
        {
            using Lock.Scope _ = @lock.EnterScope();

            if (!bridges.ContainsKey(context))
            {
                bridges[context] = new(context);

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

        public SuperResolution CreateSuperResolution(SuperResolutionDesc desc)
        {
            return GetBridge(context).CreateSuperResolution(desc);
        }

        public RayReconstruction CreateRayReconstruction(RayReconstructionDesc desc)
        {
            return GetBridge(context).CreateRayReconstruction(desc);
        }

        public FrameGeneration CreateFrameGeneration(FrameGenerationDesc desc)
        {
            return GetBridge(context).CreateFrameGeneration(desc);
        }

        public FrameGenerationSwapChain CreateFrameGenerationSwapChain(FrameGenerationSwapChainDesc desc)
        {
            return GetBridge(context).CreateFrameGenerationSwapChain(desc);
        }

        internal UpscalingBridge UpscalingBridge => GetBridge(context);
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
