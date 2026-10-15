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

        NGXResourceVK color = args.Color.Vulkan with { ReadWrite = false };
        NGXResourceVK depth = args.Depth.Vulkan with { ReadWrite = false };
        NGXResourceVK motionVectors = args.MotionVectors.Vulkan with { ReadWrite = false };
        NGXResourceVK exposure = (args.Exposure?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK biasCurrentColorMask = (args.BiasCurrentColorMask?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK output = args.Output.Vulkan with { ReadWrite = true };

        NGXVKDLSSEvalParams evalParams = new()
        {
            Feature = new()
            {
                Color = &color,
                Output = &output
            },
            Depth = &depth,
            MotionVectors = &motionVectors,
            ExposureTexture = args.Exposure.HasValue ? &exposure : null,
            BiasCurrentColorMask = args.BiasCurrentColorMask.HasValue ? &biasCurrentColorMask : null,
            JitterOffsetX = args.JitterOffset.X,
            JitterOffsetY = args.JitterOffset.Y,
            MVScaleX = args.MotionVectorScale.X,
            MVScaleY = args.MotionVectorScale.Y,
            PreExposure = args.PreExposure,
            ExposureScale = args.ExposureScale,
            RenderSubrectDimensions = renderSubrectDimensions,
            Reset = args.Reset ? 1 : 0
        };

        Ngx.Vulkan.EvaluateDLSSExt(commandList, feature.Handle, feature.Parameters, &evalParams).Success();
    }

    private void EvaluateVulkan(nint commandList, DLSSRayReconstruction feature, DLSSRayReconstructionArgs args, NGXDimensions renderSubrectDimensions)
    {
        BindDescriptorSet(commandList);

        NGXResourceVK color = args.Color.Vulkan with { ReadWrite = false };
        NGXResourceVK alpha = (args.Alpha?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK depth = args.Depth.Vulkan with { ReadWrite = false };
        NGXResourceVK motionVectors = args.MotionVectors.Vulkan with { ReadWrite = false };
        NGXResourceVK diffuseAlbedo = args.DiffuseAlbedo.Vulkan with { ReadWrite = false };
        NGXResourceVK specularAlbedo = args.SpecularAlbedo.Vulkan with { ReadWrite = false };
        NGXResourceVK reflectedAlbedo = (args.ReflectedAlbedo?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK normals = args.Normals.Vulkan with { ReadWrite = false };
        NGXResourceVK roughness = (args.Roughness?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK specularMotionVectors = (args.SpecularMotionVectors?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK diffuseHitDistance = (args.DiffuseHitDistance?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK specularHitDistance = (args.SpecularHitDistance?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK diffuseRayDirection = (args.DiffuseRayDirection?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK specularRayDirection = (args.SpecularRayDirection?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK diffuseRayDirectionHitDistance = (args.DiffuseRayDirectionHitDistance?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK specularRayDirectionHitDistance = (args.SpecularRayDirectionHitDistance?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK transparencyLayer = (args.TransparencyLayer?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK transparencyLayerOpacity = (args.TransparencyLayerOpacity?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK transparencyLayerMotionVectors = (args.TransparencyLayerMotionVectors?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK colorBeforeParticles = (args.ColorBeforeParticles?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK colorAfterParticles = (args.ColorAfterParticles?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK colorBeforeTransparency = (args.ColorBeforeTransparency?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK colorAfterTransparency = (args.ColorAfterTransparency?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK colorBeforeFog = (args.ColorBeforeFog?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK colorAfterFog = (args.ColorAfterFog?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK screenSpaceSubsurfaceScatteringGuide = (args.ScreenSpaceSubsurfaceScatteringGuide?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK colorBeforeScreenSpaceSubsurfaceScattering = (args.ColorBeforeScreenSpaceSubsurfaceScattering?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK colorAfterScreenSpaceSubsurfaceScattering = (args.ColorAfterScreenSpaceSubsurfaceScattering?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK screenSpaceRefractionGuide = (args.ScreenSpaceRefractionGuide?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK colorBeforeScreenSpaceRefraction = (args.ColorBeforeScreenSpaceRefraction?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK colorAfterScreenSpaceRefraction = (args.ColorAfterScreenSpaceRefraction?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK depthOfFieldGuide = (args.DepthOfFieldGuide?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK colorBeforeDepthOfField = (args.ColorBeforeDepthOfField?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK colorAfterDepthOfField = (args.ColorAfterDepthOfField?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK exposure = (args.Exposure?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK biasCurrentColorMask = (args.BiasCurrentColorMask?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK disocclusionMask = (args.DisocclusionMask?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK responsivityMask = (args.ResponsivityMask?.Vulkan ?? default) with { ReadWrite = false };
        NGXResourceVK output = args.Output.Vulkan with { ReadWrite = true };
        NGXResourceVK outputAlpha = (args.OutputAlpha?.Vulkan ?? default) with { ReadWrite = true };

        NGXVKDLSSDEvalParams evalParams = new()
        {
            Color = &color,
            Alpha = args.Alpha.HasValue ? &alpha : null,
            Depth = &depth,
            MotionVectors = &motionVectors,
            DiffuseAlbedo = &diffuseAlbedo,
            SpecularAlbedo = &specularAlbedo,
            ReflectedAlbedo = args.ReflectedAlbedo.HasValue ? &reflectedAlbedo : null,
            Normals = &normals,
            Roughness = args.Roughness.HasValue ? &roughness : null,
            MotionVectorsReflections = args.SpecularMotionVectors.HasValue ? &specularMotionVectors : null,
            DiffuseHitDistance = args.DiffuseHitDistance.HasValue ? &diffuseHitDistance : null,
            SpecularHitDistance = args.SpecularHitDistance.HasValue ? &specularHitDistance : null,
            DiffuseRayDirection = args.DiffuseRayDirection.HasValue ? &diffuseRayDirection : null,
            SpecularRayDirection = args.SpecularRayDirection.HasValue ? &specularRayDirection : null,
            DiffuseRayDirectionHitDistance = args.DiffuseRayDirectionHitDistance.HasValue ? &diffuseRayDirectionHitDistance : null,
            SpecularRayDirectionHitDistance = args.SpecularRayDirectionHitDistance.HasValue ? &specularRayDirectionHitDistance : null,
            TransparencyLayer = args.TransparencyLayer.HasValue ? &transparencyLayer : null,
            TransparencyLayerOpacity = args.TransparencyLayerOpacity.HasValue ? &transparencyLayerOpacity : null,
            TransparencyLayerMvecs = args.TransparencyLayerMotionVectors.HasValue ? &transparencyLayerMotionVectors : null,
            ColorBeforeParticles = args.ColorBeforeParticles.HasValue ? &colorBeforeParticles : null,
            ColorAfterParticles = args.ColorAfterParticles.HasValue ? &colorAfterParticles : null,
            ColorBeforeTransparency = args.ColorBeforeTransparency.HasValue ? &colorBeforeTransparency : null,
            ColorAfterTransparency = args.ColorAfterTransparency.HasValue ? &colorAfterTransparency : null,
            ColorBeforeFog = args.ColorBeforeFog.HasValue ? &colorBeforeFog : null,
            ColorAfterFog = args.ColorAfterFog.HasValue ? &colorAfterFog : null,
            ScreenSpaceSubsurfaceScatteringGuide = args.ScreenSpaceSubsurfaceScatteringGuide.HasValue ? &screenSpaceSubsurfaceScatteringGuide : null,
            ColorBeforeScreenSpaceSubsurfaceScattering = args.ColorBeforeScreenSpaceSubsurfaceScattering.HasValue ? &colorBeforeScreenSpaceSubsurfaceScattering : null,
            ColorAfterScreenSpaceSubsurfaceScattering = args.ColorAfterScreenSpaceSubsurfaceScattering.HasValue ? &colorAfterScreenSpaceSubsurfaceScattering : null,
            ScreenSpaceRefractionGuide = args.ScreenSpaceRefractionGuide.HasValue ? &screenSpaceRefractionGuide : null,
            ColorBeforeScreenSpaceRefraction = args.ColorBeforeScreenSpaceRefraction.HasValue ? &colorBeforeScreenSpaceRefraction : null,
            ColorAfterScreenSpaceRefraction = args.ColorAfterScreenSpaceRefraction.HasValue ? &colorAfterScreenSpaceRefraction : null,
            DepthOfFieldGuide = args.DepthOfFieldGuide.HasValue ? &depthOfFieldGuide : null,
            ColorBeforeDepthOfField = args.ColorBeforeDepthOfField.HasValue ? &colorBeforeDepthOfField : null,
            ColorAfterDepthOfField = args.ColorAfterDepthOfField.HasValue ? &colorAfterDepthOfField : null,
            ExposureTexture = args.Exposure.HasValue ? &exposure : null,
            BiasCurrentColorMask = args.BiasCurrentColorMask.HasValue ? &biasCurrentColorMask : null,
            DisocclusionMask = args.DisocclusionMask.HasValue ? &disocclusionMask : null,
            ResponsivityMask = args.ResponsivityMask.HasValue ? &responsivityMask : null,
            Output = &output,
            OutputAlpha = args.OutputAlpha.HasValue ? &outputAlpha : null,
            JitterOffsetX = args.JitterOffset.X,
            JitterOffsetY = args.JitterOffset.Y,
            MVScaleX = args.MotionVectorScale.X,
            MVScaleY = args.MotionVectorScale.Y,
            WorldToViewMatrix = &args.WorldToView,
            ViewToClipMatrix = &args.ViewToClip,
            PreExposure = args.PreExposure,
            ExposureScale = args.ExposureScale,
            RenderSubrectDimensions = renderSubrectDimensions,
            Reset = args.Reset ? 1 : 0
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
        NGXResourceVK outputDisableInterpolation = default;

        if (args.OutputDisableInterpolation is { } buffer)
        {
            outputDisableInterpolation = new()
            {
                Resource = new()
                {
                    BufferInfo = new()
                    {
                        Buffer = buffer.GetNativeObject(NativeObjectType.VulkanBuffer),
                        SizeInBytes = buffer.Desc.SizeInBytes
                    }
                },
                Type = NGXResourceVKType.VkBuffer,
                ReadWrite = true
            };
        }

        NGXVKDLSSGEvalParams evalParams = new()
        {
            Backbuffer = &color,
            Depth = &depth,
            MVecs = &motionVectors,
            Hudless = args.HudlessColor.HasValue ? &hudlessColor : null,
            UI = args.UI.HasValue ? &ui : null,
            UIAlpha = args.UIAlpha.HasValue ? &uiAlpha : null,
            BidirectionalDistortionField = args.DistortionField.HasValue ? &distortionField : null,
            OutputInterpFrame = &output,
            OutputDisableInterpolation = args.OutputDisableInterpolation is not null ? &outputDisableInterpolation : null
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
