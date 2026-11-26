using System.Numerics;
using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal unsafe partial class DLSSVendor : Vendor
{
    private readonly nint device;

    public DLSSVendor(GraphicsContext context) : base(context)
    {
        device = context.GraphicsApi switch
        {
            GraphicsApi.DirectX12 => context.DirectX12(),
            GraphicsApi.Vulkan => context.Vulkan(),
            _ => default
        };

        string projectId = typeof(GraphicsContext).GUID.ToString();
        string engineVersion = typeof(GraphicsContext).Assembly.GetName().Version!.ToString();
        string dataPath = Path.Combine(Path.GetTempPath(), "Zenith.NET", "NGX");

        Directory.CreateDirectory(dataPath);

        using NGXStrings strings = new();
        void* runtimeDirectory = strings.AllocWide(Ngx.RuntimeDirectory);

        NGXFeatureCommonInfo featureInfo = new()
        {
            PathListInfo = new()
            {
                Path = &runtimeDirectory,
                Length = 1
            }
        };

        switch (context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                InitializeDirectX12(projectId, engineVersion, dataPath, featureInfo);
                break;

            case GraphicsApi.Vulkan:
                InitializeVulkan(projectId, engineVersion, dataPath, featureInfo);
                break;
        }

        NGXParameter parameters = default;
        switch (context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                Ngx.D3D12.GetCapabilityParameters(&parameters).Success();
                break;

            case GraphicsApi.Vulkan:
                Ngx.Vulkan.GetCapabilityParameters(&parameters).Success();
                break;
        }

        SuperResolutionSupported = IsSupported(parameters, Ngx.ParameterSuperSamplingAvailable);
        RayReconstructionSupported = IsSupported(parameters, Ngx.ParameterSuperSamplingDenoisingAvailable);
        FrameGenerationSupported = IsSupported(parameters, Ngx.ParameterFrameGenerationAvailable);

        DestroyParameters(parameters);
    }

    public override bool SuperResolutionSupported { get; }

    public override bool RayReconstructionSupported { get; }

    public override bool FrameGenerationSupported { get; }

    public override SuperResolution CreateSuperResolution(SuperResolutionDesc desc)
    {
        return new DLSSSuperResolution(this, desc);
    }

    public override RayReconstruction CreateRayReconstruction(RayReconstructionDesc desc)
    {
        return new DLSSRayReconstruction(this, desc);
    }

    public override FrameGeneration CreateFrameGeneration(FrameGenerationDesc desc)
    {
        return new DLSSFrameGeneration(this, desc);
    }

    public NGXParameter CreateParameters()
    {
        NGXParameter parameters = default;
        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                Ngx.D3D12.AllocateParameters(&parameters).Success();
                break;

            case GraphicsApi.Vulkan:
                Ngx.Vulkan.AllocateParameters(&parameters).Success();
                break;
        }

        return parameters;
    }

    public void DestroyParameters(NGXParameter parameters)
    {
        if (parameters.IsNull)
        {
            return;
        }

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                Ngx.D3D12.DestroyParameters(parameters).Success();
                break;

            case GraphicsApi.Vulkan:
                Ngx.Vulkan.DestroyParameters(parameters).Success();
                break;
        }
    }

    public NGXHandle CreateFeature(NGXParameter parameters, SuperResolutionDesc desc, SuperResolutionArgs args)
    {
        ResetParameters(parameters);

        NGXDLSSCreateParams createParams = ParameterBuilder.CreationParams(desc, args);

        CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();

        NGXHandle handle = default;
        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                handle = CreateFeatureDirectX12(commandBuffer.DirectX12(), parameters, createParams);
                break;

            case GraphicsApi.Vulkan:
                handle = CreateFeatureVulkan(commandBuffer.Vulkan(), parameters, createParams);
                break;
        }

        commandBuffer.Submit().Wait();

        return handle;
    }

    public NGXHandle CreateFeature(NGXParameter parameters, RayReconstructionDesc desc, RayReconstructionArgs args)
    {
        ResetParameters(parameters);

        NGXDLSSDCreateParams createParams = ParameterBuilder.CreationParams(desc, args);

        CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();

        NGXHandle handle = default;
        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                handle = CreateFeatureDirectX12(commandBuffer.DirectX12(), parameters, createParams);
                break;

            case GraphicsApi.Vulkan:
                handle = CreateFeatureVulkan(commandBuffer.Vulkan(), parameters, createParams);
                break;
        }

        commandBuffer.Submit().Wait();

        return handle;
    }

    public NGXHandle CreateFeature(NGXParameter parameters, FrameGenerationDesc desc)
    {
        ResetParameters(parameters);

        NGXDLSSGCreateParams createParams = ParameterBuilder.CreationParams(desc, Context.GraphicsApi);

        CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();

        NGXHandle handle = default;
        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                handle = CreateFeatureDirectX12(commandBuffer.DirectX12(), parameters, createParams);
                break;

            case GraphicsApi.Vulkan:
                handle = CreateFeatureVulkan(commandBuffer.Vulkan(), parameters, createParams);
                break;
        }

        commandBuffer.Submit().Wait();

        return handle;
    }

    public void DestroyFeature(NGXHandle handle)
    {
        if (handle.IsNull)
        {
            return;
        }

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                Ngx.D3D12.ReleaseFeature(handle).Success();
                break;

            case GraphicsApi.Vulkan:
                Ngx.Vulkan.ReleaseFeature(handle).Success();
                break;
        }
    }

    public void EvaluateFeature(CommandBuffer commandBuffer, NGXParameter parameters, NGXHandle handle, SuperResolutionArgs args, Texture output)
    {
        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                EvaluateFeatureDirectX12(commandBuffer.DirectX12(), parameters, handle, args, output);
                break;

            case GraphicsApi.Vulkan:
                EvaluateFeatureVulkan(commandBuffer.Vulkan(), parameters, handle, args, output);
                break;
        }
    }

    public void EvaluateFeature(CommandBuffer commandBuffer, NGXParameter parameters, NGXHandle handle, RayReconstructionArgs args, Texture output)
    {
        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                EvaluateFeatureDirectX12(commandBuffer.DirectX12(), parameters, handle, args, output);
                break;

            case GraphicsApi.Vulkan:
                EvaluateFeatureVulkan(commandBuffer.Vulkan(), parameters, handle, args, output);
                break;
        }
    }

    public void EvaluateFeature(CommandBuffer commandBuffer, NGXParameter parameters, NGXHandle handle, FrameGenerationArgs args, Texture output, Matrix4x4 previousViewProjection)
    {
        NGXDLSSGOptEvalParams optEvalParams = ParameterBuilder.EvaluationParams(args, output, previousViewProjection);

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                EvaluateFeatureDirectX12(commandBuffer.DirectX12(), parameters, handle, args, output, optEvalParams);
                break;

            case GraphicsApi.Vulkan:
                EvaluateFeatureVulkan(commandBuffer.Vulkan(), parameters, handle, args, output, optEvalParams);
                break;
        }
    }

    protected override void Destroy()
    {
        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                DestroyDirectX12();
                break;

            case GraphicsApi.Vulkan:
                DestroyVulkan();
                break;
        }
    }

    private static bool IsSupported(NGXParameter parameters, ReadOnlySpan<byte> name)
    {
        if (parameters.IsNull)
        {
            return false;
        }

        fixed (byte* pointer = name)
        {
            int value = 0;
            Ngx.Parameter.GetI(parameters, pointer, &value).Success();

            return value is not 0;
        }
    }

    private static void ResetParameters(NGXParameter parameters)
    {
        Ngx.Parameter.Reset(parameters);

        fixed (byte* pointer = Ngx.ParameterFreeMemOnReleaseFeature)
        {
            Ngx.Parameter.SetUI(parameters, pointer, 1);
        }
    }
}

file static class ParameterBuilder
{
    public static NGXDLSSCreateParams CreationParams(SuperResolutionDesc desc, SuperResolutionArgs args)
    {
        return new()
        {
            Feature = new()
            {
                Width = desc.RenderWidth,
                Height = desc.RenderHeight,
                TargetWidth = desc.OutputWidth,
                TargetHeight = desc.OutputHeight,
                PerfQualityValue = DLSSFormats.DLSS(UpscalingHelper.Quality(desc.RenderWidth, desc.RenderHeight, desc.OutputWidth, desc.OutputHeight))
            },
            FeatureCreateFlags = (int)Flags(args.Color.Desc.Format, args.Exposure, args.Projection)
        };
    }

    public static NGXDLSSDCreateParams CreationParams(RayReconstructionDesc desc, RayReconstructionArgs args)
    {
        return new()
        {
            DenoiseMode = NGXDLSSDenoiseMode.DlUnified,
            RoughnessMode = NGXDLSSRoughnessMode.Unpacked,
            UseHWDepth = NGXDLSSDepthType.Hw,
            Width = desc.RenderWidth,
            Height = desc.RenderHeight,
            TargetWidth = desc.OutputWidth,
            TargetHeight = desc.OutputHeight,
            PerfQualityValue = DLSSFormats.DLSS(UpscalingHelper.Quality(desc.RenderWidth, desc.RenderHeight, desc.OutputWidth, desc.OutputHeight)),
            FeatureCreateFlags = (int)Flags(args.Color.Desc.Format, args.Exposure, args.Projection)
        };
    }

    public static NGXDLSSGCreateParams CreationParams(FrameGenerationDesc desc, GraphicsApi graphicsApi)
    {
        return new()
        {
            Width = desc.OutputWidth,
            Height = desc.OutputHeight,
            NativeBackbufferFormat = graphicsApi switch
            {
                GraphicsApi.DirectX12 => DLSSFormats.DirectX12(desc.Format),
                GraphicsApi.Vulkan => (uint)DLSSFormats.Vulkan(desc.Format),
                _ => default
            },
            RenderWidth = desc.RenderWidth,
            RenderHeight = desc.RenderHeight
        };
    }

    public static NGXDLSSGOptEvalParams EvaluationParams(FrameGenerationArgs args, Texture output, Matrix4x4 previousViewProjection)
    {
        (float nearPlane, float farPlane, float verticalFieldOfViewInRadians, float aspectRatio, bool depthReversed, _) = UpscalingHelper.DecomposePerspective(args.Projection);
        (Vector3 position, Vector3 up, Vector3 right, Vector3 forward) = UpscalingHelper.DecomposeCamera(args.View, args.Projection);

        Matrix4x4.Invert(args.View, out Matrix4x4 viewToWorld);
        Matrix4x4.Invert(args.Projection, out Matrix4x4 clipToCameraView);

        Matrix4x4 clipToPrevClip = Matrix4x4.Identity;
        Matrix4x4 prevClipToClip = Matrix4x4.Identity;
        if (!args.Reset)
        {
            clipToPrevClip = clipToCameraView * viewToWorld * previousViewProjection;
            Matrix4x4.Invert(clipToPrevClip, out prevClipToClip);
        }

        return new()
        {
            CameraViewToClip = args.Projection,
            ClipToCameraView = clipToCameraView,
            ClipToLensClip = Matrix4x4.Identity,
            ClipToPrevClip = clipToPrevClip,
            PrevClipToClip = prevClipToClip,
            JitterOffset = -args.JitterOffset,
            MvecScale = new(args.MotionVectors.Desc.Width, args.MotionVectors.Desc.Height),
            CameraPos = position,
            CameraUp = up,
            CameraRight = right,
            CameraFwd = forward,
            CameraNear = nearPlane,
            CameraFar = farPlane,
            CameraFOV = verticalFieldOfViewInRadians,
            CameraAspectRatio = aspectRatio,
            ColorBuffersHDR = UpscalingHelper.IsHdr(args.Color.Desc.Format),
            DepthInverted = depthReversed,
            CameraMotionIncluded = true,
            Reset = args.Reset,
            MvecsSubrectSize = Dimensions(args.MotionVectors),
            DepthSubrectSize = Dimensions(args.Depth),
            HudLessSubrectSize = args.Hudless is not null ? Dimensions(args.Hudless) : default,
            BackbufferSubrectSize = Dimensions(args.Color),
            OutputInterpSubrectSize = Dimensions(output)
        };
    }

    private static NGXDimensions Dimensions(Texture texture)
    {
        return new()
        {
            Width = texture.Desc.Width,
            Height = texture.Desc.Height
        };
    }

    private static NGXDLSSFeatureFlags Flags(PixelFormat format, Texture? exposure, Matrix4x4 projection)
    {
        NGXDLSSFeatureFlags flags = NGXDLSSFeatureFlags.MvLowRes;

        if (UpscalingHelper.IsFloat(format))
        {
            flags |= NGXDLSSFeatureFlags.IsHdr;
        }

        if (exposure is null)
        {
            flags |= NGXDLSSFeatureFlags.AutoExposure;
        }

        if (UpscalingHelper.IsDepthReversed(projection))
        {
            flags |= NGXDLSSFeatureFlags.DepthInverted;
        }

        return flags;
    }
}
