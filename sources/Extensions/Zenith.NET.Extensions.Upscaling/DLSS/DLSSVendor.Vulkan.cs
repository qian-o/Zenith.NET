using System.Runtime.InteropServices;
using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal unsafe partial class DLSSVendor
{
    private readonly Lock @lock = new();
    private readonly Dictionary<(Texture Texture, bool IsStorage), NGXResourceVK> resources = [];

    private delegate* unmanaged<nint, void*, void*, out ulong, int> createImageView;
    private delegate* unmanaged<nint, ulong, void*, void> destroyImageView;
    private delegate* unmanaged<nint, ulong, void*, void> destroyDescriptorSetLayout;
    private delegate* unmanaged<nint, ulong, void*, void> destroyPipelineLayout;
    private delegate* unmanaged<nint, ulong, void*, void> destroyDescriptorPool;
    private delegate* unmanaged<nint, int, ulong, uint, uint, ulong*, uint, uint*, void> cmdBindDescriptorSets;
    private ulong descriptorSetLayout;
    private ulong pipelineLayout;
    private ulong descriptorPool;
    private ulong descriptorSet;

    private void InitializeVulkan(string projectId, string engineVersion, string dataPath, NGXFeatureCommonInfo featureInfo)
    {
        const int VkStructureTypePipelineLayoutCreateInfo = 30;
        const int VkStructureTypeDescriptorSetLayoutCreateInfo = 32;
        const int VkStructureTypeDescriptorPoolCreateInfo = 33;
        const int VkStructureTypeDescriptorSetAllocateInfo = 34;

        nint instance = Context.GetNativeObject(NativeObjectType.VulkanInstance);
        nint physicalDevice = Context.GetNativeObject(NativeObjectType.VulkanPhysicalDevice);
        nint getInstanceProcAddr = Context.GetNativeObject(NativeObjectType.VulkanGetInstanceProcAddr);
        nint getDeviceProcAddr = Context.GetNativeObject(NativeObjectType.VulkanGetDeviceProcAddr);

        Ngx.Vulkan.InitWithProjectID(projectId, NGXEngineType.Custom, engineVersion, dataPath, instance, physicalDevice, device, getInstanceProcAddr, getDeviceProcAddr, &featureInfo, NGXVersion.Api).Success();

        nint GetProcAddr(string name)
        {
            using ZenithMarshal.Scope scope = new();
            return ((delegate* unmanaged<nint, byte*, nint>)getDeviceProcAddr)(device, (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8));
        }

        createImageView = (delegate* unmanaged<nint, void*, void*, out ulong, int>)GetProcAddr("vkCreateImageView");
        destroyImageView = (delegate* unmanaged<nint, ulong, void*, void>)GetProcAddr("vkDestroyImageView");
        destroyDescriptorSetLayout = (delegate* unmanaged<nint, ulong, void*, void>)GetProcAddr("vkDestroyDescriptorSetLayout");
        destroyPipelineLayout = (delegate* unmanaged<nint, ulong, void*, void>)GetProcAddr("vkDestroyPipelineLayout");
        destroyDescriptorPool = (delegate* unmanaged<nint, ulong, void*, void>)GetProcAddr("vkDestroyDescriptorPool");
        cmdBindDescriptorSets = (delegate* unmanaged<nint, int, ulong, uint, uint, ulong*, uint, uint*, void>)GetProcAddr("vkCmdBindDescriptorSets");

        VkDescriptorSetLayoutCreateInfo setLayoutCreateInfo = new() { SType = VkStructureTypeDescriptorSetLayoutCreateInfo };
        ((delegate* unmanaged<nint, void*, void*, out ulong, int>)GetProcAddr("vkCreateDescriptorSetLayout"))(device, &setLayoutCreateInfo, null, out ulong setLayout).Success();
        descriptorSetLayout = setLayout;

        VkPipelineLayoutCreateInfo pipelineLayoutCreateInfo = new()
        {
            SType = VkStructureTypePipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = &setLayout
        };

        ((delegate* unmanaged<nint, void*, void*, out ulong, int>)GetProcAddr("vkCreatePipelineLayout"))(device, &pipelineLayoutCreateInfo, null, out pipelineLayout).Success();

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

        ((delegate* unmanaged<nint, void*, void*, out ulong, int>)GetProcAddr("vkCreateDescriptorPool"))(device, &poolCreateInfo, null, out descriptorPool).Success();

        VkDescriptorSetAllocateInfo setAllocateInfo = new()
        {
            SType = VkStructureTypeDescriptorSetAllocateInfo,
            DescriptorPool = descriptorPool,
            DescriptorSetCount = 1,
            PSetLayouts = &setLayout
        };

        ((delegate* unmanaged<nint, void*, out ulong, int>)GetProcAddr("vkAllocateDescriptorSets"))(device, &setAllocateInfo, out descriptorSet).Success();
    }

    private void DestroyVulkan()
    {
        foreach (NGXResourceVK resource in resources.Values)
        {
            destroyImageView(device, (ulong)resource.Resource.ImageViewInfo.ImageView, null);
        }
        resources.Clear();

        destroyDescriptorPool(device, descriptorPool, null);
        destroyPipelineLayout(device, pipelineLayout, null);
        destroyDescriptorSetLayout(device, descriptorSetLayout, null);

        Ngx.Vulkan.Shutdown1(device).Success();
    }

    private NGXHandle CreateFeatureVulkan(nint commandBuffer, NGXParameter parameters, NGXDLSSCreateParams createParams)
    {
        BindDescriptorSetVulkan(commandBuffer);

        NGXHandle handle = default;
        Ngx.Vulkan.CreateDLSSExt1(device, commandBuffer, 1, 1, &handle, parameters, &createParams).Success();

        return handle;
    }

    private NGXHandle CreateFeatureVulkan(nint commandBuffer, NGXParameter parameters, NGXDLSSDCreateParams createParams)
    {
        BindDescriptorSetVulkan(commandBuffer);

        NGXHandle handle = default;
        Ngx.Vulkan.CreateDLSSDExt1(device, commandBuffer, 1, 1, &handle, parameters, &createParams).Success();

        return handle;
    }

    private NGXHandle CreateFeatureVulkan(nint commandBuffer, NGXParameter parameters, NGXDLSSGCreateParams createParams)
    {
        BindDescriptorSetVulkan(commandBuffer);

        NGXHandle handle = default;
        Ngx.Vulkan.CreateDLSSG(commandBuffer, 1, 1, &handle, parameters, &createParams).Success();

        return handle;
    }

    private void EvaluateFeatureVulkan(nint commandBuffer, NGXParameter parameters, NGXHandle handle, SuperResolutionArgs args, Texture output)
    {
        BindDescriptorSetVulkan(commandBuffer);

        NGXResourceVK vkColor = GetResourceVulkan(args.Color, false);
        NGXResourceVK vkOutput = GetResourceVulkan(output, true);
        NGXResourceVK vkDepth = GetResourceVulkan(args.Depth, false);
        NGXResourceVK vkMotionVectors = GetResourceVulkan(args.MotionVectors, false);
        NGXResourceVK vkExposure = args.Exposure is not null ? GetResourceVulkan(args.Exposure, false) : default;
        NGXResourceVK vkReactiveMask = args.ReactiveMask is not null ? GetResourceVulkan(args.ReactiveMask, false) : default;

        NGXVKDLSSEvalParams evalParams = new()
        {
            Feature = new()
            {
                Color = &vkColor,
                Output = &vkOutput
            },
            Depth = &vkDepth,
            MotionVectors = &vkMotionVectors,
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
            ExposureTexture = args.Exposure is not null ? &vkExposure : null,
            BiasCurrentColorMask = args.ReactiveMask is not null ? &vkReactiveMask : null,
            PreExposure = args.PreExposure ?? 1.0f,
            ExposureScale = 1.0f,
            FrameTimeDeltaInMsec = args.DeltaTimeInSeconds * 1000.0f
        };

        Ngx.Vulkan.EvaluateDLSSExt(commandBuffer, handle, parameters, &evalParams).Success();
    }

    private void EvaluateFeatureVulkan(nint commandBuffer, NGXParameter parameters, NGXHandle handle, RayReconstructionArgs args, Texture output)
    {
        BindDescriptorSetVulkan(commandBuffer);

        NGXResourceVK vkDiffuseAlbedo = GetResourceVulkan(args.DiffuseAlbedo, false);
        NGXResourceVK vkSpecularAlbedo = GetResourceVulkan(args.SpecularAlbedo, false);
        NGXResourceVK vkNormals = GetResourceVulkan(args.Normals, false);
        NGXResourceVK vkRoughness = GetResourceVulkan(args.Roughness, false);
        NGXResourceVK vkColor = GetResourceVulkan(args.Color, false);
        NGXResourceVK vkOutput = GetResourceVulkan(output, true);
        NGXResourceVK vkDepth = GetResourceVulkan(args.Depth, false);
        NGXResourceVK vkMotionVectors = GetResourceVulkan(args.MotionVectors, false);
        NGXResourceVK vkExposure = args.Exposure is not null ? GetResourceVulkan(args.Exposure, false) : default;
        NGXResourceVK vkReactiveMask = args.ReactiveMask is not null ? GetResourceVulkan(args.ReactiveMask, false) : default;
        NGXResourceVK vkSpecularHitDistance = args.SpecularHitDistance is not null ? GetResourceVulkan(args.SpecularHitDistance, false) : default;
        NGXResourceVK vkTransparencyOverlay = args.TransparencyOverlay is not null ? GetResourceVulkan(args.TransparencyOverlay, false) : default;

        NGXVKDLSSDEvalParams evalParams = new()
        {
            DiffuseAlbedo = &vkDiffuseAlbedo,
            SpecularAlbedo = &vkSpecularAlbedo,
            Normals = &vkNormals,
            Roughness = &vkRoughness,
            Color = &vkColor,
            Output = &vkOutput,
            Depth = &vkDepth,
            MotionVectors = &vkMotionVectors,
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
            ExposureTexture = args.Exposure is not null ? &vkExposure : null,
            BiasCurrentColorMask = args.ReactiveMask is not null ? &vkReactiveMask : null,
            PreExposure = args.PreExposure ?? 1.0f,
            ExposureScale = 1.0f,
            SpecularHitDistance = args.SpecularHitDistance is not null ? &vkSpecularHitDistance : null,
            WorldToViewMatrix = &args.View,
            ViewToClipMatrix = &args.Projection,
            FrameTimeDeltaInMsec = args.DeltaTimeInSeconds * 1000.0f,
            TransparencyLayer = args.TransparencyOverlay is not null ? &vkTransparencyOverlay : null
        };

        Ngx.Vulkan.EvaluateDLSSDExt(commandBuffer, handle, parameters, &evalParams).Success();
    }

    private void EvaluateFeatureVulkan(nint commandBuffer, NGXParameter parameters, NGXHandle handle, FrameGenerationArgs args, Texture output, NGXDLSSGOptEvalParams optEvalParams)
    {
        BindDescriptorSetVulkan(commandBuffer);

        NGXResourceVK vkColor = GetResourceVulkan(args.Color, false);
        NGXResourceVK vkDepth = GetResourceVulkan(args.Depth, false);
        NGXResourceVK vkMotionVectors = GetResourceVulkan(args.MotionVectors, false);
        NGXResourceVK vkHudless = args.Hudless is not null ? GetResourceVulkan(args.Hudless, false) : default;
        NGXResourceVK vkOutput = GetResourceVulkan(output, true);

        NGXVKDLSSGEvalParams evalParams = new()
        {
            Backbuffer = &vkColor,
            Depth = &vkDepth,
            MVecs = &vkMotionVectors,
            Hudless = args.Hudless is not null ? &vkHudless : null,
            OutputInterpFrame = &vkOutput
        };

        Ngx.Vulkan.EvaluateDLSSG(commandBuffer, handle, parameters, &evalParams, &optEvalParams).Success();
    }

    private void BindDescriptorSetVulkan(nint commandBuffer)
    {
        const int VkPipelineBindPointCompute = 1;

        ulong set = descriptorSet;
        cmdBindDescriptorSets(commandBuffer, VkPipelineBindPointCompute, pipelineLayout, 0, 1, &set, 0, null);
    }

    private NGXResourceVK GetResourceVulkan(Texture texture, bool isStorage)
    {
        const int VkStructureTypeImageViewCreateInfo = 15;
        const int VkStructureTypeImageViewUsageCreateInfo = 1000117002;
        const int VkImageViewType2D = 1;
        const uint VkImageUsageSampledBit = 4;
        const uint VkImageAspectColorBit = 1;
        const uint VkImageAspectDepthBit = 2;

        using Lock.Scope _ = @lock.EnterScope();

        if (!resources.TryGetValue((texture, isStorage), out NGXResourceVK resource))
        {
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

            createImageView(device, &createInfo, null, out ulong imageView).Success();

            resources[(texture, isStorage)] = resource = Ngx.CreateImageViewResourceVK((nint)imageView, texture.Vulkan(), subresourceRange, format, texture.Desc.Width, texture.Desc.Height, isStorage);

            texture.Disposing += (sender, _) =>
            {
                using Lock.Scope scope = @lock.EnterScope();

                if (resources.Remove(((Texture)sender!, isStorage), out NGXResourceVK disposedResource))
                {
                    destroyImageView(device, (ulong)disposedResource.Resource.ImageViewInfo.ImageView, null);
                }
            };
        }

        return resource;
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
