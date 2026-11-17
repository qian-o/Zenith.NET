using System.Runtime.InteropServices;
using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal unsafe partial class DLSSVendor
{
    private readonly Lock @lock = new();
    private readonly Dictionary<Texture, NGXResourceVK> resources = [];

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
        using ZenithMarshal.Scope scope = new();

        nint instance = Context.GetNativeObject(NativeObjectType.VulkanInstance);
        nint physicalDevice = Context.GetNativeObject(NativeObjectType.VulkanPhysicalDevice);
        nint getInstanceProcAddr = Context.GetNativeObject(NativeObjectType.VulkanGetInstanceProcAddr);
        nint getDeviceProcAddr = Context.GetNativeObject(NativeObjectType.VulkanGetDeviceProcAddr);

        Ngx.Vulkan.InitWithProjectID(projectId, NGXEngineType.Custom, engineVersion, dataPath, instance, physicalDevice, device, getInstanceProcAddr, getDeviceProcAddr, &featureInfo, NGXVersion.Api).Success();

        createImageView = (delegate* unmanaged<nint, void*, void*, out ulong, int>)GetProcAddr("vkCreateImageView");
        destroyImageView = (delegate* unmanaged<nint, ulong, void*, void>)GetProcAddr("vkDestroyImageView");
        destroyDescriptorSetLayout = (delegate* unmanaged<nint, ulong, void*, void>)GetProcAddr("vkDestroyDescriptorSetLayout");
        destroyPipelineLayout = (delegate* unmanaged<nint, ulong, void*, void>)GetProcAddr("vkDestroyPipelineLayout");
        destroyDescriptorPool = (delegate* unmanaged<nint, ulong, void*, void>)GetProcAddr("vkDestroyDescriptorPool");
        cmdBindDescriptorSets = (delegate* unmanaged<nint, int, ulong, uint, uint, ulong*, uint, uint*, void>)GetProcAddr("vkCmdBindDescriptorSets");

        VkDescriptorSetLayoutCreateInfo descriptorSetLayoutCreateInfo = new() { SType = 32 };
        ((delegate* unmanaged<nint, void*, void*, out ulong, int>)GetProcAddr("vkCreateDescriptorSetLayout"))(device, &descriptorSetLayoutCreateInfo, null, out descriptorSetLayout).Success();

        fixed (ulong* descriptorSetLayouts = &descriptorSetLayout)
        {
            VkPipelineLayoutCreateInfo pipelineLayoutCreateInfo = new()
            {
                SType = 30,
                SetLayoutCount = 1,
                PSetLayouts = descriptorSetLayouts
            };

            ((delegate* unmanaged<nint, void*, void*, out ulong, int>)GetProcAddr("vkCreatePipelineLayout"))(device, &pipelineLayoutCreateInfo, null, out pipelineLayout).Success();

            VkDescriptorPoolSize descriptorPoolSize = new() { DescriptorCount = 1 };

            VkDescriptorPoolCreateInfo descriptorPoolCreateInfo = new()
            {
                SType = 33,
                MaxSets = 1,
                PoolSizeCount = 1,
                PPoolSizes = &descriptorPoolSize
            };

            ((delegate* unmanaged<nint, void*, void*, out ulong, int>)GetProcAddr("vkCreateDescriptorPool"))(device, &descriptorPoolCreateInfo, null, out descriptorPool).Success();

            VkDescriptorSetAllocateInfo descriptorSetAllocateInfo = new()
            {
                SType = 34,
                DescriptorPool = descriptorPool,
                DescriptorSetCount = 1,
                PSetLayouts = descriptorSetLayouts
            };

            ((delegate* unmanaged<nint, void*, out ulong, int>)GetProcAddr("vkAllocateDescriptorSets"))(device, &descriptorSetAllocateInfo, out descriptorSet).Success();
        }

        nint GetProcAddr(string name)
        {
            return ((delegate* unmanaged<nint, byte*, nint>)getDeviceProcAddr)(device, (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8));
        }
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
        fixed (ulong* descriptorSets = &descriptorSet)
        {
            cmdBindDescriptorSets(commandBuffer, 1, pipelineLayout, 0, 1, descriptorSets, 0, null);
        }
    }

    private NGXResourceVK GetResourceVulkan(Texture texture, bool isStorage)
    {
        using Lock.Scope _ = @lock.EnterScope();

        if (!resources.TryGetValue(texture, out NGXResourceVK resource))
        {
            NGXVkFormat format = DLSSFormats.Vulkan(texture.Desc.Format);

            NGXVkImageSubresourceRange subresourceRange = new()
            {
                AspectMask = ZenithHelper.HasDepth(texture.Desc.Format) ? 2u : 1u,
                LevelCount = 1,
                LayerCount = 1
            };

            VkImageViewCreateInfo imageViewCreateInfo = new()
            {
                SType = 15,
                Image = (ulong)texture.Vulkan(),
                ViewType = 1,
                Format = format,
                SubresourceRange = subresourceRange
            };

            createImageView(device, &imageViewCreateInfo, null, out ulong imageView).Success();

            resources[texture] = resource = Ngx.CreateImageViewResourceVK((nint)imageView, texture.Vulkan(), subresourceRange, format, texture.Desc.Width, texture.Desc.Height, false);

            texture.Disposing += (sender, _) =>
            {
                using Lock.Scope scope = @lock.EnterScope();

                if (resources.Remove((Texture)sender!, out NGXResourceVK disposedResource))
                {
                    destroyImageView(device, (ulong)disposedResource.Resource.ImageViewInfo.ImageView, null);
                }
            };
        }

        return resource with { ReadWrite = isStorage };
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
file unsafe struct VkImageViewCreateInfo
{
    public int SType;

    public void* PNext;

    public uint Flags;

    public ulong Image;

    public int ViewType;

    public NGXVkFormat Format;

    public fixed int Components[4];

    public NGXVkImageSubresourceRange SubresourceRange;
}
