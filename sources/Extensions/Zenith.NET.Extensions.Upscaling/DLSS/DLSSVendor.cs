using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal unsafe partial class DLSSVendor : Vendor
{
    private readonly NGXParameter parameters;

    public readonly nint Device;

    public DLSSVendor(GraphicsContext context)
    {
        Context = context;

        nint device = context.GetNativeObject(context.GraphicsApi is GraphicsApi.DirectX12 ? NativeObjectType.D3D12Device : NativeObjectType.VulkanDevice);

        using ZenithMarshal.Scope scope = new();

        void* path = (void*)ZenithMarshal.StringToPointer(scope, Ngx.RuntimeDirectory, StringEncoding.UTF16);
        NGXFeatureCommonInfo featureInfo = new()
        {
            PathListInfo = new()
            {
                Path = &path,
                Length = 1
            }
        };

        byte* projectId = (byte*)ZenithMarshal.StringToPointer(scope, typeof(GraphicsContext).GUID.ToString(), StringEncoding.UTF8);
        byte* engineVersion = (byte*)ZenithMarshal.StringToPointer(scope, typeof(GraphicsContext).Assembly.GetName().Version!.ToString(), StringEncoding.UTF8);
        void* applicationDataPath = (void*)ZenithMarshal.StringToPointer(scope, Path.GetTempPath(), StringEncoding.UTF16);

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

        if (context.GraphicsApi is GraphicsApi.Vulkan && (Capabilities.SuperResolutionSupported || Capabilities.RayReconstructionSupported || Capabilities.FrameGenerationSupported) && !CreateVulkanResources())
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

    public void Evaluate(CommandBuffer commandBuffer, DLSSFeature feature, SuperResolutionArgs args)
    {
        if (Context.GraphicsApi is GraphicsApi.DirectX12)
        {
            commandBuffer.Transition(args.Color, default, TextureLayout.Sampled, TextureLayout.Common);
            commandBuffer.Transition(args.Depth, default, TextureLayout.Sampled, TextureLayout.Common);
            commandBuffer.Transition(args.MotionVectors, default, TextureLayout.Sampled, TextureLayout.Common);
            commandBuffer.Transition(args.Output, default, TextureLayout.Storage, TextureLayout.Common);

            if (args.Exposure is not null)
            {
                commandBuffer.Transition(args.Exposure, default, TextureLayout.Sampled, TextureLayout.Common);
            }

            if (args.ReactiveMask is not null)
            {
                commandBuffer.Transition(args.ReactiveMask, default, TextureLayout.Sampled, TextureLayout.Common);
            }
        }

        nint commandList = GetCommandList(commandBuffer);
        NGXResult result = Context.GraphicsApi is GraphicsApi.DirectX12 ? EvaluateDirectX12(commandList, feature, args) : EvaluateVulkan(commandList, feature, args);
        result.Success();

        if (Context.GraphicsApi is GraphicsApi.DirectX12)
        {
            commandBuffer.Transition(args.Color, default, TextureLayout.Common, TextureLayout.Sampled);
            commandBuffer.Transition(args.Depth, default, TextureLayout.Common, TextureLayout.Sampled);
            commandBuffer.Transition(args.MotionVectors, default, TextureLayout.Common, TextureLayout.Sampled);
            commandBuffer.Transition(args.Output, default, TextureLayout.Common, TextureLayout.Storage);

            if (args.Exposure is not null)
            {
                commandBuffer.Transition(args.Exposure, default, TextureLayout.Common, TextureLayout.Sampled);
            }

            if (args.ReactiveMask is not null)
            {
                commandBuffer.Transition(args.ReactiveMask, default, TextureLayout.Common, TextureLayout.Sampled);
            }
        }
    }

    public void Evaluate(CommandBuffer commandBuffer, DLSSFeature feature, RayReconstructionArgs args)
    {
        if (Context.GraphicsApi is GraphicsApi.DirectX12)
        {
            commandBuffer.Transition(args.Color, default, TextureLayout.Sampled, TextureLayout.Common);
            commandBuffer.Transition(args.Depth, default, TextureLayout.Sampled, TextureLayout.Common);
            commandBuffer.Transition(args.MotionVectors, default, TextureLayout.Sampled, TextureLayout.Common);
            commandBuffer.Transition(args.DiffuseAlbedo, default, TextureLayout.Sampled, TextureLayout.Common);
            commandBuffer.Transition(args.SpecularAlbedo, default, TextureLayout.Sampled, TextureLayout.Common);
            commandBuffer.Transition(args.Normals, default, TextureLayout.Sampled, TextureLayout.Common);
            commandBuffer.Transition(args.Roughness, default, TextureLayout.Sampled, TextureLayout.Common);
            commandBuffer.Transition(args.SpecularHitDistance, default, TextureLayout.Sampled, TextureLayout.Common);
            commandBuffer.Transition(args.Output, default, TextureLayout.Storage, TextureLayout.Common);

            if (args.Exposure is not null)
            {
                commandBuffer.Transition(args.Exposure, default, TextureLayout.Sampled, TextureLayout.Common);
            }

            if (args.ReactiveMask is not null)
            {
                commandBuffer.Transition(args.ReactiveMask, default, TextureLayout.Sampled, TextureLayout.Common);
            }

            if (args.TransparencyOverlay is not null)
            {
                commandBuffer.Transition(args.TransparencyOverlay, default, TextureLayout.Sampled, TextureLayout.Common);
            }
        }

        nint commandList = GetCommandList(commandBuffer);
        NGXResult result = Context.GraphicsApi is GraphicsApi.DirectX12 ? EvaluateDirectX12(commandList, feature, args) : EvaluateVulkan(commandList, feature, args);
        result.Success();

        if (Context.GraphicsApi is GraphicsApi.DirectX12)
        {
            commandBuffer.Transition(args.Color, default, TextureLayout.Common, TextureLayout.Sampled);
            commandBuffer.Transition(args.Depth, default, TextureLayout.Common, TextureLayout.Sampled);
            commandBuffer.Transition(args.MotionVectors, default, TextureLayout.Common, TextureLayout.Sampled);
            commandBuffer.Transition(args.DiffuseAlbedo, default, TextureLayout.Common, TextureLayout.Sampled);
            commandBuffer.Transition(args.SpecularAlbedo, default, TextureLayout.Common, TextureLayout.Sampled);
            commandBuffer.Transition(args.Normals, default, TextureLayout.Common, TextureLayout.Sampled);
            commandBuffer.Transition(args.Roughness, default, TextureLayout.Common, TextureLayout.Sampled);
            commandBuffer.Transition(args.SpecularHitDistance, default, TextureLayout.Common, TextureLayout.Sampled);
            commandBuffer.Transition(args.Output, default, TextureLayout.Common, TextureLayout.Storage);

            if (args.Exposure is not null)
            {
                commandBuffer.Transition(args.Exposure, default, TextureLayout.Common, TextureLayout.Sampled);
            }

            if (args.ReactiveMask is not null)
            {
                commandBuffer.Transition(args.ReactiveMask, default, TextureLayout.Common, TextureLayout.Sampled);
            }

            if (args.TransparencyOverlay is not null)
            {
                commandBuffer.Transition(args.TransparencyOverlay, default, TextureLayout.Common, TextureLayout.Sampled);
            }
        }
    }

    public void Evaluate(CommandBuffer commandBuffer, DLSSFeature feature, FrameGenerationArgs args, NGXDLSSGOptEvalParams* optEvalParams)
    {
        if (Context.GraphicsApi is GraphicsApi.DirectX12)
        {
            commandBuffer.Transition(args.Color, default, TextureLayout.Sampled, TextureLayout.Common);
            commandBuffer.Transition(args.Depth, default, TextureLayout.Sampled, TextureLayout.Common);
            commandBuffer.Transition(args.MotionVectors, default, TextureLayout.Sampled, TextureLayout.Common);
            commandBuffer.Transition(args.Output, default, TextureLayout.Storage, TextureLayout.Common);

            if (args.Hudless is not null)
            {
                commandBuffer.Transition(args.Hudless, default, TextureLayout.Sampled, TextureLayout.Common);
            }
        }

        nint commandList = GetCommandList(commandBuffer);
        NGXResult result = Context.GraphicsApi is GraphicsApi.DirectX12 ? EvaluateDirectX12(commandList, feature, args, optEvalParams) : EvaluateVulkan(commandList, feature, args, optEvalParams);
        result.Success();

        if (Context.GraphicsApi is GraphicsApi.DirectX12)
        {
            commandBuffer.Transition(args.Color, default, TextureLayout.Common, TextureLayout.Sampled);
            commandBuffer.Transition(args.Depth, default, TextureLayout.Common, TextureLayout.Sampled);
            commandBuffer.Transition(args.MotionVectors, default, TextureLayout.Common, TextureLayout.Sampled);
            commandBuffer.Transition(args.Output, default, TextureLayout.Common, TextureLayout.Storage);

            if (args.Hudless is not null)
            {
                commandBuffer.Transition(args.Hudless, default, TextureLayout.Common, TextureLayout.Sampled);
            }
        }
    }

    public override UpscalingInputSize GetSuperResolutionInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        return GetOptimalInputSize(NGXFeature.SuperSampling, outputWidth, outputHeight, DLSSFormats.NGX(mode));
    }

    public override UpscalingInputSize GetRayReconstructionInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        return GetOptimalInputSize(NGXFeature.RayReconstruction, outputWidth, outputHeight, DLSSFormats.NGX(mode));
    }

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

    private static void CompleteCreation(CommandBuffer commandBuffer, NGXResult result)
    {
        result.Success();

        commandBuffer.Submit().Wait();
    }
}
