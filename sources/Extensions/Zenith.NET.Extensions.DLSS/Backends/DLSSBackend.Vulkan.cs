using System.Runtime.InteropServices;
using NGX.NET;

namespace Zenith.NET.Extensions.DLSS;

internal unsafe partial class DLSSBackend
{
    private const uint ImageViewCreateInfoType = 15;
    private const uint PipelineLayoutCreateInfoType = 30;
    private const uint DescriptorSetLayoutCreateInfoType = 32;
    private const uint DescriptorPoolCreateInfoType = 33;
    private const uint DescriptorSetAllocateInfoType = 34;
    private const uint ImageViewType2D = 1;
    private const uint ComputeBindPoint = 1;

    private readonly nint descriptorSetLayout;
    private readonly nint pipelineLayout;
    private readonly nint descriptorPool;
    private readonly nint descriptorSet;

    private delegate* unmanaged<nint, void*, void*, nint*, int> vkCreateImageView;
    private delegate* unmanaged<nint, nint, void*, void> vkDestroyImageView;
    private delegate* unmanaged<nint, nint, void*, void> vkDestroyDescriptorSetLayout;
    private delegate* unmanaged<nint, nint, void*, void> vkDestroyPipelineLayout;
    private delegate* unmanaged<nint, nint, void*, void> vkDestroyDescriptorPool;
    private delegate* unmanaged<nint, uint, nint, uint, uint, nint*, uint, uint*, void> vkCmdBindDescriptorSets;

    private void InitializeVulkan(byte* projectId, byte* engineVersion, void* dataPath, NGXFeatureCommonInfo* featureInfo)
    {
        Ngx.Vulkan.InitWithProjectID(projectId,
                                     NGXEngineType.Custom,
                                     engineVersion,
                                     dataPath,
                                     Context.GetNativeObject(NativeObjectType.VulkanInstance),
                                     Context.GetNativeObject(NativeObjectType.VulkanPhysicalDevice),
                                     Device,
                                     (delegate* unmanaged[Cdecl]<nint, byte*, delegate* unmanaged[Cdecl]<void>>)Context.GetNativeObject(NativeObjectType.VulkanGetInstanceProcAddr),
                                     (delegate* unmanaged[Cdecl]<nint, byte*, delegate* unmanaged[Cdecl]<void>>)Context.GetNativeObject(NativeObjectType.VulkanGetDeviceProcAddr),
                                     featureInfo,
                                     NGXVersion.Api).Success();

        delegate* unmanaged<nint, byte*, nint> getDeviceProcAddr = (delegate* unmanaged<nint, byte*, nint>)Context.GetNativeObject(NativeObjectType.VulkanGetDeviceProcAddr);

        delegate* unmanaged<nint, void*, void*, nint*, int> vkCreateDescriptorSetLayout = (delegate* unmanaged<nint, void*, void*, nint*, int>)GetProcedureAddress("vkCreateDescriptorSetLayout");
        delegate* unmanaged<nint, void*, void*, nint*, int> vkCreatePipelineLayout = (delegate* unmanaged<nint, void*, void*, nint*, int>)GetProcedureAddress("vkCreatePipelineLayout");
        delegate* unmanaged<nint, void*, void*, nint*, int> vkCreateDescriptorPool = (delegate* unmanaged<nint, void*, void*, nint*, int>)GetProcedureAddress("vkCreateDescriptorPool");
        delegate* unmanaged<nint, void*, nint*, int> vkAllocateDescriptorSets = (delegate* unmanaged<nint, void*, nint*, int>)GetProcedureAddress("vkAllocateDescriptorSets");

        fixed (nint* pDescriptorSetLayout = &descriptorSetLayout)
        {
            fixed (nint* pPipelineLayout = &pipelineLayout)
            {
                fixed (nint* pDescriptorPool = &descriptorPool)
                {
                    fixed (nint* pDescriptorSet = &descriptorSet)
                    {
                        VkDescriptorSetLayoutCreateInfo descriptorSetLayoutCreateInfo = new() { SType = DescriptorSetLayoutCreateInfoType };
                        vkCreateDescriptorSetLayout(Device, &descriptorSetLayoutCreateInfo, null, pDescriptorSetLayout).Success();

                        VkPipelineLayoutCreateInfo pipelineLayoutCreateInfo = new()
                        {
                            SType = PipelineLayoutCreateInfoType,
                            SetLayoutCount = 1,
                            PSetLayouts = pDescriptorSetLayout
                        };
                        vkCreatePipelineLayout(Device, &pipelineLayoutCreateInfo, null, pPipelineLayout).Success();

                        VkDescriptorPoolCreateInfo descriptorPoolCreateInfo = new()
                        {
                            SType = DescriptorPoolCreateInfoType,
                            MaxSets = 1
                        };
                        vkCreateDescriptorPool(Device, &descriptorPoolCreateInfo, null, pDescriptorPool).Success();

                        VkDescriptorSetAllocateInfo descriptorSetAllocateInfo = new()
                        {
                            SType = DescriptorSetAllocateInfoType,
                            DescriptorPool = descriptorPool,
                            DescriptorSetCount = 1,
                            PSetLayouts = pDescriptorSetLayout
                        };
                        vkAllocateDescriptorSets(Device, &descriptorSetAllocateInfo, pDescriptorSet).Success();
                    }
                }
            }
        }

        vkCreateImageView = (delegate* unmanaged<nint, void*, void*, nint*, int>)GetProcedureAddress("vkCreateImageView");
        vkDestroyImageView = (delegate* unmanaged<nint, nint, void*, void>)GetProcedureAddress("vkDestroyImageView");
        vkDestroyDescriptorSetLayout = (delegate* unmanaged<nint, nint, void*, void>)GetProcedureAddress("vkDestroyDescriptorSetLayout");
        vkDestroyPipelineLayout = (delegate* unmanaged<nint, nint, void*, void>)GetProcedureAddress("vkDestroyPipelineLayout");
        vkDestroyDescriptorPool = (delegate* unmanaged<nint, nint, void*, void>)GetProcedureAddress("vkDestroyDescriptorPool");
        vkCmdBindDescriptorSets = (delegate* unmanaged<nint, uint, nint, uint, uint, nint*, uint, uint*, void>)GetProcedureAddress("vkCmdBindDescriptorSets");

        nint GetProcedureAddress(string name)
        {
            using ZenithMarshal.Scope scope = new();

            return getDeviceProcAddr(Device, (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8));
        }
    }

    private void ShutdownVulkan()
    {
        vkDestroyDescriptorPool(Device, descriptorPool, null);
        vkDestroyPipelineLayout(Device, pipelineLayout, null);
        vkDestroyDescriptorSetLayout(Device, descriptorSetLayout, null);

        Ngx.Vulkan.Shutdown1(Device).Success();
    }

    private void CreateVulkan(nint commandList, DLSSSuperResolution feature, NGXDLSSCreateParams* createParams)
    {
        BindDescriptorSet(commandList);

        fixed (NGXHandle* handle = &feature.Handle)
        {
            Ngx.Vulkan.CreateDLSSExt1(Device, commandList, 1, 1, handle, feature.Parameters, createParams).Success();
        }
    }

    private void CreateVulkan(nint commandList, DLSSRayReconstruction feature, NGXDLSSDCreateParams* createParams)
    {
        BindDescriptorSet(commandList);

        fixed (NGXHandle* handle = &feature.Handle)
        {
            Ngx.Vulkan.CreateDLSSDExt1(Device, commandList, 1, 1, handle, feature.Parameters, createParams).Success();
        }
    }

    private void CreateVulkan(nint commandList, DLSSFrameGeneration feature, NGXDLSSGCreateParams* createParams)
    {
        BindDescriptorSet(commandList);

        fixed (NGXHandle* handle = &feature.Handle)
        {
            Ngx.Vulkan.CreateDLSSG(commandList, 1, 1, handle, feature.Parameters, createParams).Success();
        }
    }

    private void EvaluateVulkan(nint commandList, DLSSSuperResolution feature, DLSSSuperResolutionArgs args, NGXDimensions renderSubrectDimensions)
    {
        BindDescriptorSet(commandList);

        NGXResourceVK input = args.Input.Vulkan with { ReadWrite = false };
        NGXResourceVK output = args.Output.Vulkan with { ReadWrite = true };
        NGXResourceVK depth = args.Depth.Vulkan with { ReadWrite = false };
        NGXResourceVK motionVectors = args.MotionVectors.Vulkan with { ReadWrite = false };
        NGXResourceVK exposure = (args.Exposure?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK reactiveMask = (args.ReactiveMask?.Vulkan ?? default) with { ReadWrite = false };

        NGXVKDLSSEvalParams evalParams = new()
        {
            Feature = new()
            {
                Color = &input,
                Output = &output
            },
            Depth = &depth,
            MotionVectors = &motionVectors,
            JitterOffsetX = args.JitterOffsetX,
            JitterOffsetY = args.JitterOffsetY,
            RenderSubrectDimensions = renderSubrectDimensions,
            Reset = args.Reset ? 1 : 0,
            MVScaleX = args.MotionVectorScaleX,
            MVScaleY = args.MotionVectorScaleY,
            ExposureTexture = args.Exposure.HasValue ? &exposure : null,
            BiasCurrentColorMask = args.ReactiveMask.HasValue ? &reactiveMask : null,
            PreExposure = args.PreExposure,
            ExposureScale = args.ExposureScale
        };

        Ngx.Vulkan.EvaluateDLSSExt(commandList, feature.Handle, feature.Parameters, &evalParams).Success();
    }

    private void EvaluateVulkan(nint commandList, DLSSRayReconstruction feature, DLSSRayReconstructionArgs args, NGXDimensions renderSubrectDimensions)
    {
        BindDescriptorSet(commandList);

        NGXResourceVK diffuseAlbedo = args.DiffuseAlbedo.Vulkan with { ReadWrite = false };
        NGXResourceVK specularAlbedo = args.SpecularAlbedo.Vulkan with { ReadWrite = false };
        NGXResourceVK normals = args.Normals.Vulkan with { ReadWrite = false };
        NGXResourceVK roughness = (args.Roughness?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK input = args.Input.Vulkan with { ReadWrite = false };
        NGXResourceVK output = args.Output.Vulkan with { ReadWrite = true };
        NGXResourceVK depth = args.Depth.Vulkan with { ReadWrite = false };
        NGXResourceVK motionVectors = args.MotionVectors.Vulkan with { ReadWrite = false };
        NGXResourceVK exposure = (args.Exposure?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK reactiveMask = (args.ReactiveMask?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK colorBeforeTransparency = (args.ColorBeforeTransparency?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK subsurfaceScatteringGuide = (args.SubsurfaceScatteringGuide?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK depthOfFieldGuide = (args.DepthOfFieldGuide?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK specularHitDistance = (args.SpecularHitDistance?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK specularMotionVectors = (args.SpecularMotionVectors?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK transparencyLayer = (args.TransparencyLayer?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK transparencyLayerOpacity = (args.TransparencyLayerOpacity?.Vulkan ?? default) with { ReadWrite = false };

        NGXVKDLSSDEvalParams evalParams = new()
        {
            DiffuseAlbedo = &diffuseAlbedo,
            SpecularAlbedo = &specularAlbedo,
            Normals = &normals,
            Roughness = args.Roughness.HasValue ? &roughness : null,
            Color = &input,
            Output = &output,
            Depth = &depth,
            MotionVectors = &motionVectors,
            JitterOffsetX = args.JitterOffsetX,
            JitterOffsetY = args.JitterOffsetY,
            RenderSubrectDimensions = renderSubrectDimensions,
            Reset = args.Reset ? 1 : 0,
            MVScaleX = args.MotionVectorScaleX,
            MVScaleY = args.MotionVectorScaleY,
            ExposureTexture = args.Exposure.HasValue ? &exposure : null,
            BiasCurrentColorMask = args.ReactiveMask.HasValue ? &reactiveMask : null,
            PreExposure = args.PreExposure,
            ExposureScale = args.ExposureScale,
            ColorBeforeTransparency = args.ColorBeforeTransparency.HasValue ? &colorBeforeTransparency : null,
            ScreenSpaceSubsurfaceScatteringGuide = args.SubsurfaceScatteringGuide.HasValue ? &subsurfaceScatteringGuide : null,
            DepthOfFieldGuide = args.DepthOfFieldGuide.HasValue ? &depthOfFieldGuide : null,
            SpecularHitDistance = args.SpecularHitDistance.HasValue ? &specularHitDistance : null,
            WorldToViewMatrix = &args.WorldToView,
            ViewToClipMatrix = &args.ViewToClip,
            MotionVectorsReflections = args.SpecularMotionVectors.HasValue ? &specularMotionVectors : null,
            TransparencyLayer = args.TransparencyLayer.HasValue ? &transparencyLayer : null,
            TransparencyLayerOpacity = args.TransparencyLayerOpacity.HasValue ? &transparencyLayerOpacity : null
        };

        Ngx.Vulkan.EvaluateDLSSDExt(commandList, feature.Handle, feature.Parameters, &evalParams).Success();
    }

    private void EvaluateVulkan(nint commandList, DLSSFrameGeneration feature, DLSSFrameGenerationArgs args, NGXDLSSGOptEvalParams* optEvalParams)
    {
        BindDescriptorSet(commandList);

        NGXResourceVK color = args.Color.Vulkan with { ReadWrite = false };
        NGXResourceVK depth = args.Depth.Vulkan with { ReadWrite = false };
        NGXResourceVK motionVectors = args.MotionVectors.Vulkan with { ReadWrite = false };
        NGXResourceVK hudlessColor = (args.HudlessColor?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK ui = (args.UI?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK uiAlpha = (args.UIAlpha?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK distortionField = (args.DistortionField?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK output = args.Output.Vulkan with { ReadWrite = true };

        NGXVKDLSSGEvalParams evalParams = new()
        {
            Backbuffer = &color,
            Depth = &depth,
            MVecs = &motionVectors,
            Hudless = args.HudlessColor.HasValue ? &hudlessColor : null,
            UI = args.UI.HasValue ? &ui : null,
            UIAlpha = args.UIAlpha.HasValue ? &uiAlpha : null,
            BidirectionalDistortionField = args.DistortionField.HasValue ? &distortionField : null,
            OutputInterpFrame = &output
        };

        Ngx.Vulkan.EvaluateDLSSG(commandList, feature.Handle, feature.Parameters, &evalParams, optEvalParams).Success();
    }

    private DLSSBinding CreateVulkanBinding(Texture texture, NGXDimensions size)
    {
        (uint format, uint aspectFlags) = DLSSFormats.Vulkan(texture.Desc.Format);

        nint image = texture.GetNativeObject(NativeObjectType.VulkanImage);

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

        nint view;
        vkCreateImageView(Device, &createInfo, null, &view).Success();

        NGXResourceVK vulkan = new()
        {
            Resource = new()
            {
                ImageViewInfo = new()
                {
                    ImageView = view,
                    Image = image,
                    SubresourceRange = subresourceRange,
                    Format = (NGXVkFormat)format,
                    Width = size.Width,
                    Height = size.Height
                }
            },
            Type = NGXResourceVKType.VkImageView
        };

        return new(default, vulkan, size);
    }

    private void DestroyVulkanBinding(DLSSBinding binding)
    {
        vkDestroyImageView(Device, binding.Vulkan.Resource.ImageViewInfo.ImageView, null);
    }

    private void BindDescriptorSet(nint commandList)
    {
        fixed (nint* pDescriptorSet = &descriptorSet)
        {
            vkCmdBindDescriptorSets(commandList, ComputeBindPoint, pipelineLayout, 0, 1, pDescriptorSet, 0, null);
        }
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
