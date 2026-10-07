using System.Runtime.InteropServices;
using NGX.NET;
using Ngx = NGX.NET.NGX;

namespace Zenith.NET.Extensions.DLSS;

internal unsafe class DLSSContext : DisposableObject
{
    private const string ProjectId = "b5b58279-25f8-4d7f-a9d1-e943a85c277c";

    private readonly Dictionary<Texture, nint> textureBindings = [];
    private readonly void* projectId;
    private readonly void* engineVersion;
    private readonly void* dataPath;
    private readonly void** paths;
    private readonly NGXParameter* capabilityParameters;
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

    private uint referenceCount;

    public DLSSContext(GraphicsContext context)
    {
        Context = context;
        IsVulkan = context.GraphicsApi is GraphicsApi.Vulkan;

        if (context.GraphicsApi is not (GraphicsApi.DirectX12 or GraphicsApi.Vulkan) || !(OperatingSystem.IsWindows() || OperatingSystem.IsLinux()) || RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64))
        {
            return;
        }

        using Lock.Scope _ = Lock.EnterScope();

        Device = context.GetNativeObject(IsVulkan ? NativeObjectType.VulkanDevice : NativeObjectType.D3D12Device);

        projectId = NGXMarshal.StringToPtr(ProjectId, NGXEncoding.Utf8);
        engineVersion = NGXMarshal.StringToPtr(typeof(GraphicsContext).Assembly.GetName().Version!.ToString(), NGXEncoding.Utf8);
        dataPath = NGXMarshal.StringToPtr(Path.GetTempPath(), NGXEncoding.NativeWide);
        paths = (void**)NativeMemory.Alloc((nuint)sizeof(nint));
        *paths = NGXMarshal.StringToPtr(Ngx.RuntimeDirectory, NGXEncoding.NativeWide);

        NGXFeatureCommonInfo featureInfo = new() { PathListInfo = new() { Path = paths, Length = 1 } };

        NGXResult result;

        if (IsVulkan)
        {
            result = Ngx.Vulkan.InitWithProjectID((sbyte*)projectId,
                                                  NGXEngineType.CUSTOM,
                                                  (sbyte*)engineVersion,
                                                  dataPath,
                                                  context.GetNativeObject(NativeObjectType.VulkanInstance),
                                                  context.GetNativeObject(NativeObjectType.VulkanPhysicalDevice),
                                                  Device,
                                                  (delegate* unmanaged[Cdecl]<nint, sbyte*, delegate* unmanaged[Cdecl]<void>>)context.GetNativeObject(NativeObjectType.VulkanGetInstanceProcAddr),
                                                  (delegate* unmanaged[Cdecl]<nint, sbyte*, delegate* unmanaged[Cdecl]<void>>)context.GetNativeObject(NativeObjectType.VulkanGetDeviceProcAddr),
                                                  &featureInfo,
                                                  NGXVersion.API);
        }
        else
        {
            result = Ngx.D3D12.InitWithProjectID((sbyte*)projectId, NGXEngineType.CUSTOM, (sbyte*)engineVersion, dataPath, Device, &featureInfo, NGXVersion.API);
        }

        result.Success();

        if (Ngx.Failed(result))
        {
            return;
        }

        NGXParameter* parameters = null;

        (IsVulkan ? Ngx.Vulkan.GetCapabilityParameters(&parameters) : Ngx.D3D12.GetCapabilityParameters(&parameters)).Success();

        capabilityParameters = parameters;

        Capabilities = new(GetI(parameters, Ngx.ParameterSuperSamplingAvailable) is not 0,
                           GetI(parameters, Ngx.ParameterSuperSamplingDenoisingAvailable) is not 0,
                           GetI(parameters, Ngx.ParameterFrameGenerationAvailable) is not 0);

        IsInitialized = true;

        if (!IsVulkan)
        {
            return;
        }

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

        VkDescriptorSetLayoutCreateInfo descriptorSetLayoutCreateInfo = new() { SType = 32 };

        nint setLayout;
        vkCreateDescriptorSetLayout(Device, &descriptorSetLayoutCreateInfo, null, &setLayout);

        VkPipelineLayoutCreateInfo pipelineLayoutCreateInfo = new() { SType = 30, SetLayoutCount = 1, PSetLayouts = &setLayout };

        nint layout;
        vkCreatePipelineLayout(Device, &pipelineLayoutCreateInfo, null, &layout);

        VkDescriptorPoolCreateInfo descriptorPoolCreateInfo = new() { SType = 33, MaxSets = 1 };

        nint pool;
        vkCreateDescriptorPool(Device, &descriptorPoolCreateInfo, null, &pool);

        VkDescriptorSetAllocateInfo descriptorSetAllocateInfo = new() { SType = 34, DescriptorPool = pool, DescriptorSetCount = 1, PSetLayouts = &setLayout };

        nint set;
        vkAllocateDescriptorSets(Device, &descriptorSetAllocateInfo, &set);

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

    public static Lock Lock { get; } = new();

    public GraphicsContext Context { get; }

    public bool IsVulkan { get; }

    public nint Device { get; }

    public bool IsInitialized { get; }

    public DLSSCapabilities Capabilities { get; }

    public static nint D3D12Resource(DLSSBinding binding)
    {
        return binding.Texture?.GetNativeObject(NativeObjectType.D3D12Resource) ?? 0;
    }

    public void AddReference()
    {
        referenceCount++;
    }

    public bool RemoveReference()
    {
        return --referenceCount is 0;
    }

    public DLSSOptimalSettings GetOptimalSettings(NGXFeature feature, uint outputWidth, uint outputHeight, DLSSMode mode)
    {
        if (!IsInitialized)
        {
            return default;
        }

        using Lock.Scope _ = Lock.EnterScope();

        uint inputWidth = 0;
        uint inputHeight = 0;
        uint maxInputWidth = 0;
        uint maxInputHeight = 0;
        uint minInputWidth = 0;
        uint minInputHeight = 0;
        float sharpness = 0.0f;

        NGXPerfQualityValue perfQualityValue = DLSSFormats.PerfQualityValue(mode);

        if (feature is NGXFeature.RayReconstruction)
        {
            Ngx.DLSSD.GetOptimalSettings(capabilityParameters, outputWidth, outputHeight, perfQualityValue, &inputWidth, &inputHeight, &maxInputWidth, &maxInputHeight, &minInputWidth, &minInputHeight, &sharpness).Success();
        }
        else
        {
            Ngx.DLSS.GetOptimalSettings(capabilityParameters, outputWidth, outputHeight, perfQualityValue, &inputWidth, &inputHeight, &maxInputWidth, &maxInputHeight, &minInputWidth, &minInputHeight, &sharpness).Success();
        }

        return new(inputWidth, inputHeight, minInputWidth, minInputHeight, maxInputWidth, maxInputHeight);
    }

    public NGXParameter* AllocateParameters()
    {
        if (!IsInitialized)
        {
            return null;
        }

        using Lock.Scope _ = Lock.EnterScope();

        NGXParameter* parameters = null;

        (IsVulkan ? Ngx.Vulkan.AllocateParameters(&parameters) : Ngx.D3D12.AllocateParameters(&parameters)).Success();

        SetUI(parameters, Ngx.ParameterFreeMemOnReleaseFeature, 1);

        return parameters;
    }

    public void SetUI(NGXParameter* parameters, string name, uint value)
    {
        if (!IsInitialized)
        {
            return;
        }

        using Lock.Scope _ = Lock.EnterScope();

        void* pointer = NGXMarshal.StringToPtr(name, NGXEncoding.Utf8);

        Ngx.Parameter.SetUI(parameters, (sbyte*)pointer, value);

        NGXMarshal.Free(pointer);
    }

    public void DestroyParameters(NGXParameter* parameters)
    {
        if (!IsInitialized)
        {
            return;
        }

        using Lock.Scope _ = Lock.EnterScope();

        (IsVulkan ? Ngx.Vulkan.DestroyParameters(parameters) : Ngx.D3D12.DestroyParameters(parameters)).Success();
    }

    public void ReleaseFeature(NGXHandle* handle)
    {
        using Lock.Scope _ = Lock.EnterScope();

        (IsVulkan ? Ngx.Vulkan.ReleaseFeature(handle) : Ngx.D3D12.ReleaseFeature(handle)).Success();
    }

    public nint CommandList(CommandBuffer commandBuffer)
    {
        return commandBuffer.GetNativeObject(IsVulkan ? NativeObjectType.VulkanCommandBuffer : NativeObjectType.D3D12GraphicsCommandList);
    }

    public void BindEmptyDescriptorSet(nint commandList)
    {
        if (!IsVulkan)
        {
            return;
        }

        nint set = descriptorSet;

        vkCmdBindDescriptorSets(commandList, 1, pipelineLayout, 0, 1, &set, 0, null);
    }

    public void RemoveDestroyedBindings()
    {
        foreach ((Texture texture, nint view) in textureBindings)
        {
            if (texture.IsDisposed)
            {
                vkDestroyImageView(Device, view, null);

                textureBindings.Remove(texture);
            }
        }
    }

    public NGXResourceVK* VulkanResource(NGXResourceVK* resource, DLSSBinding binding, bool readWrite)
    {
        if (binding.Texture is not Texture texture)
        {
            return null;
        }

        (uint format, uint aspectFlags) = DLSSFormats.Vulkan(texture.Desc.Format);

        nint image = texture.GetNativeObject(NativeObjectType.VulkanImage);

        NGXVkImageSubresourceRange subresourceRange = new() { AspectMask = aspectFlags, LevelCount = 1, LayerCount = 1 };

        if (!textureBindings.TryGetValue(texture, out nint view))
        {
            VkImageViewCreateInfo createInfo = new()
            {
                SType = 15,
                Image = image,
                ViewType = 1,
                Format = format,
                SubresourceRange = subresourceRange
            };

            vkCreateImageView(Device, &createInfo, null, &view);

            textureBindings[texture] = view;
        }

        *resource = new()
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
            Type = NGXResourceVKType.VKIMAGEVIEW,
            ReadWrite = readWrite
        };

        return resource;
    }

    protected override void Destroy()
    {
        if (IsInitialized)
        {
            using Lock.Scope _ = Lock.EnterScope();

            if (IsVulkan)
            {
                foreach (nint view in textureBindings.Values)
                {
                    vkDestroyImageView(Device, view, null);
                }

                vkDestroyDescriptorPool(Device, descriptorPool, null);
                vkDestroyPipelineLayout(Device, pipelineLayout, null);
                vkDestroyDescriptorSetLayout(Device, descriptorSetLayout, null);
            }

            DestroyParameters(capabilityParameters);

            (IsVulkan ? Ngx.Vulkan.Shutdown1(Device) : Ngx.D3D12.Shutdown1(Device)).Success();
        }

        if (paths is not null)
        {
            NGXMarshal.Free(*paths);
            NativeMemory.Free(paths);
            NGXMarshal.Free(dataPath);
            NGXMarshal.Free(engineVersion);
            NGXMarshal.Free(projectId);
        }
    }

    private static int GetI(NGXParameter* parameters, string name)
    {
        void* pointer = NGXMarshal.StringToPtr(name, NGXEncoding.Utf8);

        int value = 0;

        Ngx.Parameter.GetI(parameters, (sbyte*)pointer, &value).Success();

        NGXMarshal.Free(pointer);

        return value;
    }
}

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

file unsafe struct VkDescriptorSetLayoutCreateInfo
{
    public uint SType;

    public void* PNext;

    public uint Flags;

    public uint BindingCount;

    public void* PBindings;
}

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

file unsafe struct VkDescriptorPoolCreateInfo
{
    public uint SType;

    public void* PNext;

    public uint Flags;

    public uint MaxSets;

    public uint PoolSizeCount;

    public void* PPoolSizes;
}

file unsafe struct VkDescriptorSetAllocateInfo
{
    public uint SType;

    public void* PNext;

    public nint DescriptorPool;

    public uint DescriptorSetCount;

    public nint* PSetLayouts;
}
