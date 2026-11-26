namespace Zenith.NET.Extensions.Upscaling;

public static class Extensions
{
    private static readonly Lock @lock = new();
    private static readonly Dictionary<GraphicsContext, UpscalingBridge> bridges = [];

    extension(Capabilities capabilities)
    {
        public bool SuperResolutionSupported => GetBridge(capabilities.Context).SuperResolutionSupported;

        public bool RayReconstructionSupported => GetBridge(capabilities.Context).RayReconstructionSupported;

        public bool FrameGenerationSupported => GetBridge(capabilities.Context).FrameGenerationSupported;
    }

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
    }

    extension(TextureUsages)
    {
        public static TextureUsages UpscalingInput => TextureUsages.Sampled | TextureUsages.TransferSrc;
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
