using System.Diagnostics;
using NGX.NET;

namespace Zenith.NET.Extensions.DLSS;

public static class Extensions
{
    private static readonly Lock @lock = new();
    private static readonly Dictionary<GraphicsContext, DLSSBackend> backends = [];

    extension(GraphicsContext context)
    {
        public void InitializeDLSS(Guid project, string version, string dataDirectory)
        {
            using Lock.Scope _ = @lock.EnterScope();

            if (!backends.TryGetValue(context, out DLSSBackend? backend))
            {
                backends[context] = backend = new(context, project, version, dataDirectory);

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

        public DLSSCapabilities DLSSCapabilities => GetBackend(context).Capabilities;

        public DLSSOptimalSettings GetDLSSSuperResolutionOptimalSettings(uint outputWidth, uint outputHeight, DLSSMode mode)
        {
            return GetBackend(context).GetOptimalSettings(NGXFeature.SuperSampling, outputWidth, outputHeight, mode);
        }

        public DLSSOptimalSettings GetDLSSRayReconstructionOptimalSettings(uint outputWidth, uint outputHeight, DLSSMode mode)
        {
            return GetBackend(context).GetOptimalSettings(NGXFeature.RayReconstruction, outputWidth, outputHeight, mode);
        }

        public DLSSSuperResolution CreateDLSSSuperResolution(DLSSSuperResolutionDesc desc)
        {
            return new(GetBackend(context), desc);
        }

        public DLSSRayReconstruction CreateDLSSRayReconstruction(DLSSRayReconstructionDesc desc)
        {
            return new(GetBackend(context), desc);
        }

        public DLSSFrameGeneration CreateDLSSFrameGeneration(DLSSFrameGenerationDesc desc)
        {
            return new(GetBackend(context), desc);
        }
    }

    extension(Texture texture)
    {
        public DLSSBinding DLSSBinding => GetBackend(texture.Context).Binding(texture);
    }

    extension(NGXResult result)
    {
        internal void Success()
        {
            if (Ngx.NVSDK_NGX_FAILED(result))
            {
                Debug.WriteLine($"NGX call failed with error: {result}");
            }
        }
    }

    extension(int result)
    {
        internal void Success()
        {
            if (result is not 0)
            {
                Debug.WriteLine($"Vulkan call failed with error code: {result}");
            }
        }
    }

    private static DLSSBackend GetBackend(GraphicsContext context)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (!backends.TryGetValue(context, out DLSSBackend? backend))
        {
            throw new InvalidOperationException("The graphics context has not been initialized for DLSS.");
        }

        return backend;
    }
}
