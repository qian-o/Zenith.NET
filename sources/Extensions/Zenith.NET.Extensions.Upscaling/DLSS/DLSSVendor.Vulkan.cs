using System.Runtime.InteropServices;
using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal unsafe partial class DLSSVendor
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
    private readonly Dictionary<nint, NGXResourceVK> resources = [];

    private delegate* unmanaged<nint, void*, void*, nint*, int> vkCreateImageView;
    private delegate* unmanaged<nint, nint, void*, void> vkDestroyImageView;
    private delegate* unmanaged<nint, nint, void*, void> vkDestroyDescriptorSetLayout;
    private delegate* unmanaged<nint, nint, void*, void> vkDestroyPipelineLayout;
    private delegate* unmanaged<nint, nint, void*, void> vkDestroyDescriptorPool;
    private delegate* unmanaged<nint, uint, nint, uint, uint, nint*, uint, uint*, void> vkCmdBindDescriptorSets;

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
            int result = vkCreateDescriptorSetLayout(Device, &descriptorSetLayoutCreateInfo, null, pDescriptorSetLayout);
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
            result = vkCreatePipelineLayout(Device, &pipelineLayoutCreateInfo, null, pPipelineLayout);
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
            result = vkCreateDescriptorPool(Device, &descriptorPoolCreateInfo, null, pDescriptorPool);
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
            result = vkAllocateDescriptorSets(Device, &descriptorSetAllocateInfo, pDescriptorSet);
            result.Success();

            return result is 0;
        }

        nint GetProcedureAddress(string name)
        {
            using ZenithMarshal.Scope scope = new();

            return getDeviceProcAddr(Device, (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8));
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
            vkDestroyDescriptorPool(Device, descriptorPool, null);
        }

        if (pipelineLayout is not 0)
        {
            vkDestroyPipelineLayout(Device, pipelineLayout, null);
        }

        if (descriptorSetLayout is not 0)
        {
            vkDestroyDescriptorSetLayout(Device, descriptorSetLayout, null);
        }
    }

    private NGXResult CreateVulkan(nint commandList, DLSSFeature feature, NGXDLSSCreateParams* createParams)
    {
        BindDescriptorSet(commandList);

        fixed (NGXHandle* handle = &feature.Handle)
        {
            return Ngx.Vulkan.CreateDLSSExt1(Device, commandList, 1, 1, handle, feature.Parameters, createParams);
        }
    }

    private NGXResult CreateVulkan(nint commandList, DLSSFeature feature, NGXDLSSDCreateParams* createParams)
    {
        BindDescriptorSet(commandList);

        fixed (NGXHandle* handle = &feature.Handle)
        {
            return Ngx.Vulkan.CreateDLSSDExt1(Device, commandList, 1, 1, handle, feature.Parameters, createParams);
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
            JitterOffsetX = args.JitterOffset.X,
            JitterOffsetY = args.JitterOffset.Y,
            RenderSubrectDimensions = new()
            {
                Width = args.Color.Desc.Width,
                Height = args.Color.Desc.Height
            },
            Reset = args.Reset ? 1 : 0,
            MVScaleX = args.MotionVectors.Desc.Width,
            MVScaleY = args.MotionVectors.Desc.Height,
            FrameTimeDeltaInMsec = args.DeltaTimeInSeconds * 1000.0f,
            PreExposure = args.PreExposure,
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
            JitterOffsetX = args.JitterOffset.X,
            JitterOffsetY = args.JitterOffset.Y,
            RenderSubrectDimensions = new()
            {
                Width = args.Color.Desc.Width,
                Height = args.Color.Desc.Height
            },
            Reset = args.Reset ? 1 : 0,
            MVScaleX = args.MotionVectors.Desc.Width,
            MVScaleY = args.MotionVectors.Desc.Height,
            FrameTimeDeltaInMsec = args.DeltaTimeInSeconds * 1000.0f,
            PreExposure = args.PreExposure,
            ExposureScale = 1.0f,
            SpecularHitDistance = &specularHitDistance,
            WorldToViewMatrix = &args.WorldToView,
            ViewToClipMatrix = &args.ViewToClip
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
            vkCreateImageView(Device, &createInfo, null, &view).Success();

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
        vkDestroyImageView(Device, resource.Resource.ImageViewInfo.ImageView, null);
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
