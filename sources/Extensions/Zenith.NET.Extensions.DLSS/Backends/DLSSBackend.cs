using System.Numerics;
using NGX.NET;

namespace Zenith.NET.Extensions.DLSS;

internal unsafe partial class DLSSBackend : DisposableObject
{
    private const string ProjectId = "b5b58279-25f8-4d7f-a9d1-e943a85c277c";
    private const string EngineVersion = "1.0";

    private readonly Dictionary<Texture, DLSSBinding> bindings = [];
    private readonly NGXParameter capabilityParameters;

    public DLSSBackend(GraphicsContext context, string dataPath)
    {
        Context = context;

        Device = context.GraphicsApi switch
        {
            GraphicsApi.DirectX12 => context.GetNativeObject(NativeObjectType.D3D12Device),
            GraphicsApi.Vulkan => context.GetNativeObject(NativeObjectType.VulkanDevice),
            _ => default
        };

        using NGXStrings strings = new();

        byte* projectId = strings.AllocUtf8(ProjectId);
        byte* engineVersion = strings.AllocUtf8(EngineVersion);
        void* applicationDataPath = strings.AllocWide(dataPath);
        void* path = strings.AllocWide(Ngx.RuntimeDirectory);

        NGXFeatureCommonInfo featureInfo = new()
        {
            PathListInfo = new()
            {
                Path = &path,
                Length = 1
            }
        };

        switch (context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                InitializeDirectX12(projectId, engineVersion, applicationDataPath, &featureInfo);
                break;

            case GraphicsApi.Vulkan:
                InitializeVulkan(projectId, engineVersion, applicationDataPath, &featureInfo);
                break;
        }

        fixed (NGXParameter* parameters = &capabilityParameters)
        {
            switch (context.GraphicsApi)
            {
                case GraphicsApi.DirectX12:
                    Ngx.D3D12.GetCapabilityParameters(parameters).Success();
                    break;

                case GraphicsApi.Vulkan:
                    Ngx.Vulkan.GetCapabilityParameters(parameters).Success();
                    break;
            }
        }

        Capabilities = new(capabilityParameters);
    }

    public GraphicsContext Context { get; }

    public nint Device { get; }

    public DLSSCapabilities Capabilities { get; }

    public DLSSOptimalSettings GetOptimalSettings(NGXFeature feature, uint outputWidth, uint outputHeight, DLSSMode mode)
    {
        uint inputWidth = 0;
        uint inputHeight = 0;
        uint maxInputWidth = 0;
        uint maxInputHeight = 0;
        uint minInputWidth = 0;
        uint minInputHeight = 0;
        float sharpness = 0.0f;

        switch (feature)
        {
            case NGXFeature.SuperSampling:
                Ngx.DLSS.GetOptimalSettings(capabilityParameters, outputWidth, outputHeight, DLSSFormats.NGX(mode), &inputWidth, &inputHeight, &maxInputWidth, &maxInputHeight, &minInputWidth, &minInputHeight, &sharpness).Success();
                break;

            case NGXFeature.RayReconstruction:
                Ngx.DLSSD.GetOptimalSettings(capabilityParameters, outputWidth, outputHeight, DLSSFormats.NGX(mode), &inputWidth, &inputHeight, &maxInputWidth, &maxInputHeight, &minInputWidth, &minInputHeight, &sharpness).Success();
                break;
        }

        return new(inputWidth, inputHeight, minInputWidth, minInputHeight, maxInputWidth, maxInputHeight);
    }

    public NGXParameter AllocateParameters()
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

        SetUI(parameters, Ngx.ParameterFreeMemOnReleaseFeature, 1);

        return parameters;
    }

    public void DestroyParameters(NGXParameter parameters)
    {
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

    public void Create(CommandBuffer commandBuffer, DLSSSuperResolution feature)
    {
        NGXDLSSCreateParams createParams = new()
        {
            Feature = new()
            {
                Width = feature.Desc.InputWidth,
                Height = feature.Desc.InputHeight,
                TargetWidth = feature.Desc.OutputWidth,
                TargetHeight = feature.Desc.OutputHeight,
                PerfQualityValue = DLSSFormats.NGX(feature.Desc.Mode)
            },
            FeatureCreateFlags = (int)DLSSFormats.NGX(feature.Desc.Flags)
        };

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                CreateDirectX12(commandBuffer.GetNativeObject(NativeObjectType.D3D12GraphicsCommandList), feature, &createParams);
                break;

            case GraphicsApi.Vulkan:
                CreateVulkan(commandBuffer.GetNativeObject(NativeObjectType.VulkanCommandBuffer), feature, &createParams);
                break;
        }
    }

    public void Create(CommandBuffer commandBuffer, DLSSRayReconstruction feature)
    {
        NGXDLSSDCreateParams createParams = new()
        {
            DenoiseMode = NGXDLSSDenoiseMode.DlUnified,
            RoughnessMode = DLSSFormats.NGX(feature.Desc.RoughnessMode),
            UseHWDepth = DLSSFormats.NGX(feature.Desc.DepthType),
            Width = feature.Desc.InputWidth,
            Height = feature.Desc.InputHeight,
            TargetWidth = feature.Desc.OutputWidth,
            TargetHeight = feature.Desc.OutputHeight,
            PerfQualityValue = DLSSFormats.NGX(feature.Desc.Mode),
            FeatureCreateFlags = (int)DLSSFormats.NGX(feature.Desc.Flags)
        };

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                CreateDirectX12(commandBuffer.GetNativeObject(NativeObjectType.D3D12GraphicsCommandList), feature, &createParams);
                break;

            case GraphicsApi.Vulkan:
                CreateVulkan(commandBuffer.GetNativeObject(NativeObjectType.VulkanCommandBuffer), feature, &createParams);
                break;
        }
    }

    public void Create(CommandBuffer commandBuffer, DLSSFrameGeneration feature)
    {
        SetUI(feature.Parameters, Ngx.DLSSGParameterMvecJittered, feature.Desc.Flags.HasFlag(DLSSFeatureFlags.MotionVectorJittered) ? 1u : 0u);
        SetUI(feature.Parameters, Ngx.DLSSGParameterUserInterfaceRecompositionEnabled, feature.Desc.Flags.HasFlag(DLSSFeatureFlags.UIRecomposition) ? 1u : 0u);

        NGXDLSSGCreateParams createParams = new()
        {
            Width = feature.Desc.OutputWidth,
            Height = feature.Desc.OutputHeight,
            NativeBackbufferFormat = Context.GraphicsApi switch
            {
                GraphicsApi.DirectX12 => DLSSFormats.DirectX12(feature.Desc.Format),
                GraphicsApi.Vulkan => DLSSFormats.Vulkan(feature.Desc.Format).Format,
                _ => default
            },
            RenderWidth = feature.Desc.InputWidth,
            RenderHeight = feature.Desc.InputHeight,
            DynamicResolutionScaling = feature.Desc.Flags.HasFlag(DLSSFeatureFlags.DynamicResolution)
        };

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                CreateDirectX12(commandBuffer.GetNativeObject(NativeObjectType.D3D12GraphicsCommandList), feature, &createParams);
                break;

            case GraphicsApi.Vulkan:
                CreateVulkan(commandBuffer.GetNativeObject(NativeObjectType.VulkanCommandBuffer), feature, &createParams);
                break;
        }
    }

    public void Evaluate(CommandBuffer commandBuffer, DLSSSuperResolution feature, DLSSSuperResolutionArgs args)
    {
        NGXDimensions renderSubrectDimensions = new()
        {
            Width = args.InputContentWidth,
            Height = args.InputContentHeight
        };

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                EvaluateDirectX12(commandBuffer.GetNativeObject(NativeObjectType.D3D12GraphicsCommandList), feature, args, renderSubrectDimensions);
                break;

            case GraphicsApi.Vulkan:
                EvaluateVulkan(commandBuffer.GetNativeObject(NativeObjectType.VulkanCommandBuffer), feature, args, renderSubrectDimensions);
                break;
        }
    }

    public void Evaluate(CommandBuffer commandBuffer, DLSSRayReconstruction feature, DLSSRayReconstructionArgs args)
    {
        NGXDimensions renderSubrectDimensions = new()
        {
            Width = feature.Desc.InputWidth,
            Height = feature.Desc.InputHeight
        };

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                EvaluateDirectX12(commandBuffer.GetNativeObject(NativeObjectType.D3D12GraphicsCommandList), feature, args, renderSubrectDimensions);
                break;

            case GraphicsApi.Vulkan:
                EvaluateVulkan(commandBuffer.GetNativeObject(NativeObjectType.VulkanCommandBuffer), feature, args, renderSubrectDimensions);
                break;
        }
    }

    public void Evaluate(CommandBuffer commandBuffer, DLSSFrameGeneration feature, DLSSFrameGenerationArgs args)
    {
        Matrix4x4.Invert(args.ViewToClip, out Matrix4x4 clipToView);
        Matrix4x4.Invert(args.ClipToPrevClip, out Matrix4x4 prevClipToClip);

        NGXDimensions inputContentSize = new()
        {
            Width = args.InputContentWidth,
            Height = args.InputContentHeight
        };

        NGXDLSSGOptEvalParams optEvalParams = new()
        {
            CameraViewToClip = args.ViewToClip,
            ClipToCameraView = clipToView,
            ClipToLensClip = Matrix4x4.Identity,
            ClipToPrevClip = args.ClipToPrevClip,
            PrevClipToClip = prevClipToClip,
            JitterOffset = new(args.JitterOffsetX, args.JitterOffsetY),
            MvecScale = new(args.MotionVectorScaleX, args.MotionVectorScaleY),
            CameraPinholeOffset = new(args.CameraPinholeOffsetX, args.CameraPinholeOffsetY),
            CameraPos = args.CameraPosition,
            CameraUp = args.CameraUp,
            CameraRight = args.CameraRight,
            CameraFwd = args.CameraForward,
            CameraNear = args.CameraNear,
            CameraFar = args.CameraFar,
            CameraFOV = args.CameraFovAngleVer,
            CameraAspectRatio = args.CameraAspectRatio,
            ColorBuffersHDR = feature.Desc.Flags.HasFlag(DLSSFeatureFlags.Hdr),
            DepthInverted = feature.Desc.Flags.HasFlag(DLSSFeatureFlags.DepthReversed),
            CameraMotionIncluded = true,
            Reset = args.Reset,
            OrthoProjection = args.Orthographic,
            MvecsSubrectSize = inputContentSize,
            DepthSubrectSize = inputContentSize,
            HudLessSubrectSize = args.HudlessColor?.Size ?? default,
            UiSubrectSize = args.UI?.Size ?? default,
            UiAlphaSubrectSize = args.UIAlpha?.Size ?? default,
            BidirectionalDistFieldSubrectSize = args.DistortionField?.Size ?? default,
            BackbufferSubrectSize = args.Color.Size,
            OutputInterpSubrectSize = args.Output.Size
        };

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                EvaluateDirectX12(commandBuffer.GetNativeObject(NativeObjectType.D3D12GraphicsCommandList), feature, args, &optEvalParams);
                break;

            case GraphicsApi.Vulkan:
                EvaluateVulkan(commandBuffer.GetNativeObject(NativeObjectType.VulkanCommandBuffer), feature, args, &optEvalParams);
                break;
        }
    }

    public DLSSBinding Binding(Texture texture)
    {
        if (!bindings.TryGetValue(texture, out DLSSBinding binding))
        {
            NGXDimensions size = new()
            {
                Width = texture.Desc.Width,
                Height = texture.Desc.Height
            };

            bindings[texture] = binding = Context.GraphicsApi switch
            {
                GraphicsApi.DirectX12 => CreateDirectX12Binding(texture, size),
                GraphicsApi.Vulkan => CreateVulkanBinding(texture, size),
                _ => default
            };

            texture.Disposing += (sender, _) =>
            {
                if (bindings.Remove((Texture)sender!, out DLSSBinding disposedBinding) && Context.GraphicsApi is GraphicsApi.Vulkan)
                {
                    DestroyVulkanBinding(disposedBinding);
                }
            };
        }

        return binding;
    }

    protected override void Destroy()
    {
        if (Context.GraphicsApi is GraphicsApi.Vulkan)
        {
            foreach (DLSSBinding binding in bindings.Values)
            {
                DestroyVulkanBinding(binding);
            }
        }

        DestroyParameters(capabilityParameters);

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                ShutdownDirectX12();
                break;

            case GraphicsApi.Vulkan:
                ShutdownVulkan();
                break;
        }

        bindings.Clear();
    }

    private void SetUI(NGXParameter parameters, ReadOnlySpan<byte> name, uint value)
    {
        fixed (byte* pointer = name)
        {
            Ngx.Parameter.SetUI(parameters, pointer, value);
        }
    }
}
