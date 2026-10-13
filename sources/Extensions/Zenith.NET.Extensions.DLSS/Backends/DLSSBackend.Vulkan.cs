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

    private delegate* unmanaged<nint, void*, void*, nint*, int> vkCreateImageView;
    private delegate* unmanaged<nint, nint, void*, void> vkDestroyImageView;
    private delegate* unmanaged<nint, nint, void*, void> vkDestroyDescriptorSetLayout;
    private delegate* unmanaged<nint, nint, void*, void> vkDestroyPipelineLayout;
    private delegate* unmanaged<nint, nint, void*, void> vkDestroyDescriptorPool;
    private delegate* unmanaged<nint, uint, nint, uint, uint, nint*, uint, uint*, void> vkCmdBindDescriptorSets;
    private nint descriptorSetLayout;
    private nint pipelineLayout;
    private nint descriptorPool;
    private nint descriptorSet;

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
    }

    private void GetVulkanCapabilityParameters(NGXParameter* parameters)
    {
        Ngx.Vulkan.GetCapabilityParameters(parameters).Success();
    }

    private void AllocateVulkanParameters(NGXParameter* parameters)
    {
        Ngx.Vulkan.AllocateParameters(parameters).Success();
    }

    private void DestroyVulkanParameters(NGXParameter parameters)
    {
        Ngx.Vulkan.DestroyParameters(parameters).Success();
    }

    private void ReleaseVulkanFeature(NGXHandle handle)
    {
        Ngx.Vulkan.ReleaseFeature(handle).Success();
    }

    private void InitializeVulkanResources()
    {
        delegate* unmanaged<nint, byte*, nint> getDeviceProcAddr = (delegate* unmanaged<nint, byte*, nint>)Context.GetNativeObject(NativeObjectType.VulkanGetDeviceProcAddr);

        vkCreateImageView = (delegate* unmanaged<nint, void*, void*, nint*, int>)GetProcedureAddress("vkCreateImageView");
        vkDestroyImageView = (delegate* unmanaged<nint, nint, void*, void>)GetProcedureAddress("vkDestroyImageView");
        vkDestroyDescriptorSetLayout = (delegate* unmanaged<nint, nint, void*, void>)GetProcedureAddress("vkDestroyDescriptorSetLayout");
        vkDestroyPipelineLayout = (delegate* unmanaged<nint, nint, void*, void>)GetProcedureAddress("vkDestroyPipelineLayout");
        vkDestroyDescriptorPool = (delegate* unmanaged<nint, nint, void*, void>)GetProcedureAddress("vkDestroyDescriptorPool");
        vkCmdBindDescriptorSets = (delegate* unmanaged<nint, uint, nint, uint, uint, nint*, uint, uint*, void>)GetProcedureAddress("vkCmdBindDescriptorSets");

        delegate* unmanaged<nint, void*, void*, nint*, int> vkCreateDescriptorSetLayout = (delegate* unmanaged<nint, void*, void*, nint*, int>)GetProcedureAddress("vkCreateDescriptorSetLayout");
        delegate* unmanaged<nint, void*, void*, nint*, int> vkCreatePipelineLayout = (delegate* unmanaged<nint, void*, void*, nint*, int>)GetProcedureAddress("vkCreatePipelineLayout");
        delegate* unmanaged<nint, void*, void*, nint*, int> vkCreateDescriptorPool = (delegate* unmanaged<nint, void*, void*, nint*, int>)GetProcedureAddress("vkCreateDescriptorPool");
        delegate* unmanaged<nint, void*, nint*, int> vkAllocateDescriptorSets = (delegate* unmanaged<nint, void*, nint*, int>)GetProcedureAddress("vkAllocateDescriptorSets");

        VkDescriptorSetLayoutCreateInfo descriptorSetLayoutCreateInfo = new() { SType = DescriptorSetLayoutCreateInfoType };

        nint setLayout;
        vkCreateDescriptorSetLayout(Device, &descriptorSetLayoutCreateInfo, null, &setLayout).Success();

        VkPipelineLayoutCreateInfo pipelineLayoutCreateInfo = new()
        {
            SType = PipelineLayoutCreateInfoType,
            SetLayoutCount = 1,
            PSetLayouts = &setLayout
        };

        nint layout;
        vkCreatePipelineLayout(Device, &pipelineLayoutCreateInfo, null, &layout).Success();

        VkDescriptorPoolCreateInfo descriptorPoolCreateInfo = new()
        {
            SType = DescriptorPoolCreateInfoType,
            MaxSets = 1
        };

        nint pool;
        vkCreateDescriptorPool(Device, &descriptorPoolCreateInfo, null, &pool).Success();

        VkDescriptorSetAllocateInfo descriptorSetAllocateInfo = new()
        {
            SType = DescriptorSetAllocateInfoType,
            DescriptorPool = pool,
            DescriptorSetCount = 1,
            PSetLayouts = &setLayout
        };

        nint set;
        vkAllocateDescriptorSets(Device, &descriptorSetAllocateInfo, &set).Success();

        descriptorSetLayout = setLayout;
        pipelineLayout = layout;
        descriptorPool = pool;
        descriptorSet = set;

        nint GetProcedureAddress(string name)
        {
            using ZenithMarshal.Scope scope = new();

            return getDeviceProcAddr(Device, (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8));
        }
    }

    private void BindVulkanEmptyDescriptorSet(nint commandList)
    {
        nint set = descriptorSet;
        vkCmdBindDescriptorSets(commandList, ComputeBindPoint, pipelineLayout, 0, 1, &set, 0, null);
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

    private void DestroyVulkanResources()
    {
        foreach (DLSSBinding binding in bindings.Values)
        {
            DestroyVulkanBinding(binding);
        }

        vkDestroyDescriptorPool(Device, descriptorPool, null);
        vkDestroyPipelineLayout(Device, pipelineLayout, null);
        vkDestroyDescriptorSetLayout(Device, descriptorSetLayout, null);
    }

    private void ShutdownVulkan()
    {
        Ngx.Vulkan.Shutdown1(Device).Success();
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
