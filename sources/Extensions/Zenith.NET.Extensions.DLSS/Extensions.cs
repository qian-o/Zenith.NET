using System.Diagnostics;
using NGX.NET;
using Ngx = NGX.NET.NGX;

namespace Zenith.NET.Extensions.DLSS;

public static class Extensions
{
    private static readonly Lock @lock = new();
    private static readonly Dictionary<GraphicsContext, DLSSContext> contexts = [];
    private static readonly Dictionary<(GraphicsApi, string), DLSSCapabilities> capabilities = [];

    extension(GraphicsContext context)
    {
        public DLSSCapabilities DLSSCapabilities
        {
            get
            {
                using Lock.Scope _ = @lock.EnterScope();

                if (!capabilities.TryGetValue((context.GraphicsApi, context.Capabilities.DeviceName), out DLSSCapabilities dlssCapabilities))
                {
                    DLSSContext dlssContext = AcquireContext(context);

                    dlssCapabilities = dlssContext.Capabilities;

                    ReleaseContext(dlssContext);
                }

                return dlssCapabilities;
            }
        }

        public DLSSOptimalSettings GetDLSSSuperResolutionOptimalSettings(uint outputWidth, uint outputHeight, DLSSMode mode)
        {
            DLSSContext dlssContext = AcquireContext(context);

            DLSSOptimalSettings settings = dlssContext.GetOptimalSettings(NGXFeature.SuperSampling, outputWidth, outputHeight, mode);

            ReleaseContext(dlssContext);

            return settings;
        }

        public DLSSOptimalSettings GetDLSSRayReconstructionOptimalSettings(uint outputWidth, uint outputHeight, DLSSMode mode)
        {
            DLSSContext dlssContext = AcquireContext(context);

            DLSSOptimalSettings settings = dlssContext.GetOptimalSettings(NGXFeature.RayReconstruction, outputWidth, outputHeight, mode);

            ReleaseContext(dlssContext);

            return settings;
        }

        public DLSSSuperResolution CreateDLSSSuperResolution(DLSSSuperResolutionDesc desc)
        {
            return new(AcquireContext(context), desc);
        }

        public DLSSRayReconstruction CreateDLSSRayReconstruction(DLSSRayReconstructionDesc desc)
        {
            return new(AcquireContext(context), desc);
        }

        public DLSSFrameGeneration CreateDLSSFrameGeneration(DLSSFrameGenerationDesc desc)
        {
            return new(AcquireContext(context), desc);
        }
    }

    extension(Texture texture)
    {
        public DLSSBinding DLSSBinding => new(texture);
    }

    extension(NGXResult result)
    {
        internal void Success()
        {
            if (Ngx.Failed(result))
            {
                Debug.WriteLine($"NGX call failed with error: {result}");
            }
        }
    }

    internal static void ReleaseContext(DLSSContext dlssContext)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (dlssContext.RemoveReference() && contexts.Remove(dlssContext.Context))
        {
            dlssContext.Dispose();
        }
    }

    private static DLSSContext AcquireContext(GraphicsContext context)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (!contexts.TryGetValue(context, out DLSSContext? dlssContext))
        {
            contexts[context] = dlssContext = new(context);

            if (dlssContext.IsInitialized)
            {
                capabilities.TryAdd((context.GraphicsApi, context.Capabilities.DeviceName), dlssContext.Capabilities);
            }
        }

        dlssContext.AddReference();

        return dlssContext;
    }
}
