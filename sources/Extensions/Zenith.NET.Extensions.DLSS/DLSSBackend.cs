using System.Runtime.InteropServices;
using NGX.NET;

namespace Zenith.NET.Extensions.DLSS;

internal unsafe class DLSSBackend : DisposableObject
{
    private const uint ImageViewCreateInfoType = 15;
    private const uint PipelineLayoutCreateInfoType = 30;
    private const uint DescriptorSetLayoutCreateInfoType = 32;
    private const uint DescriptorPoolCreateInfoType = 33;
    private const uint DescriptorSetAllocateInfoType = 34;
    private const uint ImageViewType2D = 1;
    private const uint ComputeBindPoint = 1;

    private readonly Dictionary<Texture, DLSSBinding> bindings = [];
    private readonly NGXStrings strings = new();
    private readonly ZenithMarshal.Scope scope = new();
    private readonly NGXParameter capabilityParameters;
    private readonly delegate* unmanaged<nint, void*, void*, nint*, int> vkCreateImageView;
    private readonly delegate* unmanaged<nint, nint, void*, void> vkDestroyImageView;
    private readonly delegate* unmanaged<nint, nint, void*, void> vkDestroyDescriptorSetLayout;
    private readonly delegate* unmanaged<nint, nint, void*, void> vkDestroyPipelineLayout;
    private readonly delegate* unmanaged<nint, nint, void*, void> vkDestroyDescriptorPool;
    private readonly delegate* unmanaged<nint, uint, nint, uint, uint, nint*, uint, uint*, void> vkCmdBindDescriptorSets;
    private readonly nint descriptorSetLayout;
    private readonly nint pipelineLayout;
    private readonly nint descriptorPool;
    private readonly nint descriptorSet;

    public DLSSBackend(GraphicsContext context, Guid project, string version, string dataDirectory)
    {
        Context = context;

        using Lock.Scope _ = Lock.EnterScope();

        Device = context.GraphicsApi switch
        {
            GraphicsApi.DirectX12 => context.GetNativeObject(NativeObjectType.D3D12Device),
            GraphicsApi.Vulkan => context.GetNativeObject(NativeObjectType.VulkanDevice),
            _ => default
        };

        byte* projectId = strings.AllocUtf8(project.ToString("D"));
        byte* engineVersion = strings.AllocUtf8(version);
        void* dataPath = strings.AllocWide(dataDirectory);
        void** paths = (void**)ZenithMarshal.Allocate<nint>(scope, 1);
        *paths = strings.AllocWide(Ngx.RuntimeDirectory);

        NGXFeatureCommonInfo featureInfo = new()
        {
            PathListInfo = new()
            {
                Path = paths,
                Length = 1
            }
        };

        switch (context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                Ngx.D3D12.InitWithProjectID(projectId, NGXEngineType.Custom, engineVersion, dataPath, Device, &featureInfo, NGXVersion.Api).Success();
                break;

            case GraphicsApi.Vulkan:
                Ngx.Vulkan.InitWithProjectID(projectId,
                                             NGXEngineType.Custom,
                                             engineVersion,
                                             dataPath,
                                             context.GetNativeObject(NativeObjectType.VulkanInstance),
                                             context.GetNativeObject(NativeObjectType.VulkanPhysicalDevice),
                                             Device,
                                             (delegate* unmanaged[Cdecl]<nint, byte*, delegate* unmanaged[Cdecl]<void>>)context.GetNativeObject(NativeObjectType.VulkanGetInstanceProcAddr),
                                             (delegate* unmanaged[Cdecl]<nint, byte*, delegate* unmanaged[Cdecl]<void>>)context.GetNativeObject(NativeObjectType.VulkanGetDeviceProcAddr),
                                             &featureInfo,
                                             NGXVersion.Api).Success();
                break;
        }

        fixed (NGXParameter* parameters = &capabilityParameters)
        {
            switch (context.GraphicsApi)
            {
                case GraphicsApi.DirectX12:
                    Ngx.D3D12.GetCapabilityParameters(parameters).Success();
                    break;

                case GraphicsApi.Vulkan:
                    Ngx.Vulkan.GetCapabilityParameters(parameters).Success();
                    break;
            }
        }

        Capabilities = new(GetI(capabilityParameters, Ngx.ParameterSuperSamplingAvailable) is not 0,
                           GetI(capabilityParameters, Ngx.ParameterSuperSamplingDenoisingAvailable) is not 0,
                           GetI(capabilityParameters, Ngx.ParameterFrameGenerationAvailable) is not 0);

        switch (context.GraphicsApi)
        {
            case GraphicsApi.Vulkan:
                {
                    delegate* unmanaged<nint, byte*, nint> getDeviceProcAddr = (delegate* unmanaged<nint, byte*, nint>)context.GetNativeObject(NativeObjectType.VulkanGetDeviceProcAddr);

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
                break;
        }
    }

    public static Lock Lock { get; } = new();

    public GraphicsContext Context { get; }

    public nint Device { get; }

    public DLSSCapabilities Capabilities { get; }

    public DLSSOptimalSettings GetOptimalSettings(NGXFeature feature, uint outputWidth, uint outputHeight, DLSSMode mode)
    {
        using Lock.Scope _ = Lock.EnterScope();

        uint inputWidth = 0;
        uint inputHeight = 0;
        uint maxInputWidth = 0;
        uint maxInputHeight = 0;
        uint minInputWidth = 0;
        uint minInputHeight = 0;
        float sharpness = 0.0f;

        NGXPerfQualityValue perfQualityValue = DLSSFormats.PerfQualityValue(mode);

        switch (feature)
        {
            case NGXFeature.SuperSampling:
                Ngx.DLSS.GetOptimalSettings(capabilityParameters, outputWidth, outputHeight, perfQualityValue, &inputWidth, &inputHeight, &maxInputWidth, &maxInputHeight, &minInputWidth, &minInputHeight, &sharpness).Success();
                break;

            case NGXFeature.RayReconstruction:
                Ngx.DLSSD.GetOptimalSettings(capabilityParameters, outputWidth, outputHeight, perfQualityValue, &inputWidth, &inputHeight, &maxInputWidth, &maxInputHeight, &minInputWidth, &minInputHeight, &sharpness).Success();
                break;
        }

        return new(inputWidth, inputHeight, minInputWidth, minInputHeight, maxInputWidth, maxInputHeight);
    }

    public NGXParameter AllocateParameters()
    {
        using Lock.Scope _ = Lock.EnterScope();

        NGXParameter parameters = default;

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                Ngx.D3D12.AllocateParameters(&parameters).Success();
                break;

            case GraphicsApi.Vulkan:
                Ngx.Vulkan.AllocateParameters(&parameters).Success();
                break;
        }

        SetUI(parameters, Ngx.ParameterFreeMemOnReleaseFeature, 1);

        return parameters;
    }

    public void SetUI(NGXParameter parameters, ReadOnlySpan<byte> name, uint value)
    {
        using Lock.Scope _ = Lock.EnterScope();

        fixed (byte* pointer = name)
        {
            Ngx.Parameter.SetUI(parameters, pointer, value);
        }
    }

    public void DestroyParameters(NGXParameter parameters)
    {
        using Lock.Scope _ = Lock.EnterScope();

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                Ngx.D3D12.DestroyParameters(parameters).Success();
                break;

            case GraphicsApi.Vulkan:
                Ngx.Vulkan.DestroyParameters(parameters).Success();
                break;
        }
    }

    public void ReleaseFeature(NGXHandle handle)
    {
        using Lock.Scope _ = Lock.EnterScope();

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                Ngx.D3D12.ReleaseFeature(handle).Success();
                break;

            case GraphicsApi.Vulkan:
                Ngx.Vulkan.ReleaseFeature(handle).Success();
                break;
        }
    }

    public nint CommandList(CommandBuffer commandBuffer)
    {
        return Context.GraphicsApi switch
        {
            GraphicsApi.DirectX12 => commandBuffer.GetNativeObject(NativeObjectType.D3D12GraphicsCommandList),
            GraphicsApi.Vulkan => commandBuffer.GetNativeObject(NativeObjectType.VulkanCommandBuffer),
            _ => default
        };
    }

    public void BindEmptyDescriptorSet(nint commandList)
    {
        switch (Context.GraphicsApi)
        {
            case GraphicsApi.Vulkan:
                {
                    nint set = descriptorSet;
                    vkCmdBindDescriptorSets(commandList, ComputeBindPoint, pipelineLayout, 0, 1, &set, 0, null);
                }
                break;
        }
    }

    public DLSSBinding Binding(Texture texture)
    {
        using Lock.Scope _ = Lock.EnterScope();

        if (!bindings.TryGetValue(texture, out DLSSBinding binding))
        {
            nint directX12 = default;
            NGXResourceVK vulkan = default;
            NGXDimensions size = new()
            {
                Width = texture.Desc.Width,
                Height = texture.Desc.Height
            };

            switch (Context.GraphicsApi)
            {
                case GraphicsApi.DirectX12:
                    directX12 = texture.GetNativeObject(NativeObjectType.D3D12Resource);
                    break;

                case GraphicsApi.Vulkan:
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

                        vulkan = new()
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
                    }
                    break;
            }

            bindings[texture] = binding = new(directX12, vulkan, size);

            texture.Disposing += (sender, _) =>
            {
                using Lock.Scope scope = Lock.EnterScope();

                Texture disposedTexture = (Texture)sender!;

                if (bindings.Remove(disposedTexture, out DLSSBinding disposedBinding))
                {
                    switch (Context.GraphicsApi)
                    {
                        case GraphicsApi.Vulkan:
                            vkDestroyImageView(Device, disposedBinding.Vulkan.Resource.ImageViewInfo.ImageView, null);
                            break;
                    }
                }
            };
        }

        return binding;
    }

    protected override void Destroy()
    {
        using Lock.Scope _ = Lock.EnterScope();

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.Vulkan:
                foreach (DLSSBinding binding in bindings.Values)
                {
                    vkDestroyImageView(Device, binding.Vulkan.Resource.ImageViewInfo.ImageView, null);
                }

                vkDestroyDescriptorPool(Device, descriptorPool, null);
                vkDestroyPipelineLayout(Device, pipelineLayout, null);
                vkDestroyDescriptorSetLayout(Device, descriptorSetLayout, null);
                break;
        }

        bindings.Clear();

        DestroyParameters(capabilityParameters);

        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                Ngx.D3D12.Shutdown1(Device).Success();
                break;

            case GraphicsApi.Vulkan:
                Ngx.Vulkan.Shutdown1(Device).Success();
                break;
        }

        scope.Dispose();
        strings.Dispose();
    }

    private static int GetI(NGXParameter parameters, ReadOnlySpan<byte> name)
    {
        fixed (byte* pointer = name)
        {
            int value = 0;
            Ngx.Parameter.GetI(parameters, pointer, &value).Success();

            return value;
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
