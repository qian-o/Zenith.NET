using System.Runtime.InteropServices;
using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal unsafe partial class DLSSVendor
{
    private readonly Lock imageViewLock = new();
    private readonly Dictionary<Texture, (ulong Sampled, ulong Storage)> imageViews = [];
    private readonly ulong descriptorSetLayout;
    private readonly ulong pipelineLayout;
    private readonly ulong descriptorPool;
    private readonly ulong descriptorSet;

    private delegate* unmanaged<nint, void*, void*, ulong*, int> createImageView;
    private delegate* unmanaged<nint, ulong, void*, void> destroyImageView;
    private delegate* unmanaged<nint, ulong, void*, void> destroyDescriptorSetLayout;
    private delegate* unmanaged<nint, ulong, void*, void> destroyPipelineLayout;
    private delegate* unmanaged<nint, ulong, void*, void> destroyDescriptorPool;
    private delegate* unmanaged<nint, int, ulong, uint, uint, ulong*, uint, uint*, void> cmdBindDescriptorSets;

    private void InitializeVulkan(string projectId, string engineVersion, string dataPath, NGXFeatureCommonInfo* commonInfo)
    {
        const int VkStructureTypePipelineLayoutCreateInfo = 30;
        const int VkStructureTypeDescriptorSetLayoutCreateInfo = 32;
        const int VkStructureTypeDescriptorPoolCreateInfo = 33;
        const int VkStructureTypeDescriptorSetAllocateInfo = 34;

        (nint instance, nint physicalDevice, Device, nint getInstanceProcAddr, nint getDeviceProcAddr) = Context.Vulkan();

        Ngx.Vulkan.InitWithProjectID(projectId, NGXEngineType.Custom, engineVersion, dataPath, instance, physicalDevice, Device, getInstanceProcAddr, getDeviceProcAddr, commonInfo, NGXVersion.Api).Success();

        using ZenithMarshal.Scope scope = new();

        nint GetProcAddr(string name)
        {
            return ((delegate* unmanaged<nint, byte*, nint>)getDeviceProcAddr)(Device, (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8));
        }

        createImageView = (delegate* unmanaged<nint, void*, void*, ulong*, int>)GetProcAddr("vkCreateImageView");
        destroyImageView = (delegate* unmanaged<nint, ulong, void*, void>)GetProcAddr("vkDestroyImageView");
        destroyDescriptorSetLayout = (delegate* unmanaged<nint, ulong, void*, void>)GetProcAddr("vkDestroyDescriptorSetLayout");
        destroyPipelineLayout = (delegate* unmanaged<nint, ulong, void*, void>)GetProcAddr("vkDestroyPipelineLayout");
        destroyDescriptorPool = (delegate* unmanaged<nint, ulong, void*, void>)GetProcAddr("vkDestroyDescriptorPool");
        cmdBindDescriptorSets = (delegate* unmanaged<nint, int, ulong, uint, uint, ulong*, uint, uint*, void>)GetProcAddr("vkCmdBindDescriptorSets");

        fixed (ulong* pDescriptorSetLayout = &descriptorSetLayout)
        {
            fixed (ulong* pPipelineLayout = &pipelineLayout)
            {
                fixed (ulong* pDescriptorPool = &descriptorPool)
                {
                    fixed (ulong* pDescriptorSet = &descriptorSet)
                    {
                        VkDescriptorSetLayoutCreateInfo setLayoutCreateInfo = new() { SType = VkStructureTypeDescriptorSetLayoutCreateInfo };
                        ((delegate* unmanaged<nint, void*, void*, ulong*, int>)GetProcAddr("vkCreateDescriptorSetLayout"))(Device, &setLayoutCreateInfo, null, pDescriptorSetLayout).Success();

                        VkPipelineLayoutCreateInfo pipelineLayoutCreateInfo = new()
                        {
                            SType = VkStructureTypePipelineLayoutCreateInfo,
                            SetLayoutCount = 1,
                            PSetLayouts = pDescriptorSetLayout
                        };

                        ((delegate* unmanaged<nint, void*, void*, ulong*, int>)GetProcAddr("vkCreatePipelineLayout"))(Device, &pipelineLayoutCreateInfo, null, pPipelineLayout).Success();

                        VkDescriptorPoolSize poolSize = new()
                        {
                            Type = 0,
                            DescriptorCount = 1
                        };

                        VkDescriptorPoolCreateInfo poolCreateInfo = new()
                        {
                            SType = VkStructureTypeDescriptorPoolCreateInfo,
                            MaxSets = 1,
                            PoolSizeCount = 1,
                            PPoolSizes = &poolSize
                        };

                        ((delegate* unmanaged<nint, void*, void*, ulong*, int>)GetProcAddr("vkCreateDescriptorPool"))(Device, &poolCreateInfo, null, pDescriptorPool).Success();

                        VkDescriptorSetAllocateInfo setAllocateInfo = new()
                        {
                            SType = VkStructureTypeDescriptorSetAllocateInfo,
                            DescriptorPool = descriptorPool,
                            DescriptorSetCount = 1,
                            PSetLayouts = pDescriptorSetLayout
                        };

                        ((delegate* unmanaged<nint, void*, ulong*, int>)GetProcAddr("vkAllocateDescriptorSets"))(Device, &setAllocateInfo, pDescriptorSet).Success();
                    }
                }
            }
        }
    }

    private void ShutdownVulkan()
    {
        using (Lock.Scope _ = imageViewLock.EnterScope())
        {
            foreach ((ulong sampled, ulong storage) in imageViews.Values)
            {
                if (storage is not 0)
                {
                    destroyImageView(Device, storage, null);
                }

                if (sampled is not 0)
                {
                    destroyImageView(Device, sampled, null);
                }
            }
            imageViews.Clear();
        }

        destroyDescriptorPool(Device, descriptorPool, null);
        destroyPipelineLayout(Device, pipelineLayout, null);
        destroyDescriptorSetLayout(Device, descriptorSetLayout, null);

        Ngx.Vulkan.Shutdown1(Device).Success();
    }

    private void CreateVulkan(nint commandBuffer, DLSSSuperResolution feature, NGXDLSSCreateParams* createParams)
    {
        BindDescriptorSet(commandBuffer);

        fixed (NGXHandle* handle = &feature.Handle)
        {
            Ngx.Vulkan.CreateDLSSExt1(Device, commandBuffer, 1, 1, handle, feature.Parameters, createParams).Success();
        }
    }

    private void CreateVulkan(nint commandBuffer, DLSSRayReconstruction feature, NGXDLSSDCreateParams* createParams)
    {
        BindDescriptorSet(commandBuffer);

        fixed (NGXHandle* handle = &feature.Handle)
        {
            Ngx.Vulkan.CreateDLSSDExt1(Device, commandBuffer, 1, 1, handle, feature.Parameters, createParams).Success();
        }
    }

    private void CreateVulkan(nint commandBuffer, DLSSFrameGeneration feature, NGXDLSSGCreateParams* createParams)
    {
        BindDescriptorSet(commandBuffer);

        DLSSHelper.SetUI(feature.Parameters, Ngx.DLSSGParameterInternalWidth, feature.Desc.RenderWidth);
        DLSSHelper.SetUI(feature.Parameters, Ngx.DLSSGParameterInternalHeight, feature.Desc.RenderHeight);
        DLSSHelper.SetUI(feature.Parameters, Ngx.DLSSGParameterDynamicResolution, 0);

        fixed (NGXHandle* handle = &feature.Handle)
        {
            Ngx.Vulkan.CreateDLSSG(commandBuffer, 1, 1, handle, feature.Parameters, createParams).Success();
        }
    }

    private void EvaluateVulkan(nint commandBuffer, DLSSSuperResolution feature, SuperResolutionArgs args)
    {
        BindDescriptorSet(commandBuffer);

        NGXResourceVK color = EnsureVulkanResource(args.Color, false);
        NGXResourceVK output = EnsureVulkanResource(feature.Output, true);
        NGXResourceVK depth = EnsureVulkanResource(args.Depth, false);
        NGXResourceVK motionVectors = EnsureVulkanResource(args.MotionVectors, false);
        NGXResourceVK exposure = args.Exposure is not null ? EnsureVulkanResource(args.Exposure, false) : default;
        NGXResourceVK reactiveMask = args.ReactiveMask is not null ? EnsureVulkanResource(args.ReactiveMask, false) : default;

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
                Width = feature.Desc.RenderWidth,
                Height = feature.Desc.RenderHeight
            },
            Reset = args.Reset ? 1 : 0,
            MVScaleX = feature.Desc.RenderWidth,
            MVScaleY = feature.Desc.RenderHeight,
            ExposureTexture = args.Exposure is not null ? &exposure : null,
            BiasCurrentColorMask = args.ReactiveMask is not null ? &reactiveMask : null,
            PreExposure = args.PreExposure ?? 1.0f,
            ExposureScale = 1.0f,
            FrameTimeDeltaInMsec = args.DeltaTimeInSeconds * 1000.0f
        };

        Ngx.Vulkan.EvaluateDLSSExt(commandBuffer, feature.Handle, feature.Parameters, &evalParams).Success();
    }

    private void EvaluateVulkan(nint commandBuffer, DLSSRayReconstruction feature, RayReconstructionArgs args)
    {
        BindDescriptorSet(commandBuffer);

        NGXResourceVK diffuseAlbedo = EnsureVulkanResource(args.DiffuseAlbedo, false);
        NGXResourceVK specularAlbedo = EnsureVulkanResource(args.SpecularAlbedo, false);
        NGXResourceVK normals = EnsureVulkanResource(args.Normals, false);
        NGXResourceVK roughness = EnsureVulkanResource(args.Roughness, false);
        NGXResourceVK color = EnsureVulkanResource(args.Color, false);
        NGXResourceVK output = EnsureVulkanResource(feature.Output, true);
        NGXResourceVK depth = EnsureVulkanResource(args.Depth, false);
        NGXResourceVK motionVectors = EnsureVulkanResource(args.MotionVectors, false);
        NGXResourceVK exposure = args.Exposure is not null ? EnsureVulkanResource(args.Exposure, false) : default;
        NGXResourceVK reactiveMask = args.ReactiveMask is not null ? EnsureVulkanResource(args.ReactiveMask, false) : default;
        NGXResourceVK specularHitDistance = args.SpecularHitDistance is not null ? EnsureVulkanResource(args.SpecularHitDistance, false) : default;
        NGXResourceVK transparencyOverlay = args.TransparencyOverlay is not null ? EnsureVulkanResource(args.TransparencyOverlay, false) : default;

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
                Width = feature.Desc.RenderWidth,
                Height = feature.Desc.RenderHeight
            },
            Reset = args.Reset ? 1 : 0,
            MVScaleX = feature.Desc.RenderWidth,
            MVScaleY = feature.Desc.RenderHeight,
            ExposureTexture = args.Exposure is not null ? &exposure : null,
            BiasCurrentColorMask = args.ReactiveMask is not null ? &reactiveMask : null,
            PreExposure = args.PreExposure ?? 1.0f,
            ExposureScale = 1.0f,
            SpecularHitDistance = args.SpecularHitDistance is not null ? &specularHitDistance : null,
            WorldToViewMatrix = &args.View,
            ViewToClipMatrix = &args.Projection,
            FrameTimeDeltaInMsec = args.DeltaTimeInSeconds * 1000.0f,
            TransparencyLayer = args.TransparencyOverlay is not null ? &transparencyOverlay : null
        };

        Ngx.Vulkan.EvaluateDLSSDExt(commandBuffer, feature.Handle, feature.Parameters, &evalParams).Success();
    }

    private void EvaluateVulkan(nint commandBuffer, DLSSFrameGeneration feature, FrameGenerationArgs args, NGXDLSSGOptEvalParams* optEvalParams)
    {
        BindDescriptorSet(commandBuffer);

        NGXResourceVK color = EnsureVulkanResource(args.Color, false);
        NGXResourceVK depth = EnsureVulkanResource(args.Depth, false);
        NGXResourceVK motionVectors = EnsureVulkanResource(args.MotionVectors, false);
        NGXResourceVK hudless = args.Hudless is not null ? EnsureVulkanResource(args.Hudless, false) : default;
        NGXResourceVK output = EnsureVulkanResource(feature.Output, true);

        NGXVKDLSSGEvalParams evalParams = new()
        {
            Backbuffer = &color,
            Depth = &depth,
            MVecs = &motionVectors,
            Hudless = args.Hudless is not null ? &hudless : null,
            OutputInterpFrame = &output
        };

        Ngx.Vulkan.EvaluateDLSSG(commandBuffer, feature.Handle, feature.Parameters, &evalParams, optEvalParams).Success();
    }

    private void BindDescriptorSet(nint commandBuffer)
    {
        const int VkPipelineBindPointCompute = 1;

        fixed (ulong* pDescriptorSet = &descriptorSet)
        {
            cmdBindDescriptorSets(commandBuffer, VkPipelineBindPointCompute, pipelineLayout, 0, 1, pDescriptorSet, 0, null);
        }
    }

    private NGXResourceVK EnsureVulkanResource(Texture texture, bool isStorage)
    {
        const int VkStructureTypeImageViewCreateInfo = 15;
        const int VkStructureTypeImageViewUsageCreateInfo = 1000117002;
        const int VkImageViewType2D = 1;
        const uint VkImageUsageSampledBit = 4;
        const uint VkImageAspectColorBit = 1;
        const uint VkImageAspectDepthBit = 2;

        NGXVkFormat format = DLSSFormats.Vulkan(texture.Desc.Format);
        if (isStorage)
        {
            format = format switch
            {
                NGXVkFormat.R8G8B8A8Srgb => NGXVkFormat.R8G8B8A8Unorm,
                NGXVkFormat.B8G8R8A8Srgb => NGXVkFormat.B8G8R8A8Unorm,
                _ => format
            };
        }

        NGXVkImageSubresourceRange subresourceRange = new()
        {
            AspectMask = ZenithHelper.HasDepth(texture.Desc.Format) ? VkImageAspectDepthBit : VkImageAspectColorBit,
            BaseMipLevel = 0,
            LevelCount = 1,
            BaseArrayLayer = 0,
            LayerCount = 1
        };

        ulong imageView;
        using (Lock.Scope _ = imageViewLock.EnterScope())
        {
            imageViews.TryGetValue(texture, out (ulong Sampled, ulong Storage) views);
            imageView = isStorage ? views.Storage : views.Sampled;
            if (imageView is 0)
            {
                VkImageViewUsageCreateInfo usageCreateInfo = new()
                {
                    SType = VkStructureTypeImageViewUsageCreateInfo,
                    Usage = VkImageUsageSampledBit
                };

                VkImageViewCreateInfo createInfo = new()
                {
                    SType = VkStructureTypeImageViewCreateInfo,
                    PNext = !isStorage && texture.Desc.Format is PixelFormat.R8G8B8A8SRgb or PixelFormat.B8G8R8A8SRgb ? &usageCreateInfo : null,
                    Image = (ulong)texture.Vulkan(),
                    ViewType = VkImageViewType2D,
                    Format = format,
                    SubresourceRange = subresourceRange
                };

                createImageView(Device, &createInfo, null, &imageView).Success();

                if (views is (0, 0))
                {
                    texture.Disposing += (sender, _) =>
                    {
                        using Lock.Scope scope = imageViewLock.EnterScope();

                        if (imageViews.Remove((Texture)sender!, out (ulong Sampled, ulong Storage) disposedViews))
                        {
                            if (disposedViews.Storage is not 0)
                            {
                                destroyImageView(Device, disposedViews.Storage, null);
                            }

                            if (disposedViews.Sampled is not 0)
                            {
                                destroyImageView(Device, disposedViews.Sampled, null);
                            }
                        }
                    };
                }

                imageViews[texture] = isStorage ? (views.Sampled, imageView) : (imageView, views.Storage);
            }
        }

        return Ngx.CreateImageViewResourceVK((nint)imageView, texture.Vulkan(), subresourceRange, format, texture.Desc.Width, texture.Desc.Height, isStorage);
    }
}

[StructLayout(LayoutKind.Sequential)]
file unsafe struct VkDescriptorSetLayoutCreateInfo
{
    public int SType;

    public void* PNext;

    public uint Flags;

    public uint BindingCount;

    public void* PBindings;
}

[StructLayout(LayoutKind.Sequential)]
file unsafe struct VkPipelineLayoutCreateInfo
{
    public int SType;

    public void* PNext;

    public uint Flags;

    public uint SetLayoutCount;

    public ulong* PSetLayouts;

    public uint PushConstantRangeCount;

    public void* PPushConstantRanges;
}

[StructLayout(LayoutKind.Sequential)]
file struct VkDescriptorPoolSize
{
    public int Type;

    public uint DescriptorCount;
}

[StructLayout(LayoutKind.Sequential)]
file unsafe struct VkDescriptorPoolCreateInfo
{
    public int SType;

    public void* PNext;

    public uint Flags;

    public uint MaxSets;

    public uint PoolSizeCount;

    public VkDescriptorPoolSize* PPoolSizes;
}

[StructLayout(LayoutKind.Sequential)]
file unsafe struct VkDescriptorSetAllocateInfo
{
    public int SType;

    public void* PNext;

    public ulong DescriptorPool;

    public uint DescriptorSetCount;

    public ulong* PSetLayouts;
}

[StructLayout(LayoutKind.Sequential)]
file struct VkComponentMapping
{
    public int R;

    public int G;

    public int B;

    public int A;
}

[StructLayout(LayoutKind.Sequential)]
file unsafe struct VkImageViewUsageCreateInfo
{
    public int SType;

    public void* PNext;

    public uint Usage;
}

[StructLayout(LayoutKind.Sequential)]
file unsafe struct VkImageViewCreateInfo
{
    public int SType;

    public void* PNext;

    public uint Flags;

    public ulong Image;

    public int ViewType;

    public NGXVkFormat Format;

    public VkComponentMapping Components;

    public NGXVkImageSubresourceRange SubresourceRange;
}
