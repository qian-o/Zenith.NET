using System.Numerics;
using NGX.NET;

namespace Zenith.NET.Extensions.DLSS;

internal unsafe partial class DLSSBackend : DisposableObject
{
    private readonly NGXParameter parameters;

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
                InitializeDirectX12(strings.AllocUtf8(typeof(GraphicsContext).GUID.ToString()),
                                    strings.AllocUtf8(typeof(GraphicsContext).Assembly.GetName().Version!.ToString()),
                                    strings.AllocWide(dataPath),
                                    &featureInfo);
                break;

            case GraphicsApi.Vulkan:
                InitializeVulkan(strings.AllocUtf8(typeof(GraphicsContext).GUID.ToString()),
                                 strings.AllocUtf8(typeof(GraphicsContext).Assembly.GetName().Version!.ToString()),
                                 strings.AllocWide(dataPath),
                                 &featureInfo);
                break;
        }

        fixed (NGXParameter* pParameters = &parameters)
        {
            switch (context.GraphicsApi)
            {
                case GraphicsApi.DirectX12:
                    Ngx.D3D12.GetCapabilityParameters(pParameters).Success();
                    break;

                case GraphicsApi.Vulkan:
                    Ngx.Vulkan.GetCapabilityParameters(pParameters).Success();
                    break;
            }
        }

        Capabilities = new(parameters);
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
                Ngx.DLSS.GetOptimalSettings(parameters,
                                            outputWidth,
                                            outputHeight,
                                            DLSSFormats.NGX(mode),
                                            &inputWidth,
                                            &inputHeight,
                                            &maxInputWidth,
                                            &maxInputHeight,
                                            &minInputWidth,
                                            &minInputHeight,
                                            &sharpness).Success();
                break;

            case NGXFeature.RayReconstruction:
                Ngx.DLSSD.GetOptimalSettings(parameters,
                                             outputWidth,
                                             outputHeight,
                                             DLSSFormats.NGX(mode),
                                             &inputWidth,
                                             &inputHeight,
                                             &maxInputWidth,
                                             &maxInputHeight,
                                             &minInputWidth,
                                             &minInputHeight,
                                             &sharpness).Success();
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

        fixed (byte* name = Ngx.ParameterFreeMemOnReleaseFeature)
        {
            Ngx.Parameter.SetUI(parameters, name, 1);
        }

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
            RoughnessMode = NGXDLSSRoughnessMode.Unpacked,
            UseHWDepth = NGXDLSSDepthType.Hw,
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
            RenderHeight = feature.Desc.InputHeight
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
        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                EvaluateDirectX12(commandBuffer.GetNativeObject(NativeObjectType.D3D12GraphicsCommandList), feature, args);
                break;

            case GraphicsApi.Vulkan:
                EvaluateVulkan(commandBuffer.GetNativeObject(NativeObjectType.VulkanCommandBuffer), feature, args);
                break;
        }
    }

    public void Evaluate(CommandBuffer commandBuffer, DLSSRayReconstruction feature, DLSSRayReconstructionArgs args)
    {
        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                EvaluateDirectX12(commandBuffer.GetNativeObject(NativeObjectType.D3D12GraphicsCommandList), feature, args);
                break;

            case GraphicsApi.Vulkan:
                EvaluateVulkan(commandBuffer.GetNativeObject(NativeObjectType.VulkanCommandBuffer), feature, args);
                break;
        }
    }

    public void Evaluate(CommandBuffer commandBuffer, DLSSFrameGeneration feature, DLSSFrameGenerationArgs args)
    {
        NGXDLSSGOptEvalParams optEvalParams = new()
        {
            CameraViewToClip = args.ViewToClip,
            ClipToCameraView = args.ClipToView,
            ClipToLensClip = Matrix4x4.Identity,
            ClipToPrevClip = args.ClipToPrevClip,
            PrevClipToClip = args.PrevClipToClip,
            JitterOffset = args.JitterOffset,
            MvecScale = args.MotionVectorScale,
            CameraPos = args.CameraPosition,
            CameraUp = args.CameraUp,
            CameraRight = args.CameraRight,
            CameraFwd = args.CameraForward,
            CameraNear = args.CameraNear,
            CameraFar = args.CameraFar,
            CameraFOV = args.CameraFovAngleVer,
            CameraAspectRatio = args.CameraAspectRatio,
            ColorBuffersHDR = args.IsHdrDisplay,
            DepthInverted = args.IsDepthReversed,
            CameraMotionIncluded = true,
            Reset = args.Reset,
            OrthoProjection = args.IsOrthographic,
            MvecsSubrectSize = new()
            {
                Width = args.MotionVectors.Desc.Width,
                Height = args.MotionVectors.Desc.Height
            },
            DepthSubrectSize = new()
            {
                Width = args.Depth.Desc.Width,
                Height = args.Depth.Desc.Height
            },
            BackbufferSubrectSize = new()
            {
                Width = args.Color.Desc.Width,
                Height = args.Color.Desc.Height
            },
            OutputInterpSubrectSize = new()
            {
                Width = args.Output.Desc.Width,
                Height = args.Output.Desc.Height
            }
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

    protected override void Destroy()
    {
        DestroyParameters(parameters);

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
}
