using System.Diagnostics.CodeAnalysis;
using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal unsafe partial class DLSSVendor : Vendor
{
    private readonly NGXParameter parameters;

    public readonly nint Device;

    private DLSSVendor(GraphicsContext context)
    {
        Context = context;

        nint device = context.GetNativeObject(context.GraphicsApi is GraphicsApi.DirectX12 ? NativeObjectType.D3D12Device : NativeObjectType.VulkanDevice);

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

        NGXResult result = context.GraphicsApi is GraphicsApi.DirectX12 ? Ngx.D3D12.InitWithProjectID(projectId, NGXEngineType.Custom, engineVersion, applicationDataPath, device, &featureInfo, NGXVersion.Api) : InitializeVulkan(context, device, projectId, engineVersion, applicationDataPath, &featureInfo);
        result.Success();

        if (!result.IsSuccess())
        {
            Capabilities = new(false, false, false);

            return;
        }

        Device = device;

        fixed (NGXParameter* pointer = &parameters)
        {
            result = context.GraphicsApi is GraphicsApi.DirectX12 ? Ngx.D3D12.GetCapabilityParameters(pointer) : Ngx.Vulkan.GetCapabilityParameters(pointer);
            result.Success();
        }

        if (!result.IsSuccess())
        {
            Capabilities = new(false, false, false);

            return;
        }

        Capabilities = new(IsAvailable(Ngx.ParameterSuperSamplingAvailable), IsAvailable(Ngx.ParameterSuperSamplingDenoisingAvailable), IsAvailable(Ngx.ParameterFrameGenerationAvailable));

        if (context.GraphicsApi is GraphicsApi.Vulkan && (Capabilities.TemporalUpscalerSupported || Capabilities.DenoisingUpscalerSupported || Capabilities.FrameGeneratorSupported) && !CreateVulkanResources())
        {
            Capabilities = new(false, false, false);
        }
    }

    public GraphicsContext Context { get; }

    public override UpscalingCapabilities Capabilities { get; }

    public NGXParameter AllocateParameters()
    {
        NGXParameter parameters = default;

        NGXResult result = Context.GraphicsApi is GraphicsApi.DirectX12 ? Ngx.D3D12.AllocateParameters(&parameters) : Ngx.Vulkan.AllocateParameters(&parameters);
        result.Success();

        if (!result.IsSuccess())
        {
            throw new InvalidOperationException("DLSS feature parameter allocation failed.");
        }

        fixed (byte* name = Ngx.ParameterFreeMemOnReleaseFeature)
        {
            Ngx.Parameter.SetUI(parameters, name, 1);
        }

        return parameters;
    }

    public void DestroyParameters(NGXParameter parameters)
    {
        NGXResult result = Context.GraphicsApi is GraphicsApi.DirectX12 ? Ngx.D3D12.DestroyParameters(parameters) : Ngx.Vulkan.DestroyParameters(parameters);
        result.Success();
    }

    public void ReleaseFeature(NGXHandle handle)
    {
        NGXResult result = Context.GraphicsApi is GraphicsApi.DirectX12 ? Ngx.D3D12.ReleaseFeature(handle) : Ngx.Vulkan.ReleaseFeature(handle);
        result.Success();
    }

    public NGXPerfQualityValue FindPerfQualityValue(NGXFeature feature, uint inputWidth, uint inputHeight, uint outputWidth, uint outputHeight)
    {
        foreach (UpscalingMode mode in Enum.GetValues<UpscalingMode>())
        {
            NGXPerfQualityValue perfQualityValue = DLSSFormats.NGX(mode);
            UpscalingInputSize inputSize = GetOptimalInputSize(feature, outputWidth, outputHeight, perfQualityValue);
            if (inputSize.Width == inputWidth && inputSize.Height == inputHeight)
            {
                return perfQualityValue;
            }
        }

        return DLSSFormats.NGX(UpscalingMode.Native);
    }

    public void Create(DLSSFeature feature, NGXDLSSCreateParams createParams)
    {
        CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();
        nint commandList = GetCommandList(commandBuffer);
        NGXResult result = Context.GraphicsApi is GraphicsApi.DirectX12 ? CreateDirectX12(commandList, feature, &createParams) : CreateVulkan(commandList, feature, &createParams);

        CompleteCreation(commandBuffer, result);
    }

    public void Create(DLSSFeature feature, NGXDLSSDCreateParams createParams)
    {
        CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();
        nint commandList = GetCommandList(commandBuffer);
        NGXResult result = Context.GraphicsApi is GraphicsApi.DirectX12 ? CreateDirectX12(commandList, feature, &createParams) : CreateVulkan(commandList, feature, &createParams);

        CompleteCreation(commandBuffer, result);
    }

    public void Create(DLSSFeature feature, NGXDLSSGCreateParams createParams)
    {
        CommandBuffer commandBuffer = Context.GraphicsQueue.CommandBuffer();
        nint commandList = GetCommandList(commandBuffer);
        NGXResult result = Context.GraphicsApi is GraphicsApi.DirectX12 ? CreateDirectX12(commandList, feature, &createParams) : CreateVulkan(commandList, feature, &createParams);

        CompleteCreation(commandBuffer, result);
    }

    public void Evaluate(CommandBuffer commandBuffer, DLSSFeature feature, TemporalUpscalerArgs args)
    {
        nint commandList = GetCommandList(commandBuffer);
        NGXResult result = Context.GraphicsApi is GraphicsApi.DirectX12 ? EvaluateDirectX12(commandList, feature, args) : EvaluateVulkan(commandList, feature, args);
        result.Success();
    }

    public void Evaluate(CommandBuffer commandBuffer, DLSSFeature feature, DenoisingUpscalerArgs args)
    {
        nint commandList = GetCommandList(commandBuffer);
        NGXResult result = Context.GraphicsApi is GraphicsApi.DirectX12 ? EvaluateDirectX12(commandList, feature, args) : EvaluateVulkan(commandList, feature, args);
        result.Success();
    }

    public void Evaluate(CommandBuffer commandBuffer, DLSSFeature feature, FrameGeneratorArgs args, NGXDLSSGOptEvalParams* optEvalParams)
    {
        nint commandList = GetCommandList(commandBuffer);
        NGXResult result = Context.GraphicsApi is GraphicsApi.DirectX12 ? EvaluateDirectX12(commandList, feature, args, optEvalParams) : EvaluateVulkan(commandList, feature, args, optEvalParams);
        result.Success();
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

    protected override void Destroy()
    {
        if (Device is 0)
        {
            return;
        }

        if (Context.GraphicsApi is GraphicsApi.Vulkan)
        {
            DestroyVulkanResources();
        }

        if (!parameters.IsNull)
        {
            DestroyParameters(parameters);
        }

        NGXResult result = Context.GraphicsApi is GraphicsApi.DirectX12 ? Ngx.D3D12.Shutdown1(Device) : Ngx.Vulkan.Shutdown1(Device);
        result.Success();
    }

    private nint GetCommandList(CommandBuffer commandBuffer)
    {
        return commandBuffer.GetNativeObject(Context.GraphicsApi is GraphicsApi.DirectX12 ? NativeObjectType.D3D12GraphicsCommandList : NativeObjectType.VulkanCommandBuffer);
    }

    private bool IsAvailable(ReadOnlySpan<byte> name)
    {
        fixed (byte* pointer = name)
        {
            int value = 0;
            NGXResult result = Ngx.Parameter.GetI(parameters, pointer, &value);
            result.Success();

            return result.IsSuccess() && value is not 0;
        }
    }

    private UpscalingInputSize GetOptimalInputSize(NGXFeature feature, uint outputWidth, uint outputHeight, NGXPerfQualityValue perfQualityValue)
    {
        uint inputWidth = 0;
        uint inputHeight = 0;
        NGXResult result = feature switch
        {
            NGXFeature.SuperSampling => Ngx.DLSS.GetOptimalSettings(parameters, outputWidth, outputHeight, perfQualityValue, out inputWidth, out inputHeight, out _, out _, out _, out _, out _),
            NGXFeature.RayReconstruction => Ngx.DLSSD.GetOptimalSettings(parameters, outputWidth, outputHeight, perfQualityValue, out inputWidth, out inputHeight, out _, out _, out _, out _, out _),
            _ => default
        };
        result.Success();

        return result.IsSuccess() ? new(inputWidth, inputHeight) : default;
    }

    public static bool TryCreate(GraphicsContext context, [NotNullWhen(true)] out DLSSVendor? vendor)
    {
        vendor = null;

        DLSSVendor candidate = new(context);
        UpscalingCapabilities capabilities = candidate.Capabilities;
        if (!capabilities.TemporalUpscalerSupported && !capabilities.DenoisingUpscalerSupported && !capabilities.FrameGeneratorSupported)
        {
            candidate.Dispose();

            return false;
        }

        vendor = candidate;

        return true;
    }

    private static void CompleteCreation(CommandBuffer commandBuffer, NGXResult result)
    {
        result.Success();

        commandBuffer.Submit().Wait();

        if (!result.IsSuccess())
        {
            throw new InvalidOperationException("DLSS feature creation failed.");
        }
    }
}
