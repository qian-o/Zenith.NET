using System.Diagnostics;
using System.Numerics;
using Metal.NET;
using NGX.NET;
using Zenith.NET.Extensions.Reconstruction.DLSS;
using Zenith.NET.Extensions.Reconstruction.MetalFX;

namespace Zenith.NET.Extensions.Reconstruction;

public static class Extensions
{
    private static readonly Lock @lock = new();
    private static readonly Dictionary<GraphicsContext, ReconstructionBackend> backends = [];

    extension(GraphicsContext context)
    {
        public void InitializeReconstruction(string dataPath)
        {
            using Lock.Scope _ = @lock.EnterScope();

            if (!backends.ContainsKey(context))
            {
                backends[context] = context.GraphicsApi is GraphicsApi.Metal ? new MetalFXBackend(context, dataPath) : new DLSSBackend(context, dataPath);

                context.Disposing += static (sender, _) =>
                {
                    using Lock.Scope scope = @lock.EnterScope();

                    if (backends.Remove((GraphicsContext)sender!, out ReconstructionBackend? disposedBackend))
                    {
                        disposedBackend.Dispose();
                    }
                };
            }
        }

        public ReconstructionCapabilities ReconstructionCapabilities => GetBackend(context).Capabilities;

        public UpscalerInputSize GetTemporalUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalerMode mode)
        {
            return GetBackend(context).GetTemporalUpscalerInputSize(outputWidth, outputHeight, mode);
        }

        public UpscalerInputSize GetDenoisingUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalerMode mode)
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

    extension(CommandBuffer commandBuffer)
    {
        internal MTL4CommandBuffer Metal()
        {
            return new(commandBuffer.GetNativeObject(NativeObjectType.MTL4CommandBuffer), NativeObjectOwnership.Borrowed);
        }
    }

    extension(Texture texture)
    {
        internal MTLTexture Metal()
        {
            return new(texture.GetNativeObject(NativeObjectType.MTLTexture), NativeObjectOwnership.Borrowed);
        }
    }

    extension(Matrix4x4 viewToClip)
    {
        internal (float NearPlane, float FarPlane, float VerticalFieldOfViewInRadians, float AspectRatio) DecomposePerspective()
        {
            float zeroDepthDistance = MathF.Abs(viewToClip.M43 / -viewToClip.M33);
            float unitDepthDistance = MathF.Abs(viewToClip.M43 / (viewToClip.M34 - viewToClip.M33));

            return (MathF.Min(zeroDepthDistance, unitDepthDistance), MathF.Max(zeroDepthDistance, unitDepthDistance), 2.0f * MathF.Atan(1.0f / viewToClip.M22), viewToClip.M22 / viewToClip.M11);
        }
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

        internal bool IsSuccess()
        {
            return !Ngx.NVSDK_NGX_FAILED(result);
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

    extension(NSError error)
    {
        internal void Success()
        {
            if (!error.IsNull)
            {
                Debug.WriteLine($"Metal call failed with error: {error.LocalizedDescription}");

                error.Dispose();
            }
        }
    }

    private static ReconstructionBackend GetBackend(GraphicsContext context)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (!backends.TryGetValue(context, out ReconstructionBackend? backend))
        {
            throw new InvalidOperationException("The graphics context has not been initialized for reconstruction.");
        }

        return backend;
    }
}
