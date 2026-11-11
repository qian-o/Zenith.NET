using System.Runtime.InteropServices;
using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal unsafe class DLSSVendor : Vendor
{
    private const uint ImageViewCreateInfoType = 15;
    private const uint PipelineLayoutCreateInfoType = 30;
    private const uint DescriptorSetLayoutCreateInfoType = 32;
    private const uint DescriptorPoolCreateInfoType = 33;
    private const uint DescriptorSetAllocateInfoType = 34;
    private const uint ImageViewType2D = 1;
    private const uint ComputeBindPoint = 1;

    private readonly NGXParameter parameters;
    private readonly nint descriptorSetLayout;
    private readonly nint pipelineLayout;
    private readonly nint descriptorPool;
    private readonly nint descriptorSet;
    private readonly Dictionary<nint, NGXResourceVK> resources = [];
    private readonly nint device;

    private delegate* unmanaged<nint, void*, void*, nint*, int> vkCreateImageView;
    private delegate* unmanaged<nint, nint, void*, void> vkDestroyImageView;
    private delegate* unmanaged<nint, nint, void*, void> vkDestroyDescriptorSetLayout;
    private delegate* unmanaged<nint, nint, void*, void> vkDestroyPipelineLayout;
    private delegate* unmanaged<nint, nint, void*, void> vkDestroyDescriptorPool;
    private delegate* unmanaged<nint, uint, nint, uint, uint, nint*, uint, uint*, void> vkCmdBindDescriptorSets;

    public DLSSVendor(GraphicsContext context) : base(context)
    {
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

        this.device = device;

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

    public NGXPerfQualityValue FindPerfQualityValue(NGXFeature feature, uint renderWidth, uint renderHeight, uint outputWidth, uint outputHeight)
    {
        foreach (UpscalingQuality quality in Enum.GetValues<UpscalingQuality>())
        {
            NGXPerfQualityValue perfQualityValue = DLSSFormats.NGX(quality);
            if (GetOptimalRenderSize(feature, outputWidth, outputHeight, perfQualityValue) == (renderWidth, renderHeight))
            {
                return perfQualityValue;
            }
        }

        return DLSSFormats.NGX(UpscalingQuality.Native);
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
        commandBuffer.Transition(args.Color, default, TextureLayout.Sampled, TextureLayout.Common);
        commandBuffer.Transition(args.Depth, default, TextureLayout.Sampled, TextureLayout.Common);
        commandBuffer.Transition(args.MotionVectors, default, TextureLayout.Sampled, TextureLayout.Common);

        if (args.Exposure is not null)
        {
            commandBuffer.Transition(args.Exposure, default, TextureLayout.Sampled, TextureLayout.Common);
        }

        if (args.ReactiveMask is not null)
        {
            commandBuffer.Transition(args.ReactiveMask, default, TextureLayout.Sampled, TextureLayout.Common);
        }

        commandBuffer.BeginExternalCommands();

        nint commandList = GetCommandList(commandBuffer);
        NGXResult result = Context.GraphicsApi is GraphicsApi.DirectX12 ? EvaluateDirectX12(commandList, feature, args) : EvaluateVulkan(commandList, feature, args);
        result.Success();

        commandBuffer.EndExternalCommands();

        commandBuffer.Transition(args.Color, default, TextureLayout.Common, TextureLayout.Sampled);
        commandBuffer.Transition(args.Depth, default, TextureLayout.Common, TextureLayout.Sampled);
        commandBuffer.Transition(args.MotionVectors, default, TextureLayout.Common, TextureLayout.Sampled);

        if (args.Exposure is not null)
        {
            commandBuffer.Transition(args.Exposure, default, TextureLayout.Common, TextureLayout.Sampled);
        }

        if (args.ReactiveMask is not null)
        {
            commandBuffer.Transition(args.ReactiveMask, default, TextureLayout.Common, TextureLayout.Sampled);
        }
    }

    public void Evaluate(CommandBuffer commandBuffer, DLSSFeature feature, RayReconstructionArgs args)
    {
        commandBuffer.Transition(args.Color, default, TextureLayout.Sampled, TextureLayout.Common);
        commandBuffer.Transition(args.Depth, default, TextureLayout.Sampled, TextureLayout.Common);
        commandBuffer.Transition(args.MotionVectors, default, TextureLayout.Sampled, TextureLayout.Common);
        commandBuffer.Transition(args.DiffuseAlbedo, default, TextureLayout.Sampled, TextureLayout.Common);
        commandBuffer.Transition(args.SpecularAlbedo, default, TextureLayout.Sampled, TextureLayout.Common);
        commandBuffer.Transition(args.Normals, default, TextureLayout.Sampled, TextureLayout.Common);
        commandBuffer.Transition(args.Roughness, default, TextureLayout.Sampled, TextureLayout.Common);
        commandBuffer.Transition(args.SpecularHitDistance, default, TextureLayout.Sampled, TextureLayout.Common);

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

        commandBuffer.BeginExternalCommands();

        nint commandList = GetCommandList(commandBuffer);
        NGXResult result = Context.GraphicsApi is GraphicsApi.DirectX12 ? EvaluateDirectX12(commandList, feature, args) : EvaluateVulkan(commandList, feature, args);
        result.Success();

        commandBuffer.EndExternalCommands();

        commandBuffer.Transition(args.Color, default, TextureLayout.Common, TextureLayout.Sampled);
        commandBuffer.Transition(args.Depth, default, TextureLayout.Common, TextureLayout.Sampled);
        commandBuffer.Transition(args.MotionVectors, default, TextureLayout.Common, TextureLayout.Sampled);
        commandBuffer.Transition(args.DiffuseAlbedo, default, TextureLayout.Common, TextureLayout.Sampled);
        commandBuffer.Transition(args.SpecularAlbedo, default, TextureLayout.Common, TextureLayout.Sampled);
        commandBuffer.Transition(args.Normals, default, TextureLayout.Common, TextureLayout.Sampled);
        commandBuffer.Transition(args.Roughness, default, TextureLayout.Common, TextureLayout.Sampled);
        commandBuffer.Transition(args.SpecularHitDistance, default, TextureLayout.Common, TextureLayout.Sampled);

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

    public void Evaluate(CommandBuffer commandBuffer, DLSSFeature feature, FrameGenerationArgs args, NGXDLSSGOptEvalParams* optEvalParams)
    {
        commandBuffer.Transition(args.Color, default, TextureLayout.Sampled, TextureLayout.Common);
        commandBuffer.Transition(args.Depth, default, TextureLayout.Sampled, TextureLayout.Common);
        commandBuffer.Transition(args.MotionVectors, default, TextureLayout.Sampled, TextureLayout.Common);

        if (args.Hudless is not null)
        {
            commandBuffer.Transition(args.Hudless, default, TextureLayout.Sampled, TextureLayout.Common);
        }

        commandBuffer.BeginExternalCommands();

        nint commandList = GetCommandList(commandBuffer);
        NGXResult result = Context.GraphicsApi is GraphicsApi.DirectX12 ? EvaluateDirectX12(commandList, feature, args, optEvalParams) : EvaluateVulkan(commandList, feature, args, optEvalParams);
        result.Success();

        commandBuffer.EndExternalCommands();

        commandBuffer.Transition(args.Color, default, TextureLayout.Common, TextureLayout.Sampled);
        commandBuffer.Transition(args.Depth, default, TextureLayout.Common, TextureLayout.Sampled);
        commandBuffer.Transition(args.MotionVectors, default, TextureLayout.Common, TextureLayout.Sampled);

        if (args.Hudless is not null)
        {
            commandBuffer.Transition(args.Hudless, default, TextureLayout.Common, TextureLayout.Sampled);
        }
    }

    public override (uint Width, uint Height) GetSuperResolutionRenderSize(uint outputWidth, uint outputHeight, UpscalingQuality quality)
    {
        return GetOptimalRenderSize(NGXFeature.SuperSampling, outputWidth, outputHeight, DLSSFormats.NGX(quality));
    }

    public override (uint Width, uint Height) GetRayReconstructionRenderSize(uint outputWidth, uint outputHeight, UpscalingQuality quality)
    {
        return GetOptimalRenderSize(NGXFeature.RayReconstruction, outputWidth, outputHeight, DLSSFormats.NGX(quality));
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
        if (device is 0)
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

        NGXResult result = Context.GraphicsApi is GraphicsApi.DirectX12 ? Ngx.D3D12.Shutdown1(device) : Ngx.Vulkan.Shutdown1(device);
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

    private (uint Width, uint Height) GetOptimalRenderSize(NGXFeature feature, uint outputWidth, uint outputHeight, NGXPerfQualityValue perfQualityValue)
    {
        uint width = 0;
        uint height = 0;
        NGXResult result = feature switch
        {
            NGXFeature.SuperSampling => Ngx.DLSS.GetOptimalSettings(parameters, outputWidth, outputHeight, perfQualityValue, out width, out height, out _, out _, out _, out _, out _),
            NGXFeature.RayReconstruction => Ngx.DLSSD.GetOptimalSettings(parameters, outputWidth, outputHeight, perfQualityValue, out width, out height, out _, out _, out _, out _, out _),
            _ => default
        };
        result.Success();

        return result.IsSuccess() ? (width, height) : default;
    }

    private bool CreateVulkanResources()
    {
        delegate* unmanaged<nint, byte*, nint> getDeviceProcAddr = (delegate* unmanaged<nint, byte*, nint>)Context.GetNativeObject(NativeObjectType.VulkanGetDeviceProcAddr);

        delegate* unmanaged<nint, void*, void*, nint*, int> vkCreateDescriptorSetLayout = (delegate* unmanaged<nint, void*, void*, nint*, int>)GetProcedureAddress("vkCreateDescriptorSetLayout");
        delegate* unmanaged<nint, void*, void*, nint*, int> vkCreatePipelineLayout = (delegate* unmanaged<nint, void*, void*, nint*, int>)GetProcedureAddress("vkCreatePipelineLayout");
        delegate* unmanaged<nint, void*, void*, nint*, int> vkCreateDescriptorPool = (delegate* unmanaged<nint, void*, void*, nint*, int>)GetProcedureAddress("vkCreateDescriptorPool");
        delegate* unmanaged<nint, void*, nint*, int> vkAllocateDescriptorSets = (delegate* unmanaged<nint, void*, nint*, int>)GetProcedureAddress("vkAllocateDescriptorSets");

        vkCreateImageView = (delegate* unmanaged<nint, void*, void*, nint*, int>)GetProcedureAddress("vkCreateImageView");
        vkDestroyImageView = (delegate* unmanaged<nint, nint, void*, void>)GetProcedureAddress("vkDestroyImageView");
        vkDestroyDescriptorSetLayout = (delegate* unmanaged<nint, nint, void*, void>)GetProcedureAddress("vkDestroyDescriptorSetLayout");
        vkDestroyPipelineLayout = (delegate* unmanaged<nint, nint, void*, void>)GetProcedureAddress("vkDestroyPipelineLayout");
        vkDestroyDescriptorPool = (delegate* unmanaged<nint, nint, void*, void>)GetProcedureAddress("vkDestroyDescriptorPool");
        vkCmdBindDescriptorSets = (delegate* unmanaged<nint, uint, nint, uint, uint, nint*, uint, uint*, void>)GetProcedureAddress("vkCmdBindDescriptorSets");

        fixed (nint* pDescriptorSetLayout = &descriptorSetLayout)
        fixed (nint* pPipelineLayout = &pipelineLayout)
        fixed (nint* pDescriptorPool = &descriptorPool)
        fixed (nint* pDescriptorSet = &descriptorSet)
        {
            VkDescriptorSetLayoutCreateInfo descriptorSetLayoutCreateInfo = new() { SType = DescriptorSetLayoutCreateInfoType };
            int result = vkCreateDescriptorSetLayout(device, &descriptorSetLayoutCreateInfo, null, pDescriptorSetLayout);
            result.Success();

            if (result is not 0)
            {
                return false;
            }

            VkPipelineLayoutCreateInfo pipelineLayoutCreateInfo = new()
            {
                SType = PipelineLayoutCreateInfoType,
                SetLayoutCount = 1,
                PSetLayouts = pDescriptorSetLayout
            };
            result = vkCreatePipelineLayout(device, &pipelineLayoutCreateInfo, null, pPipelineLayout);
            result.Success();

            if (result is not 0)
            {
                return false;
            }

            VkDescriptorPoolCreateInfo descriptorPoolCreateInfo = new()
            {
                SType = DescriptorPoolCreateInfoType,
                MaxSets = 1
            };
            result = vkCreateDescriptorPool(device, &descriptorPoolCreateInfo, null, pDescriptorPool);
            result.Success();

            if (result is not 0)
            {
                return false;
            }

            VkDescriptorSetAllocateInfo descriptorSetAllocateInfo = new()
            {
                SType = DescriptorSetAllocateInfoType,
                DescriptorPool = descriptorPool,
                DescriptorSetCount = 1,
                PSetLayouts = pDescriptorSetLayout
            };
            result = vkAllocateDescriptorSets(device, &descriptorSetAllocateInfo, pDescriptorSet);
            result.Success();

            return result is 0;
        }

        nint GetProcedureAddress(string name)
        {
            using ZenithMarshal.Scope scope = new();

            return getDeviceProcAddr(device, (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8));
        }
    }

    private void DestroyVulkanResources()
    {
        foreach (NGXResourceVK resource in resources.Values)
        {
            DestroyVulkanResource(resource);
        }
        resources.Clear();

        if (descriptorPool is not 0)
        {
            vkDestroyDescriptorPool(device, descriptorPool, null);
        }

        if (pipelineLayout is not 0)
        {
            vkDestroyPipelineLayout(device, pipelineLayout, null);
        }

        if (descriptorSetLayout is not 0)
        {
            vkDestroyDescriptorSetLayout(device, descriptorSetLayout, null);
        }
    }

    private NGXResult CreateVulkan(nint commandList, DLSSFeature feature, NGXDLSSCreateParams* createParams)
    {
        BindDescriptorSet(commandList);

        fixed (NGXHandle* handle = &feature.Handle)
        {
            return Ngx.Vulkan.CreateDLSSExt1(device, commandList, 1, 1, handle, feature.Parameters, createParams);
        }
    }

    private NGXResult CreateVulkan(nint commandList, DLSSFeature feature, NGXDLSSDCreateParams* createParams)
    {
        BindDescriptorSet(commandList);

        fixed (NGXHandle* handle = &feature.Handle)
        {
            return Ngx.Vulkan.CreateDLSSDExt1(device, commandList, 1, 1, handle, feature.Parameters, createParams);
        }
    }

    private NGXResult CreateVulkan(nint commandList, DLSSFeature feature, NGXDLSSGCreateParams* createParams)
    {
        BindDescriptorSet(commandList);

        // The Vulkan helper does not forward the render size or the dynamic resolution switch.
        feature.SetUI(Ngx.DLSSGParameterInternalWidth, createParams->RenderWidth);
        feature.SetUI(Ngx.DLSSGParameterInternalHeight, createParams->RenderHeight);
        feature.SetUI(Ngx.DLSSGParameterDynamicResolution, 0);

        fixed (NGXHandle* handle = &feature.Handle)
        {
            return Ngx.Vulkan.CreateDLSSG(commandList, 1, 1, handle, feature.Parameters, createParams);
        }
    }

    private NGXResult EvaluateVulkan(nint commandList, DLSSFeature feature, SuperResolutionArgs args)
    {
        BindDescriptorSet(commandList);

        NGXResourceVK color = GetVulkanResource(args.Color);
        NGXResourceVK depth = GetVulkanResource(args.Depth);
        NGXResourceVK motionVectors = GetVulkanResource(args.MotionVectors);
        NGXResourceVK output = GetVulkanResource(args.Output, readWrite: true);

        NGXVKDLSSEvalParams evalParams = new()
        {
            Feature = new()
            {
                Color = &color,
                Output = &output
            },
            Depth = &depth,
            MotionVectors = &motionVectors,
            JitterOffsetX = -args.JitterOffset.X,
            JitterOffsetY = -args.JitterOffset.Y,
            RenderSubrectDimensions = new()
            {
                Width = args.Color.Desc.Width,
                Height = args.Color.Desc.Height
            },
            Reset = args.Reset ? 1 : 0,
            MVScaleX = args.MotionVectors.Desc.Width,
            MVScaleY = args.MotionVectors.Desc.Height,
            FrameTimeDeltaInMsec = args.DeltaTimeInSeconds * 1000.0f,
            PreExposure = args.PreExposure ?? 1.0f,
            ExposureScale = 1.0f
        };

        NGXResourceVK exposure;
        if (args.Exposure is not null)
        {
            exposure = GetVulkanResource(args.Exposure);

            evalParams.ExposureTexture = &exposure;
        }

        NGXResourceVK reactiveMask;
        if (args.ReactiveMask is not null)
        {
            reactiveMask = GetVulkanResource(args.ReactiveMask);

            evalParams.BiasCurrentColorMask = &reactiveMask;
        }

        return Ngx.Vulkan.EvaluateDLSSExt(commandList, feature.Handle, feature.Parameters, &evalParams);
    }

    private NGXResult EvaluateVulkan(nint commandList, DLSSFeature feature, RayReconstructionArgs args)
    {
        BindDescriptorSet(commandList);

        NGXResourceVK color = GetVulkanResource(args.Color);
        NGXResourceVK depth = GetVulkanResource(args.Depth);
        NGXResourceVK motionVectors = GetVulkanResource(args.MotionVectors);
        NGXResourceVK diffuseAlbedo = GetVulkanResource(args.DiffuseAlbedo);
        NGXResourceVK specularAlbedo = GetVulkanResource(args.SpecularAlbedo);
        NGXResourceVK normals = GetVulkanResource(args.Normals);
        NGXResourceVK roughness = GetVulkanResource(args.Roughness);
        NGXResourceVK specularHitDistance = GetVulkanResource(args.SpecularHitDistance);
        NGXResourceVK output = GetVulkanResource(args.Output, readWrite: true);

        NGXVKDLSSDEvalParams evalParams = new()
        {
            DiffuseAlbedo = &diffuseAlbedo,
            SpecularAlbedo = &specularAlbedo,
            Normals = &normals,
            Roughness = &roughness,
            Color = &color,
            Output = &output,
            Depth = &depth,
            MotionVectors = &motionVectors,
            JitterOffsetX = -args.JitterOffset.X,
            JitterOffsetY = -args.JitterOffset.Y,
            RenderSubrectDimensions = new()
            {
                Width = args.Color.Desc.Width,
                Height = args.Color.Desc.Height
            },
            Reset = args.Reset ? 1 : 0,
            MVScaleX = args.MotionVectors.Desc.Width,
            MVScaleY = args.MotionVectors.Desc.Height,
            FrameTimeDeltaInMsec = args.DeltaTimeInSeconds * 1000.0f,
            PreExposure = args.PreExposure ?? 1.0f,
            ExposureScale = 1.0f,
            SpecularHitDistance = &specularHitDistance,
            WorldToViewMatrix = &args.View,
            ViewToClipMatrix = &args.Projection
        };

        NGXResourceVK exposure;
        if (args.Exposure is not null)
        {
            exposure = GetVulkanResource(args.Exposure);

            evalParams.ExposureTexture = &exposure;
        }

        NGXResourceVK reactiveMask;
        if (args.ReactiveMask is not null)
        {
            reactiveMask = GetVulkanResource(args.ReactiveMask);

            evalParams.BiasCurrentColorMask = &reactiveMask;
        }

        NGXResourceVK transparencyOverlay;
        if (args.TransparencyOverlay is not null)
        {
            transparencyOverlay = GetVulkanResource(args.TransparencyOverlay);

            evalParams.TransparencyLayer = &transparencyOverlay;
        }

        return Ngx.Vulkan.EvaluateDLSSDExt(commandList, feature.Handle, feature.Parameters, &evalParams);
    }

    private NGXResult EvaluateVulkan(nint commandList, DLSSFeature feature, FrameGenerationArgs args, NGXDLSSGOptEvalParams* optEvalParams)
    {
        BindDescriptorSet(commandList);

        NGXResourceVK backbuffer = GetVulkanResource(args.Color);
        NGXResourceVK depth = GetVulkanResource(args.Depth);
        NGXResourceVK motionVectors = GetVulkanResource(args.MotionVectors);
        NGXResourceVK output = GetVulkanResource(args.Output, readWrite: true);

        NGXVKDLSSGEvalParams evalParams = new()
        {
            Backbuffer = &backbuffer,
            Depth = &depth,
            MVecs = &motionVectors,
            OutputInterpFrame = &output
        };

        NGXResourceVK hudless;
        if (args.Hudless is not null)
        {
            hudless = GetVulkanResource(args.Hudless);

            evalParams.Hudless = &hudless;
        }

        return Ngx.Vulkan.EvaluateDLSSG(commandList, feature.Handle, feature.Parameters, &evalParams, optEvalParams);
    }

    private void BindDescriptorSet(nint commandList)
    {
        fixed (nint* pDescriptorSet = &descriptorSet)
        {
            vkCmdBindDescriptorSets(commandList, ComputeBindPoint, pipelineLayout, 0, 1, pDescriptorSet, 0, null);
        }
    }

    private NGXResourceVK GetVulkanResource(Texture texture, bool readWrite = false)
    {
        nint image = texture.GetNativeObject(NativeObjectType.VulkanImage);

        if (!resources.TryGetValue(image, out NGXResourceVK resource))
        {
            (uint format, uint aspectFlags) = DLSSFormats.Vulkan(texture.Desc.Format);

            NGXVkImageSubresourceRange subresourceRange = new()
            {
                AspectMask = aspectFlags,
                LevelCount = 1,
                LayerCount = 1
            };

            VkImageViewCreateInfo createInfo = new()
            {
                SType = ImageViewCreateInfoType,
                Image = image,
                ViewType = ImageViewType2D,
                Format = format,
                SubresourceRange = subresourceRange
            };

            nint view = 0;
            vkCreateImageView(device, &createInfo, null, &view).Success();

            resources[image] = resource = new()
            {
                Resource = new()
                {
                    ImageViewInfo = new()
                    {
                        ImageView = view,
                        Image = image,
                        SubresourceRange = subresourceRange,
                        Format = (NGXVkFormat)format,
                        Width = texture.Desc.Width,
                        Height = texture.Desc.Height
                    }
                },
                Type = NGXResourceVKType.VkImageView
            };

            texture.Disposing += OnTextureDisposing;
        }

        return resource with { ReadWrite = readWrite };
    }

    private void OnTextureDisposing(object? sender, EventArgs args)
    {
        Texture texture = (Texture)sender!;
        texture.Disposing -= OnTextureDisposing;

        if (resources.Remove(texture.GetNativeObject(NativeObjectType.VulkanImage), out NGXResourceVK resource))
        {
            DestroyVulkanResource(resource);
        }
    }

    private void DestroyVulkanResource(NGXResourceVK resource)
    {
        vkDestroyImageView(device, resource.Resource.ImageViewInfo.ImageView, null);
    }

    private static void CompleteCreation(CommandBuffer commandBuffer, NGXResult result)
    {
        result.Success();

        commandBuffer.Submit().Wait();
    }

    private static NGXResult CreateDirectX12(nint commandList, DLSSFeature feature, NGXDLSSCreateParams* createParams)
    {
        fixed (NGXHandle* handle = &feature.Handle)
        {
            return Ngx.D3D12.CreateDLSSExt(commandList, 1, 1, handle, feature.Parameters, createParams);
        }
    }

    private static NGXResult CreateDirectX12(nint commandList, DLSSFeature feature, NGXDLSSDCreateParams* createParams)
    {
        fixed (NGXHandle* handle = &feature.Handle)
        {
            return Ngx.D3D12.CreateDLSSDExt(commandList, 1, 1, handle, feature.Parameters, createParams);
        }
    }

    private static NGXResult CreateDirectX12(nint commandList, DLSSFeature feature, NGXDLSSGCreateParams* createParams)
    {
        fixed (NGXHandle* handle = &feature.Handle)
        {
            return Ngx.D3D12.CreateDLSSG(commandList, 1, 1, handle, feature.Parameters, createParams);
        }
    }

    private static NGXResult EvaluateDirectX12(nint commandList, DLSSFeature feature, SuperResolutionArgs args)
    {
        NGXD3D12DLSSEvalParams evalParams = new()
        {
            Feature = new()
            {
                Color = args.Color.GetNativeObject(NativeObjectType.D3D12Resource),
                Output = args.Output.GetNativeObject(NativeObjectType.D3D12Resource)
            },
            Depth = args.Depth.GetNativeObject(NativeObjectType.D3D12Resource),
            MotionVectors = args.MotionVectors.GetNativeObject(NativeObjectType.D3D12Resource),
            JitterOffsetX = -args.JitterOffset.X,
            JitterOffsetY = -args.JitterOffset.Y,
            RenderSubrectDimensions = new()
            {
                Width = args.Color.Desc.Width,
                Height = args.Color.Desc.Height
            },
            Reset = args.Reset ? 1 : 0,
            MVScaleX = args.MotionVectors.Desc.Width,
            MVScaleY = args.MotionVectors.Desc.Height,
            ExposureTexture = args.Exposure?.GetNativeObject(NativeObjectType.D3D12Resource) ?? 0,
            BiasCurrentColorMask = args.ReactiveMask?.GetNativeObject(NativeObjectType.D3D12Resource) ?? 0,
            FrameTimeDeltaInMsec = args.DeltaTimeInSeconds * 1000.0f,
            PreExposure = args.PreExposure ?? 1.0f,
            ExposureScale = 1.0f
        };

        return Ngx.D3D12.EvaluateDLSSExt(commandList, feature.Handle, feature.Parameters, &evalParams);
    }

    private static NGXResult EvaluateDirectX12(nint commandList, DLSSFeature feature, RayReconstructionArgs args)
    {
        NGXD3D12DLSSDEvalParams evalParams = new()
        {
            DiffuseAlbedo = args.DiffuseAlbedo.GetNativeObject(NativeObjectType.D3D12Resource),
            SpecularAlbedo = args.SpecularAlbedo.GetNativeObject(NativeObjectType.D3D12Resource),
            Normals = args.Normals.GetNativeObject(NativeObjectType.D3D12Resource),
            Roughness = args.Roughness.GetNativeObject(NativeObjectType.D3D12Resource),
            Color = args.Color.GetNativeObject(NativeObjectType.D3D12Resource),
            Output = args.Output.GetNativeObject(NativeObjectType.D3D12Resource),
            Depth = args.Depth.GetNativeObject(NativeObjectType.D3D12Resource),
            MotionVectors = args.MotionVectors.GetNativeObject(NativeObjectType.D3D12Resource),
            JitterOffsetX = -args.JitterOffset.X,
            JitterOffsetY = -args.JitterOffset.Y,
            RenderSubrectDimensions = new()
            {
                Width = args.Color.Desc.Width,
                Height = args.Color.Desc.Height
            },
            Reset = args.Reset ? 1 : 0,
            MVScaleX = args.MotionVectors.Desc.Width,
            MVScaleY = args.MotionVectors.Desc.Height,
            ExposureTexture = args.Exposure?.GetNativeObject(NativeObjectType.D3D12Resource) ?? 0,
            BiasCurrentColorMask = args.ReactiveMask?.GetNativeObject(NativeObjectType.D3D12Resource) ?? 0,
            SpecularHitDistance = args.SpecularHitDistance.GetNativeObject(NativeObjectType.D3D12Resource),
            WorldToViewMatrix = &args.View,
            ViewToClipMatrix = &args.Projection,
            FrameTimeDeltaInMsec = args.DeltaTimeInSeconds * 1000.0f,
            PreExposure = args.PreExposure ?? 1.0f,
            ExposureScale = 1.0f,
            TransparencyLayer = args.TransparencyOverlay?.GetNativeObject(NativeObjectType.D3D12Resource) ?? 0
        };

        return Ngx.D3D12.EvaluateDLSSDExt(commandList, feature.Handle, feature.Parameters, &evalParams);
    }

    private static NGXResult EvaluateDirectX12(nint commandList, DLSSFeature feature, FrameGenerationArgs args, NGXDLSSGOptEvalParams* optEvalParams)
    {
        NGXD3D12DLSSGEvalParams evalParams = new()
        {
            Backbuffer = args.Color.GetNativeObject(NativeObjectType.D3D12Resource),
            Depth = args.Depth.GetNativeObject(NativeObjectType.D3D12Resource),
            MVecs = args.MotionVectors.GetNativeObject(NativeObjectType.D3D12Resource),
            Hudless = args.Hudless?.GetNativeObject(NativeObjectType.D3D12Resource) ?? 0,
            OutputInterpFrame = args.Output.GetNativeObject(NativeObjectType.D3D12Resource)
        };

        return Ngx.D3D12.EvaluateDLSSG(commandList, feature.Handle, feature.Parameters, &evalParams, optEvalParams);
    }

    private static NGXResult InitializeVulkan(GraphicsContext context, nint device, byte* projectId, byte* engineVersion, void* dataPath, NGXFeatureCommonInfo* featureInfo)
    {
        return Ngx.Vulkan.InitWithProjectID(projectId,
                                           NGXEngineType.Custom,
                                           engineVersion,
                                           dataPath,
                                           context.GetNativeObject(NativeObjectType.VulkanInstance),
                                           context.GetNativeObject(NativeObjectType.VulkanPhysicalDevice),
                                           device,
                                           (delegate* unmanaged[Cdecl]<nint, byte*, delegate* unmanaged[Cdecl]<void>>)context.GetNativeObject(NativeObjectType.VulkanGetInstanceProcAddr),
                                           (delegate* unmanaged[Cdecl]<nint, byte*, delegate* unmanaged[Cdecl]<void>>)context.GetNativeObject(NativeObjectType.VulkanGetDeviceProcAddr),
                                           featureInfo,
                                           NGXVersion.Api);
    }
}

[StructLayout(LayoutKind.Sequential)]
file unsafe struct VkImageViewCreateInfo
{
    public uint SType;

    public void* PNext;

    public uint Flags;

    public nint Image;

    public uint ViewType;

    public uint Format;

    public fixed uint Components[4];

    public NGXVkImageSubresourceRange SubresourceRange;
}

[StructLayout(LayoutKind.Sequential)]
file unsafe struct VkDescriptorSetLayoutCreateInfo
{
    public uint SType;

    public void* PNext;

    public uint Flags;

    public uint BindingCount;

    public void* PBindings;
}

[StructLayout(LayoutKind.Sequential)]
file unsafe struct VkPipelineLayoutCreateInfo
{
    public uint SType;

    public void* PNext;

    public uint Flags;

    public uint SetLayoutCount;

    public nint* PSetLayouts;

    public uint PushConstantRangeCount;

    public void* PPushConstantRanges;
}

[StructLayout(LayoutKind.Sequential)]
file unsafe struct VkDescriptorPoolCreateInfo
{
    public uint SType;

    public void* PNext;

    public uint Flags;

    public uint MaxSets;

    public uint PoolSizeCount;

    public void* PPoolSizes;
}

[StructLayout(LayoutKind.Sequential)]
file unsafe struct VkDescriptorSetAllocateInfo
{
    public uint SType;

    public void* PNext;

    public nint DescriptorPool;

    public uint DescriptorSetCount;

    public nint* PSetLayouts;
}
