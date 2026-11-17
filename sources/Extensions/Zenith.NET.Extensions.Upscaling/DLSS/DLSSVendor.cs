using System.Numerics;
using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal unsafe partial class DLSSVendor : Vendor
{
    private readonly Lock retireLock = new();
    private readonly List<RetiredFeature> retiredFeatures = [];
    private readonly bool isInitialized;

    public nint Device;

    public DLSSVendor(GraphicsContext context) : base(context)
    {
        string projectId = typeof(GraphicsContext).GUID.ToString();
        string engineVersion = typeof(GraphicsContext).Assembly.GetName().Version!.ToString();
        string dataPath = Path.Combine(Path.GetTempPath(), "Zenith.NET", "NGX");

        Directory.CreateDirectory(dataPath);

        using NGXStrings strings = new();
        void* runtimeDirectory = strings.AllocWide(Ngx.RuntimeDirectory);

        NGXFeatureCommonInfo commonInfo = new()
        {
            PathListInfo = new()
            {
                Path = &runtimeDirectory,
                Length = 1
            }
        };

        isInitialized = context.GraphicsApi switch
        {
            GraphicsApi.DirectX12 => InitializeDirectX12(projectId, engineVersion, dataPath, &commonInfo),
            GraphicsApi.Vulkan => InitializeVulkan(projectId, engineVersion, dataPath, &commonInfo),
            _ => false
        };

        if (!isInitialized || !TryGetCapabilityParameters(out NGXParameter parameters))
        {
            Capabilities = new(false, false, false);

            return;
        }

        Capabilities = new(DLSSHelper.GetBool(parameters, Ngx.ParameterSuperSamplingAvailable), DLSSHelper.GetBool(parameters, Ngx.ParameterSuperSamplingDenoisingAvailable), DLSSHelper.GetBool(parameters, Ngx.ParameterFrameGenerationAvailable));

        DestroyParameters(parameters);
    }

    public override UpscalingCapabilities Capabilities { get; }

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

    public NGXParameter AllocateParameters()
    {
        NGXParameter parameters = default;

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                Ngx.D3D12.AllocateParameters(out parameters).Success();
                break;

            case GraphicsApi.Vulkan:
                Ngx.Vulkan.AllocateParameters(out parameters).Success();
                break;
        }

        if (!parameters.IsNull)
        {
            DLSSHelper.SetUI(parameters, Ngx.ParameterFreeMemOnReleaseFeature, 1);
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

    public void ReleaseFeature(NGXHandle handle)
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

    public void Create(CommandBuffer commandBuffer, DLSSSuperResolution feature, SuperResolutionArgs args)
    {
        PollRetiredFeatures(commandBuffer);

        NGXDLSSCreateParams createParams = new()
        {
            Feature = new()
            {
                Width = feature.Desc.RenderWidth,
                Height = feature.Desc.RenderHeight,
                TargetWidth = feature.Desc.OutputWidth,
                TargetHeight = feature.Desc.OutputHeight,
                PerfQualityValue = DLSSHelper.PerfQuality(feature.Desc.RenderWidth, feature.Desc.RenderHeight, feature.Desc.OutputWidth, feature.Desc.OutputHeight)
            },
            FeatureCreateFlags = DLSSHelper.FeatureFlags(args.Color.Desc.Format, args.Exposure, args.Projection)
        };

        RetireFeature(commandBuffer, feature.Handle);
        feature.Handle = default;
        DLSSHelper.ResetParameters(feature.Parameters);

        if (args.Exposure is not null)
        {
            DLSSHelper.SetUI(feature.Parameters, Ngx.ParameterDLSSHintRenderPresetDLAA, (uint)NGXDLSSHintRenderPreset.K);
            DLSSHelper.SetUI(feature.Parameters, Ngx.ParameterDLSSHintRenderPresetQuality, (uint)NGXDLSSHintRenderPreset.K);
            DLSSHelper.SetUI(feature.Parameters, Ngx.ParameterDLSSHintRenderPresetBalanced, (uint)NGXDLSSHintRenderPreset.K);
            DLSSHelper.SetUI(feature.Parameters, Ngx.ParameterDLSSHintRenderPresetPerformance, (uint)NGXDLSSHintRenderPreset.K);
            DLSSHelper.SetUI(feature.Parameters, Ngx.ParameterDLSSHintRenderPresetUltraPerformance, (uint)NGXDLSSHintRenderPreset.K);
        }

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                CreateDirectX12(commandBuffer.DirectX12(), feature, &createParams);
                break;

            case GraphicsApi.Vulkan:
                CreateVulkan(commandBuffer.Vulkan(), feature, &createParams);
                break;
        }
    }

    public void Create(CommandBuffer commandBuffer, DLSSRayReconstruction feature, RayReconstructionArgs args)
    {
        PollRetiredFeatures(commandBuffer);

        NGXDLSSDCreateParams createParams = new()
        {
            DenoiseMode = NGXDLSSDenoiseMode.DlUnified,
            RoughnessMode = NGXDLSSRoughnessMode.Unpacked,
            UseHWDepth = NGXDLSSDepthType.Hw,
            Width = feature.Desc.RenderWidth,
            Height = feature.Desc.RenderHeight,
            TargetWidth = feature.Desc.OutputWidth,
            TargetHeight = feature.Desc.OutputHeight,
            PerfQualityValue = DLSSHelper.PerfQuality(feature.Desc.RenderWidth, feature.Desc.RenderHeight, feature.Desc.OutputWidth, feature.Desc.OutputHeight),
            FeatureCreateFlags = DLSSHelper.FeatureFlags(args.Color.Desc.Format, args.Exposure, args.Projection)
        };

        RetireFeature(commandBuffer, feature.Handle);
        feature.Handle = default;
        DLSSHelper.ResetParameters(feature.Parameters);

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                CreateDirectX12(commandBuffer.DirectX12(), feature, &createParams);
                break;

            case GraphicsApi.Vulkan:
                CreateVulkan(commandBuffer.Vulkan(), feature, &createParams);
                break;
        }
    }

    public void Create(CommandBuffer commandBuffer, DLSSFrameGeneration feature)
    {
        PollRetiredFeatures(commandBuffer);

        NGXDLSSGCreateParams createParams = new()
        {
            Width = feature.Desc.OutputWidth,
            Height = feature.Desc.OutputHeight,
            NativeBackbufferFormat = Context.GraphicsApi switch
            {
                GraphicsApi.DirectX12 => DLSSFormats.DirectX12(feature.Desc.Format),
                GraphicsApi.Vulkan => (uint)DLSSFormats.Vulkan(feature.Desc.Format),
                _ => default
            },
            RenderWidth = feature.Desc.RenderWidth,
            RenderHeight = feature.Desc.RenderHeight
        };

        RetireFeature(commandBuffer, feature.Handle);
        feature.Handle = default;
        DLSSHelper.ResetParameters(feature.Parameters);
        DLSSHelper.SetUI(feature.Parameters, Ngx.DLSSGParameterMvecJittered, 0);

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                CreateDirectX12(commandBuffer.DirectX12(), feature, &createParams);
                break;

            case GraphicsApi.Vulkan:
                CreateVulkan(commandBuffer.Vulkan(), feature, &createParams);
                break;
        }
    }

    public void Evaluate(CommandBuffer commandBuffer, DLSSSuperResolution feature, SuperResolutionArgs args)
    {
        PollRetiredFeatures(commandBuffer);

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                EvaluateDirectX12(commandBuffer.DirectX12(), feature, args);
                break;

            case GraphicsApi.Vulkan:
                EvaluateVulkan(commandBuffer.Vulkan(), feature, args);
                break;
        }
    }

    public void Evaluate(CommandBuffer commandBuffer, DLSSRayReconstruction feature, RayReconstructionArgs args)
    {
        PollRetiredFeatures(commandBuffer);

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                EvaluateDirectX12(commandBuffer.DirectX12(), feature, args);
                break;

            case GraphicsApi.Vulkan:
                EvaluateVulkan(commandBuffer.Vulkan(), feature, args);
                break;
        }
    }

    public void Evaluate(CommandBuffer commandBuffer, DLSSFrameGeneration feature, FrameGenerationArgs args)
    {
        const float FallbackInfiniteFarScale = 100000.0f;

        PollRetiredFeatures(commandBuffer);

        UpscalingHelper.DecomposePerspective(args.Projection, out float nearPlane, out float farPlane, out float verticalFieldOfViewInRadians, out float aspectRatio, out bool depthReversed, out bool depthInfinite);

        Matrix4x4.Invert(args.View, out Matrix4x4 viewToWorld);
        Matrix4x4.Invert(args.Projection, out Matrix4x4 clipToCameraView);

        Matrix4x4 clipToPrevClip = Matrix4x4.Identity;
        Matrix4x4 prevClipToClip = Matrix4x4.Identity;
        if (!args.Reset && Matrix4x4.Invert(feature.PreviousView, out Matrix4x4 previousViewToWorld) && Matrix4x4.Invert(feature.PreviousProjection, out Matrix4x4 previousClipToView))
        {
            Matrix4x4 relativeViewToWorld = viewToWorld;
            relativeViewToWorld.Translation -= previousViewToWorld.Translation;

            Matrix4x4 relativeWorldToPreviousView = feature.PreviousView;
            relativeWorldToPreviousView.Translation = Vector3.Zero;

            Matrix4x4 viewToPreviousView = relativeViewToWorld * relativeWorldToPreviousView;
            Matrix4x4.Invert(viewToPreviousView, out Matrix4x4 previousViewToView);

            clipToPrevClip = clipToCameraView * viewToPreviousView * feature.PreviousProjection;
            prevClipToClip = previousClipToView * previousViewToView * args.Projection;
        }

        Vector3 forward = new(viewToWorld.M31, viewToWorld.M32, viewToWorld.M33);

        NGXDimensions renderDimensions = new()
        {
            Width = feature.Desc.RenderWidth,
            Height = feature.Desc.RenderHeight
        };

        NGXDimensions outputDimensions = new()
        {
            Width = feature.Desc.OutputWidth,
            Height = feature.Desc.OutputHeight
        };

        NGXDLSSGOptEvalParams optEvalParams = new()
        {
            CameraViewToClip = args.Projection,
            ClipToCameraView = clipToCameraView,
            ClipToLensClip = Matrix4x4.Identity,
            ClipToPrevClip = clipToPrevClip,
            PrevClipToClip = prevClipToClip,
            JitterOffset = -args.JitterOffset,
            MvecScale = new(feature.Desc.RenderWidth, feature.Desc.RenderHeight),
            CameraPos = viewToWorld.Translation,
            CameraUp = Vector3.Normalize(new(viewToWorld.M21, viewToWorld.M22, viewToWorld.M23)),
            CameraRight = Vector3.Normalize(new(viewToWorld.M11, viewToWorld.M12, viewToWorld.M13)),
            CameraFwd = Vector3.Normalize(args.Projection.M34 < 0.0f ? -forward : forward),
            CameraNear = nearPlane,
            CameraFar = depthInfinite ? MathF.Max(nearPlane * FallbackInfiniteFarScale, FallbackInfiniteFarScale) : farPlane,
            CameraFOV = verticalFieldOfViewInRadians,
            CameraAspectRatio = aspectRatio,
            ColorBuffersHDR = args.Color.Desc.Format is PixelFormat.R10G10B10A2UNorm || UpscalingHelper.IsFloat(args.Color.Desc.Format),
            DepthInverted = depthReversed,
            CameraMotionIncluded = true,
            Reset = args.Reset,
            MvecsSubrectSize = renderDimensions,
            DepthSubrectSize = renderDimensions,
            HudLessSubrectSize = args.Hudless is not null ? outputDimensions : default,
            BackbufferSubrectSize = outputDimensions,
            OutputInterpSubrectSize = outputDimensions
        };

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                EvaluateDirectX12(commandBuffer.DirectX12(), feature, args, &optEvalParams);
                break;

            case GraphicsApi.Vulkan:
                EvaluateVulkan(commandBuffer.Vulkan(), feature, args, &optEvalParams);
                break;
        }

        feature.PreviousView = args.View;
        feature.PreviousProjection = args.Projection;
    }

    protected override void Destroy()
    {
        if (!isInitialized)
        {
            return;
        }

        using (Lock.Scope _ = retireLock.EnterScope())
        {
            for (int i = retiredFeatures.Count - 1; i >= 0; i--)
            {
                ReleaseFeature(retiredFeatures[i].Handle);
            }
            retiredFeatures.Clear();
        }

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                ShutdownDirectX12();
                break;

            case GraphicsApi.Vulkan:
                ShutdownVulkan();
                break;
        }
    }

    private void RetireFeature(CommandBuffer commandBuffer, NGXHandle handle)
    {
        if (handle.IsNull)
        {
            return;
        }

        Timeline timeline = commandBuffer.Queue.Timeline;
        TimelineValue afterSubmit = new(timeline, timeline.Signal().Value + 1);

        using Lock.Scope _ = retireLock.EnterScope();

        retiredFeatures.Add(new(commandBuffer, afterSubmit, handle));
    }

    private void PollRetiredFeatures(CommandBuffer currentCommandBuffer)
    {
        using Lock.Scope _ = retireLock.EnterScope();

        for (int i = retiredFeatures.Count - 1; i >= 0; i--)
        {
            RetiredFeature retired = retiredFeatures[i];

            if (retired.CommandBuffer is not null)
            {
                if (retired.TimelineValue.IsCompleted)
                {
                    ReleaseFeature(retired.Handle);
                    retiredFeatures.RemoveAt(i);

                    continue;
                }

                if (retired.CommandBuffer == currentCommandBuffer)
                {
                    continue;
                }

                retired = new(null, retired.CommandBuffer.Queue.Timeline.Signal(), retired.Handle);
                retiredFeatures[i] = retired;
            }

            if (retired.TimelineValue.IsCompleted)
            {
                ReleaseFeature(retired.Handle);
                retiredFeatures.RemoveAt(i);
            }
        }
    }

    private bool TryGetCapabilityParameters(out NGXParameter parameters)
    {
        parameters = default;

        NGXResult result = NGXResult.Fail;
        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                result = Ngx.D3D12.GetCapabilityParameters(out parameters);
                break;

            case GraphicsApi.Vulkan:
                result = Ngx.Vulkan.GetCapabilityParameters(out parameters);
                break;
        }

        return Ngx.NVSDK_NGX_SUCCEED(result);
    }

    private readonly struct RetiredFeature(CommandBuffer? commandBuffer, TimelineValue timelineValue, NGXHandle handle)
    {
        public readonly CommandBuffer? CommandBuffer = commandBuffer;

        public readonly TimelineValue TimelineValue = timelineValue;

        public readonly NGXHandle Handle = handle;
    }
}
