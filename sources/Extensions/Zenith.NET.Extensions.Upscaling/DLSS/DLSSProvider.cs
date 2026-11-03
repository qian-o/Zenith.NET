using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal unsafe partial class DLSSProvider(GraphicsContext context) : UpscalingProvider(context)
{
    private readonly NGXParameter parameters;

    public readonly nint Device = context.GraphicsApi switch
    {
        GraphicsApi.DirectX12 => context.GetNativeObject(NativeObjectType.D3D12Device),
        GraphicsApi.Vulkan => context.GetNativeObject(NativeObjectType.VulkanDevice),
        _ => default
    };

    private bool isInitialized;

    public override bool TemporalUpscalerSupported => IsAvailable(Ngx.ParameterSuperSamplingAvailable);

    public override bool DenoisingUpscalerSupported => IsAvailable(Ngx.ParameterSuperSamplingDenoisingAvailable);

    public override bool FrameGeneratorSupported => IsAvailable(Ngx.ParameterFrameGenerationAvailable);

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

    public NGXPerfQualityValue FindPerfQualityValue(NGXFeature feature, uint inputWidth, uint inputHeight, uint outputWidth, uint outputHeight)
    {
        foreach (UpscalingMode mode in Enum.GetValues<UpscalingMode>())
        {
            UpscalingInputSize inputSize = GetOptimalInputSize(feature, outputWidth, outputHeight, DLSSFormats.NGX(mode));
            if (inputSize.Width == inputWidth && inputSize.Height == inputHeight)
            {
                return DLSSFormats.NGX(mode);
            }
        }

        return DLSSFormats.NGX(UpscalingMode.Native);
    }

    public void Create(DLSSFeature feature, NGXDLSSCreateParams createParams)
    {
        CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                CreateDirectX12(commandBuffer.GetNativeObject(NativeObjectType.D3D12GraphicsCommandList), feature, &createParams);
                break;

            case GraphicsApi.Vulkan:
                CreateVulkan(commandBuffer.GetNativeObject(NativeObjectType.VulkanCommandBuffer), feature, &createParams);
                break;
        }

        commandBuffer.Submit().Wait();
    }

    public void Create(DLSSFeature feature, NGXDLSSDCreateParams createParams)
    {
        CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                CreateDirectX12(commandBuffer.GetNativeObject(NativeObjectType.D3D12GraphicsCommandList), feature, &createParams);
                break;

            case GraphicsApi.Vulkan:
                CreateVulkan(commandBuffer.GetNativeObject(NativeObjectType.VulkanCommandBuffer), feature, &createParams);
                break;
        }

        commandBuffer.Submit().Wait();
    }

    public void Create(DLSSFeature feature, NGXDLSSGCreateParams createParams)
    {
        CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                CreateDirectX12(commandBuffer.GetNativeObject(NativeObjectType.D3D12GraphicsCommandList), feature, &createParams);
                break;

            case GraphicsApi.Vulkan:
                CreateVulkan(commandBuffer.GetNativeObject(NativeObjectType.VulkanCommandBuffer), feature, &createParams);
                break;
        }

        commandBuffer.Submit().Wait();
    }

    public void Evaluate(CommandBuffer commandBuffer, DLSSFeature feature, TemporalUpscalerArgs args)
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

    public void Evaluate(CommandBuffer commandBuffer, DLSSFeature feature, DenoisingUpscalerArgs args)
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

    public void Evaluate(CommandBuffer commandBuffer, DLSSFeature feature, FrameGeneratorArgs args, NGXDLSSGOptEvalParams* optEvalParams)
    {
        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                EvaluateDirectX12(commandBuffer.GetNativeObject(NativeObjectType.D3D12GraphicsCommandList), feature, args, optEvalParams);
                break;

            case GraphicsApi.Vulkan:
                EvaluateVulkan(commandBuffer.GetNativeObject(NativeObjectType.VulkanCommandBuffer), feature, args, optEvalParams);
                break;
        }
    }

    public bool IsAvailable(ReadOnlySpan<byte> name)
    {
        if (!isInitialized)
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

    public override UpscalingInputSize GetTemporalUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        return GetOptimalInputSize(NGXFeature.SuperSampling, outputWidth, outputHeight, DLSSFormats.NGX(mode));
    }

    public override UpscalingInputSize GetDenoisingUpscalerInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        return GetOptimalInputSize(NGXFeature.RayReconstruction, outputWidth, outputHeight, DLSSFormats.NGX(mode));
    }

    public override TemporalUpscaler CreateTemporalUpscaler(TemporalUpscalerDesc desc)
    {
        return new DLSSTemporalUpscaler(this, desc);
    }

    public override DenoisingUpscaler CreateDenoisingUpscaler(DenoisingUpscalerDesc desc)
    {
        return new DLSSDenoisingUpscaler(this, desc);
    }

    public override FrameGenerator CreateFrameGenerator(FrameGeneratorDesc desc)
    {
        return new DLSSFrameGenerator(this, desc);
    }

    protected override void Initialize()
    {
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

        byte* projectId = strings.AllocUtf8(typeof(GraphicsContext).GUID.ToString());
        byte* engineVersion = strings.AllocUtf8(typeof(GraphicsContext).Assembly.GetName().Version!.ToString());
        void* applicationDataPath = strings.AllocWide(Path.GetTempPath());

        NGXResult result = Context.GraphicsApi switch
        {
            GraphicsApi.DirectX12 => InitializeDirectX12(projectId, engineVersion, applicationDataPath, &featureInfo),
            GraphicsApi.Vulkan => InitializeVulkan(projectId, engineVersion, applicationDataPath, &featureInfo),
            _ => default
        };
        result.Success();

        isInitialized = result.IsSuccess();

        if (isInitialized)
        {
            fixed (NGXParameter* pParameters = &parameters)
            {
                switch (Context.GraphicsApi)
                {
                    case GraphicsApi.DirectX12:
                        Ngx.D3D12.GetCapabilityParameters(pParameters).Success();
                        break;

                    case GraphicsApi.Vulkan:
                        Ngx.Vulkan.GetCapabilityParameters(pParameters).Success();
                        break;
                }
            }
        }
    }

    protected override void Destroy()
    {
        if (!isInitialized)
        {
            return;
        }

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

    private UpscalingInputSize GetOptimalInputSize(NGXFeature feature, uint outputWidth, uint outputHeight, NGXPerfQualityValue perfQualityValue)
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
                                            perfQualityValue,
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
                                             perfQualityValue,
                                             &inputWidth,
                                             &inputHeight,
                                             &maxInputWidth,
                                             &maxInputHeight,
                                             &minInputWidth,
                                             &minInputHeight,
                                             &sharpness).Success();
                break;
        }

        return new(inputWidth, inputHeight);
    }
}
