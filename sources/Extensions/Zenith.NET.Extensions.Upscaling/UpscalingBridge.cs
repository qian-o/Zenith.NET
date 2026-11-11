using System.Numerics;
using Zenith.NET.Extensions.Upscaling.DLSS;
using Zenith.NET.Extensions.Upscaling.FSR;
using Zenith.NET.Extensions.Upscaling.MetalFX;

namespace Zenith.NET.Extensions.Upscaling;

internal class UpscalingBridge : DisposableObject
{
    private readonly List<Vendor> vendors = [];
    private readonly Vendor? superResolutionVendor;
    private readonly Vendor? rayReconstructionVendor;
    private readonly Vendor? frameGenerationVendor;

    public UpscalingBridge(GraphicsContext context, ReadOnlySpan<UpscalingApi> apis)
    {
        Context = context;

        if (apis.IsEmpty)
        {
            apis = [UpscalingApi.DLSS, UpscalingApi.FSR, UpscalingApi.MetalFX];
        }

        foreach (UpscalingApi api in apis.ToArray().Distinct())
        {
            if (superResolutionVendor is not null && rayReconstructionVendor is not null && frameGenerationVendor is not null)
            {
                break;
            }

            if (CreateVendor(context, api) is not Vendor vendor)
            {
                continue;
            }

            UpscalingCapabilities capabilities = vendor.Capabilities;
            superResolutionVendor ??= capabilities.SuperResolutionSupported ? vendor : null;
            rayReconstructionVendor ??= capabilities.RayReconstructionSupported ? vendor : null;
            frameGenerationVendor ??= capabilities.FrameGenerationSupported ? vendor : null;

            if (vendor == superResolutionVendor || vendor == rayReconstructionVendor || vendor == frameGenerationVendor)
            {
                vendors.Add(vendor);
            }
            else
            {
                vendor.Dispose();
            }
        }

        Capabilities = new(superResolutionVendor is not null, rayReconstructionVendor is not null, frameGenerationVendor is not null);
    }

    public GraphicsContext Context { get; }

    public UpscalingCapabilities Capabilities { get; }

    public SuperResolutionDesc CreateSuperResolutionDesc(uint outputWidth, uint outputHeight, UpscalingQuality quality)
    {
        (uint renderWidth, uint renderHeight) = GetVendor(superResolutionVendor, "super resolution").GetSuperResolutionRenderSize(outputWidth, outputHeight, quality);

        return new()
        {
            RenderWidth = renderWidth,
            RenderHeight = renderHeight,
            OutputWidth = outputWidth,
            OutputHeight = outputHeight
        };
    }

    public RayReconstructionDesc CreateRayReconstructionDesc(uint outputWidth, uint outputHeight, UpscalingQuality quality)
    {
        (uint renderWidth, uint renderHeight) = GetVendor(rayReconstructionVendor, "ray reconstruction").GetRayReconstructionRenderSize(outputWidth, outputHeight, quality);

        return new()
        {
            RenderWidth = renderWidth,
            RenderHeight = renderHeight,
            OutputWidth = outputWidth,
            OutputHeight = outputHeight
        };
    }

    public SuperResolution CreateSuperResolution(SuperResolutionDesc desc)
    {
        return GetVendor(superResolutionVendor, "super resolution").CreateSuperResolution(desc);
    }

    public RayReconstruction CreateRayReconstruction(RayReconstructionDesc desc)
    {
        return GetVendor(rayReconstructionVendor, "ray reconstruction").CreateRayReconstruction(desc);
    }

    public FrameGeneration CreateFrameGeneration(FrameGenerationDesc desc)
    {
        return GetVendor(frameGenerationVendor, "frame generation").CreateFrameGeneration(desc);
    }

    public FrameGenerationSwapChain CreateFrameGenerationSwapChain(FrameGenerationSwapChainDesc desc)
    {
        return new(Context, desc);
    }

    protected override void Destroy()
    {
        for (int i = 0; i < vendors.Count; i++)
        {
            vendors[i].Dispose();
        }
    }

    private static Vendor? CreateVendor(GraphicsContext context, UpscalingApi api)
    {
        return api switch
        {
            UpscalingApi.DLSS when context.GraphicsApi is GraphicsApi.DirectX12 or GraphicsApi.Vulkan => new DLSSVendor(context),
            UpscalingApi.FSR when context.GraphicsApi is GraphicsApi.DirectX12 or GraphicsApi.Vulkan => new FSRVendor(context),
            UpscalingApi.MetalFX when context.GraphicsApi is GraphicsApi.Metal => new MetalFXVendor(context),
            _ => default
        };
    }

    private static Vendor GetVendor(Vendor? vendor, string feature)
    {
        return vendor ?? throw new NotSupportedException($"This device does not support the {feature} with any of the specified upscaling APIs.");
    }
}

internal abstract class Vendor(GraphicsContext context) : DisposableObject
{
    public GraphicsContext Context { get; } = context;

    public abstract UpscalingCapabilities Capabilities { get; }

    public abstract (uint Width, uint Height) GetSuperResolutionRenderSize(uint outputWidth, uint outputHeight, UpscalingQuality quality);

    public abstract (uint Width, uint Height) GetRayReconstructionRenderSize(uint outputWidth, uint outputHeight, UpscalingQuality quality);

    public abstract SuperResolution CreateSuperResolution(SuperResolutionDesc desc);

    public abstract RayReconstruction CreateRayReconstruction(RayReconstructionDesc desc);

    public abstract FrameGeneration CreateFrameGeneration(FrameGenerationDesc desc);

    public static (float NearPlane, float FarPlane, float VerticalFieldOfViewInRadians, float AspectRatio, bool DepthReversed, bool DepthInfinite) DecomposePerspective(Matrix4x4 projection)
    {
        const float Epsilon = 1e-6f;

        float zeroDenominator = -projection.M33;
        float unitDenominator = projection.M34 - projection.M33;

        float zeroDepthDistance = MathF.Abs(zeroDenominator) <= Epsilon ? float.PositiveInfinity : MathF.Abs(projection.M43 / zeroDenominator);
        float unitDepthDistance = MathF.Abs(unitDenominator) <= Epsilon ? float.PositiveInfinity : MathF.Abs(projection.M43 / unitDenominator);

        bool depthReversed = zeroDepthDistance > unitDepthDistance;
        float nearPlane = MathF.Min(zeroDepthDistance, unitDepthDistance);
        float farPlane = MathF.Max(zeroDepthDistance, unitDepthDistance);
        bool depthInfinite = float.IsInfinity(farPlane) || float.IsNaN(farPlane);

        return (nearPlane, farPlane, 2.0f * MathF.Atan(1.0f / MathF.Abs(projection.M22)), MathF.Abs(projection.M22 / projection.M11), depthReversed, depthInfinite);
    }
}
