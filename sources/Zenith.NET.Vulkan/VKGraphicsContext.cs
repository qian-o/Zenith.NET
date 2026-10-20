using Silk.NET.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.ANDROID;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Vulkan.Extensions.NVX;

namespace Zenith.NET.Vulkan;

internal unsafe class VKGraphicsContext(bool useValidationLayer) : GraphicsContext(GraphicsApi.Vulkan, useValidationLayer)
{
    private static readonly string[] InstanceLayers =
    [
        "VK_LAYER_KHRONOS_validation"
    ];

    private static readonly string[] InstanceExtensions =
    [
        ExtDebugUtils.ExtensionName,
        ExtMetalSurface.ExtensionName,
        KhrAndroidSurface.ExtensionName,
        KhrSurface.ExtensionName,
        KhrWaylandSurface.ExtensionName,
        KhrWin32Surface.ExtensionName,
        KhrXlibSurface.ExtensionName
    ];

    private static readonly string[] DeviceExtensions =
    [
        AndroidExternalMemoryAndroidHardwareBuffer.ExtensionName,
        ExtDescriptorHeap.ExtensionName,
        ExtMeshShader.ExtensionName,
        ExtMetalObjects.ExtensionName,
        KhrAccelerationStructure.ExtensionName,
        KhrDeferredHostOperations.ExtensionName,
        KhrExternalMemoryFd.ExtensionName,
        KhrExternalMemoryWin32.ExtensionName,
        KhrFragmentShadingRate.ExtensionName,
        KhrRayQuery.ExtensionName,
        KhrShaderUntypedPointers.ExtensionName,
        KhrSwapchain.ExtensionName,
        NvxBinaryImport.ExtensionName,
        NvxImageViewHandle.ExtensionName
    ];

    public Instance Instance;

    public PhysicalDevice PhysicalDevice;

    public Device Device;

    public Vk Vk { get; } = Vk.GetApi();

    public ExtDebugUtils? DebugUtils { get; private set; }

    public ExtMetalSurface? MetalSurface { get; private set; }

    public KhrAndroidSurface? AndroidSurface { get; private set; }

    public KhrSurface? Surface { get; private set; }

    public KhrWaylandSurface? WaylandSurface { get; private set; }

    public KhrWin32Surface? Win32Surface { get; private set; }

    public KhrXlibSurface? XlibSurface { get; private set; }

    public AndroidExternalMemoryAndroidHardwareBuffer? ExternalMemoryAndroidHardwareBuffer { get; private set; }

    public ExtDescriptorHeap? DescriptorHeap { get; private set; }

    public ExtMeshShader? MeshShader { get; private set; }

    public KhrAccelerationStructure? AccelerationStructure { get; private set; }

    public KhrSwapchain? Swapchain { get; private set; }

    public QueueFamilies QueueFamilies { get; private set; }

    public VKDescriptorHeap ResourceHeap => field ??= VKDescriptorHeap.CreateResourceHeap(this, 200000, 800000);

    public VKDescriptorHeap SamplerHeap => field ??= VKDescriptorHeap.CreateSamplerHeap(this, 2048);

    public uint FindMemoryTypeIndex(uint memoryTypeBits, MemoryResidency residency)
    {
        PhysicalDeviceMemoryProperties properties;
        Vk.GetPhysicalDeviceMemoryProperties(PhysicalDevice, &properties);

        MemoryPropertyFlags[] candidates = residency switch
        {
            MemoryResidency.GpuOnly =>
            [
                MemoryPropertyFlags.DeviceLocalBit
            ],
            MemoryResidency.CpuWriteOnly =>
            [
                MemoryPropertyFlags.DeviceLocalBit | MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit
            ],
            MemoryResidency.CpuReadOnly =>
            [
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit | MemoryPropertyFlags.HostCachedBit,
                MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit
            ],
            _ => []
        };

        foreach (MemoryPropertyFlags propertyFlags in candidates)
        {
            for (uint index = 0; index < properties.MemoryTypeCount; index++)
            {
                if ((memoryTypeBits & (1u << (int)index)) is not 0 && (properties.MemoryTypes[(int)index].PropertyFlags & propertyFlags) == propertyFlags)
                {
                    return index;
                }
            }
        }

        return 0;
    }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return type switch
        {
            NativeObjectType.VulkanDevice => Device.Handle,
            NativeObjectType.VulkanGetDeviceProcAddr => Vk.GetInstanceProcAddr(Instance, "vkGetDeviceProcAddr"),
            NativeObjectType.VulkanGetInstanceProcAddr => Vk.GetInstanceProcAddr(default, "vkGetInstanceProcAddr"),
            NativeObjectType.VulkanInstance => Instance.Handle,
            NativeObjectType.VulkanPhysicalDevice => PhysicalDevice.Handle,
            _ => default
        };
    }

    protected override void Initialize(bool useValidationLayer,
                                       out Capabilities capabilities,
                                       out CommandQueue graphicsQueue,
                                       out CommandQueue computeQueue,
                                       out CommandQueue transferQueue,
                                       out CommandQueue presentQueue,
                                       out ValidationLayer? validationLayer)
    {
        Version32 apiVersion = new(1, 4, 0);

        using ZenithMarshal.Scope scope = new();

        // Create instance
        {
            uint extensionCount = 0;
            Vk.EnumerateInstanceExtensionProperties(default(byte*), &extensionCount, default).Success();

            ExtensionProperties* extensions = (ExtensionProperties*)ZenithMarshal.Allocate<ExtensionProperties>(scope, extensionCount);
            Vk.EnumerateInstanceExtensionProperties(default(byte*), &extensionCount, extensions).Success();

            string[] enabledExtensions = [.. new ReadOnlySpan<ExtensionProperties>(extensions, (int)extensionCount).ToArray().Select(static item => ZenithMarshal.StringFromPointer((nint)item.ExtensionName, StringEncoding.UTF8))];
            enabledExtensions = [.. enabledExtensions.Intersect(InstanceExtensions)];

            ApplicationInfo applicationInfo = new()
            {
                SType = StructureType.ApplicationInfo,
                ApiVersion = apiVersion
            };

            InstanceCreateInfo createInfo = new()
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &applicationInfo,
                EnabledExtensionCount = (uint)enabledExtensions.Length,
                PpEnabledExtensionNames = (byte**)ZenithMarshal.StringArrayToPointer(scope, enabledExtensions, StringEncoding.UTF8)
            };

            if (useValidationLayer)
            {
                uint layerCount = 0;
                Vk.EnumerateInstanceLayerProperties(&layerCount, default).Success();

                LayerProperties* layers = (LayerProperties*)ZenithMarshal.Allocate<LayerProperties>(scope, layerCount);
                Vk.EnumerateInstanceLayerProperties(&layerCount, layers).Success();

                string[] enabledLayers = [.. new ReadOnlySpan<LayerProperties>(layers, (int)layerCount).ToArray().Select(static item => ZenithMarshal.StringFromPointer((nint)item.LayerName, StringEncoding.UTF8))];
                enabledLayers = [.. enabledLayers.Intersect(InstanceLayers)];

                createInfo.EnabledLayerCount = (uint)enabledLayers.Length;
                createInfo.PpEnabledLayerNames = (byte**)ZenithMarshal.StringArrayToPointer(scope, enabledLayers, StringEncoding.UTF8);
            }

            Vk.CreateInstance(&createInfo, default, out Instance).Success();

            LamdaNativeContext context = new(proc => Vk.GetInstanceProcAddr(Instance, (byte*)ZenithMarshal.StringToPointer(scope, proc, StringEncoding.UTF8)));

            DebugUtils = enabledExtensions.Contains(ExtDebugUtils.ExtensionName) ? new(context) : null;
            MetalSurface = enabledExtensions.Contains(ExtMetalSurface.ExtensionName) ? new(context) : null;
            AndroidSurface = enabledExtensions.Contains(KhrAndroidSurface.ExtensionName) ? new(context) : null;
            Surface = enabledExtensions.Contains(KhrSurface.ExtensionName) ? new(context) : null;
            WaylandSurface = enabledExtensions.Contains(KhrWaylandSurface.ExtensionName) ? new(context) : null;
            Win32Surface = enabledExtensions.Contains(KhrWin32Surface.ExtensionName) ? new(context) : null;
            XlibSurface = enabledExtensions.Contains(KhrXlibSurface.ExtensionName) ? new(context) : null;
        }

        QueueSelection queueSelection;

        // Select physical device and create logical device
        {
            uint physicalDeviceCount = 0;
            Vk.EnumeratePhysicalDevices(Instance, &physicalDeviceCount, default).Success();

            PhysicalDevice* physicalDevices = (PhysicalDevice*)ZenithMarshal.Allocate<PhysicalDevice>(scope, physicalDeviceCount);
            Vk.EnumeratePhysicalDevices(Instance, &physicalDeviceCount, physicalDevices).Success();

            ulong bestScore = 0;
            foreach (PhysicalDevice physicalDevice in new ReadOnlySpan<PhysicalDevice>(physicalDevices, (int)physicalDeviceCount))
            {
                PhysicalDeviceProperties properties;
                Vk.GetPhysicalDeviceProperties(physicalDevice, &properties);

                if (properties.ApiVersion < apiVersion)
                {
                    continue;
                }

                ulong score = properties.DeviceType switch
                {
                    PhysicalDeviceType.DiscreteGpu => 40000,
                    PhysicalDeviceType.IntegratedGpu => 30000,
                    PhysicalDeviceType.VirtualGpu => 20000,
                    PhysicalDeviceType.Other => 10000,
                    _ => 0
                };

                PhysicalDeviceFeatures features;
                Vk.GetPhysicalDeviceFeatures(physicalDevice, &features);

                if (features.MultiDrawIndirect)
                {
                    score += 1000;
                }

                if (features.DrawIndirectFirstInstance)
                {
                    score += 1000;
                }

                if (features.IndependentBlend)
                {
                    score += 500;
                }

                if (features.SamplerAnisotropy)
                {
                    score += 500;
                }

                if (features.TextureCompressionBC)
                {
                    score += 500;
                }

                if (PhysicalDevice.Handle is 0 || score > bestScore)
                {
                    bestScore = score;

                    PhysicalDevice = physicalDevice;
                }
            }

            if (PhysicalDevice.Handle is 0)
            {
                throw new NotSupportedException("This device does not support Vulkan 1.4 or higher.");
            }

            queueSelection = new(this, scope);

            uint extensionCount = 0;
            Vk.EnumerateDeviceExtensionProperties(PhysicalDevice, default(byte*), &extensionCount, default).Success();

            ExtensionProperties* extensions = (ExtensionProperties*)ZenithMarshal.Allocate<ExtensionProperties>(scope, extensionCount);
            Vk.EnumerateDeviceExtensionProperties(PhysicalDevice, default(byte*), &extensionCount, extensions).Success();

            string[] enabledExtensions = [.. new ReadOnlySpan<ExtensionProperties>(extensions, (int)extensionCount).ToArray().Select(static item => ZenithMarshal.StringFromPointer((nint)item.ExtensionName, StringEncoding.UTF8))];
            enabledExtensions = [.. enabledExtensions.Intersect(DeviceExtensions)];

            DeviceCreateInfo createInfo = new()
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = (uint)queueSelection.CreateInfos.Length,
                PQueueCreateInfos = (DeviceQueueCreateInfo*)ZenithMarshal.AllocateAndFill(scope, queueSelection.CreateInfos),
                EnabledExtensionCount = (uint)enabledExtensions.Length,
                PpEnabledExtensionNames = (byte**)ZenithMarshal.StringArrayToPointer(scope, enabledExtensions, StringEncoding.UTF8)
            };

            createInfo.AddNext(out PhysicalDeviceFeatures2 features2);
            createInfo.AddNext(out PhysicalDeviceVulkan11Features _);
            createInfo.AddNext(out PhysicalDeviceVulkan12Features _);
            createInfo.AddNext(out PhysicalDeviceVulkan13Features _);
            createInfo.AddNext(out PhysicalDeviceVulkan14Features _);

            if (enabledExtensions.Contains(ExtDescriptorHeap.ExtensionName))
            {
                createInfo.AddNext(out PhysicalDeviceDescriptorHeapFeaturesEXT _);
            }

            if (enabledExtensions.Contains(ExtMeshShader.ExtensionName))
            {
                createInfo.AddNext(out PhysicalDeviceMeshShaderFeaturesEXT _);
            }

            if (enabledExtensions.Contains(KhrAccelerationStructure.ExtensionName))
            {
                createInfo.AddNext(out PhysicalDeviceAccelerationStructureFeaturesKHR _);
            }

            if (enabledExtensions.Contains(KhrFragmentShadingRate.ExtensionName))
            {
                createInfo.AddNext(out PhysicalDeviceFragmentShadingRateFeaturesKHR _);
            }

            if (enabledExtensions.Contains(KhrRayQuery.ExtensionName))
            {
                createInfo.AddNext(out PhysicalDeviceRayQueryFeaturesKHR _);
            }

            if (enabledExtensions.Contains(KhrShaderUntypedPointers.ExtensionName))
            {
                createInfo.AddNext(out PhysicalDeviceShaderUntypedPointersFeaturesKHR _);
            }

            Vk.GetPhysicalDeviceFeatures2(PhysicalDevice, &features2);

            Vk.CreateDevice(PhysicalDevice, &createInfo, default, out Device).Success();

            LamdaNativeContext context = new(proc => Vk.GetDeviceProcAddr(Device, (byte*)ZenithMarshal.StringToPointer(scope, proc, StringEncoding.UTF8)));

            ExternalMemoryAndroidHardwareBuffer = enabledExtensions.Contains(AndroidExternalMemoryAndroidHardwareBuffer.ExtensionName) ? new(context) : null;
            DescriptorHeap = enabledExtensions.Contains(ExtDescriptorHeap.ExtensionName) ? new(context) : null;
            MeshShader = enabledExtensions.Contains(ExtMeshShader.ExtensionName) ? new(context) : null;
            AccelerationStructure = enabledExtensions.Contains(KhrAccelerationStructure.ExtensionName) ? new(context) : null;
            Swapchain = enabledExtensions.Contains(KhrSwapchain.ExtensionName) ? new(context) : null;

            QueueFamilies = new([.. queueSelection.CreateInfos.Select(static item => item.QueueFamilyIndex)]);
        }

        capabilities = new VKCapabilities(this);
        graphicsQueue = new VKCommandQueue(this, CommandQueueType.Graphics, queueSelection.Graphics.QueueFamilyIndex, queueSelection.Graphics.QueueIndex);
        computeQueue = new VKCommandQueue(this, CommandQueueType.Compute, queueSelection.Compute.QueueFamilyIndex, queueSelection.Compute.QueueIndex);
        transferQueue = new VKCommandQueue(this, CommandQueueType.Transfer, queueSelection.Transfer.QueueFamilyIndex, queueSelection.Transfer.QueueIndex);
        presentQueue = new VKCommandQueue(this, CommandQueueType.Present, queueSelection.Present.QueueFamilyIndex, queueSelection.Present.QueueIndex);
        validationLayer = useValidationLayer ? new VKValidationLayer(this) : null;
    }

    protected override SwapChain CreateSwapChainImpl(SwapChainDesc desc)
    {
        return new VKSwapChain(this, desc);
    }

    protected override Heap CreateHeapImpl(HeapDesc desc)
    {
        return new VKHeap(this, desc);
    }

    protected override SizeAndAlignment GetSizeAndAlignmentImpl(BufferDesc desc)
    {
        BufferCreateInfo createInfo = VKBuffer.CreateInfo(desc, Capabilities, QueueFamilies);

        DeviceBufferMemoryRequirements requirements = new()
        {
            SType = StructureType.DeviceBufferMemoryRequirements,
            PCreateInfo = &createInfo
        };

        MemoryRequirements2 requirements2 = new() { SType = StructureType.MemoryRequirements2 };

        Vk.GetDeviceBufferMemoryRequirements(Device, &requirements, &requirements2);

        return new(requirements2.MemoryRequirements.Size, requirements2.MemoryRequirements.Alignment);
    }

    protected override SizeAndAlignment GetSizeAndAlignmentImpl(TextureDesc desc)
    {
        ImageCreateInfo createInfo = VKTexture.CreateInfo(desc, QueueFamilies);

        DeviceImageMemoryRequirements requirements = new()
        {
            SType = StructureType.DeviceImageMemoryRequirements,
            PCreateInfo = &createInfo
        };

        MemoryRequirements2 requirements2 = new() { SType = StructureType.MemoryRequirements2 };

        Vk.GetDeviceImageMemoryRequirements(Device, &requirements, &requirements2);

        PhysicalDeviceProperties properties;
        Vk.GetPhysicalDeviceProperties(PhysicalDevice, &properties);

        return new(requirements2.MemoryRequirements.Size, Math.Max(requirements2.MemoryRequirements.Alignment, properties.Limits.BufferImageGranularity));
    }

    protected override Buffer CreateBufferImpl(BufferDesc desc)
    {
        return new VKBuffer(this, desc);
    }

    protected override BufferView CreateBufferViewImpl(BufferViewDesc desc)
    {
        return new VKBufferView(this, desc);
    }

    protected override Texture CreateTextureImpl(TextureDesc desc)
    {
        return new VKTexture(this, desc);
    }

    protected override Texture CreateTextureImpl(TextureDesc desc, NativeTextureType nativeTextureType, nint nativeTexture)
    {
        ImageCreateInfo createInfo = VKTexture.CreateInfo(desc, QueueFamilies);

        if (nativeTextureType is NativeTextureType.MTLSharedTextureHandle or NativeTextureType.IOSurfaceRef)
        {
            switch (nativeTextureType)
            {
                case NativeTextureType.MTLSharedTextureHandle:
                    {
                        createInfo.AddNext(out ImportMetalTextureInfoEXT importInfo);
                        importInfo.Plane = ImageAspectFlags.ColorBit;
                        importInfo.MtlTexture = nativeTexture;
                    }
                    break;

                case NativeTextureType.IOSurfaceRef:
                    {
                        createInfo.AddNext(out ImportMetalIOSurfaceInfoEXT importInfo);
                        importInfo.IoSurface = nativeTexture;
                    }
                    break;
            }

            Vk.CreateImage(Device, &createInfo, default, out Image metalImage).Success();

            return new VKTexture(this, desc, metalImage, new(default, 0, true, false));
        }
        else
        {
            createInfo.AddNext(out ExternalMemoryImageCreateInfo externalMemoryImageCreateInfo);
            externalMemoryImageCreateInfo.HandleTypes = VKFormats.Vulkan(nativeTextureType);

            Vk.CreateImage(Device, &createInfo, default, out Image image).Success();

            ImageMemoryRequirementsInfo2 requirementsInfo2 = new()
            {
                SType = StructureType.ImageMemoryRequirementsInfo2,
                Image = image
            };

            MemoryRequirements2 requirements2 = new() { SType = StructureType.MemoryRequirements2 };
            Vk.GetImageMemoryRequirements2(Device, &requirementsInfo2, &requirements2);

            MemoryAllocateInfo allocateInfo = new()
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = requirements2.MemoryRequirements.Size,
                MemoryTypeIndex = FindMemoryTypeIndex(requirements2.MemoryRequirements.MemoryTypeBits, MemoryResidency.GpuOnly)
            };

            allocateInfo.AddNext(out MemoryDedicatedAllocateInfo dedicatedAllocateInfo);
            dedicatedAllocateInfo.Image = image;

            switch (nativeTextureType)
            {
                case NativeTextureType.D3D11TextureNtHandle:
                case NativeTextureType.D3D12ResourceNtHandle:
                case NativeTextureType.VulkanOpaqueNtHandle:
                    {
                        allocateInfo.AddNext(out ImportMemoryWin32HandleInfoKHR importInfo);
                        importInfo.HandleType = VKFormats.Vulkan(nativeTextureType);
                        importInfo.Handle = nativeTexture;
                    }
                    break;

                case NativeTextureType.VulkanOpaquePosixFileDescriptor:
                    {
                        allocateInfo.AddNext(out ImportMemoryFdInfoKHR importInfo);
                        importInfo.HandleType = VKFormats.Vulkan(nativeTextureType);
                        importInfo.Fd = (int)nativeTexture;
                    }
                    break;

                case NativeTextureType.VulkanAndroidHardwareBuffer:
                    {
                        AndroidHardwareBufferPropertiesANDROID properties = new() { SType = StructureType.AndroidHardwareBufferPropertiesAndroid };
                        ExternalMemoryAndroidHardwareBuffer!.GetAndroidHardwareBufferProperties(Device, (nint*)nativeTexture, &properties).Success();

                        allocateInfo.AllocationSize = properties.AllocationSize;
                        allocateInfo.MemoryTypeIndex = FindMemoryTypeIndex(properties.MemoryTypeBits, MemoryResidency.GpuOnly);

                        allocateInfo.AddNext(out ImportAndroidHardwareBufferInfoANDROID importInfo);
                        importInfo.Buffer = (nint*)nativeTexture;
                    }
                    break;
            }

            Vk.AllocateMemory(Device, &allocateInfo, default, out DeviceMemory deviceMemory).Success();
            Vk.BindImageMemory(Device, image, deviceMemory, 0).Success();

            return new VKTexture(this, desc, image, new(deviceMemory, 0, true, true));
        }
    }

    protected override TextureView CreateTextureViewImpl(TextureViewDesc desc)
    {
        return new VKTextureView(this, desc);
    }

    protected override Sampler CreateSamplerImpl(SamplerDesc desc)
    {
        return new VKSampler(this, desc);
    }

    protected override Shader CreateShaderImpl(ShaderDesc desc)
    {
        return new VKShader(this, desc);
    }

    protected override GraphicsPipeline CreateGraphicsPipelineImpl(GraphicsPipelineDesc desc)
    {
        return new VKGraphicsPipeline(this, desc);
    }

    protected override ComputePipeline CreateComputePipelineImpl(ComputePipelineDesc desc)
    {
        return new VKComputePipeline(this, desc);
    }

    protected override MeshShadingPipeline CreateMeshShadingPipelineImpl(MeshShadingPipelineDesc desc)
    {
        return new VKMeshShadingPipeline(this, desc);
    }

    protected override QueryHeap CreateQueryHeapImpl(QueryHeapDesc desc)
    {
        return new VKQueryHeap(this, desc);
    }

    protected override void Destroy()
    {
        base.Destroy();

        SamplerHeap.Dispose();
        ResourceHeap.Dispose();

        QueueFamilies.Dispose();

        Vk.DestroyDevice(Device, default);
        Vk.DestroyInstance(Instance, default);
        Vk.Dispose();
    }
}

file readonly unsafe struct QueueSelection
{
    public readonly (uint QueueFamilyIndex, uint QueueIndex) Graphics;

    public readonly (uint QueueFamilyIndex, uint QueueIndex) Compute;

    public readonly (uint QueueFamilyIndex, uint QueueIndex) Transfer;

    public readonly (uint QueueFamilyIndex, uint QueueIndex) Present;

    public readonly DeviceQueueCreateInfo[] CreateInfos;

    public QueueSelection(VKGraphicsContext context, ZenithMarshal.Scope scope)
    {
        uint queueFamilyCount = 0;
        context.Vk.GetPhysicalDeviceQueueFamilyProperties(context.PhysicalDevice, &queueFamilyCount, default);

        QueueFamilyProperties* queueFamilies = (QueueFamilyProperties*)ZenithMarshal.Allocate<QueueFamilyProperties>(scope, queueFamilyCount);
        context.Vk.GetPhysicalDeviceQueueFamilyProperties(context.PhysicalDevice, &queueFamilyCount, queueFamilies);

        QueueFamilyState graphicsState = default;
        QueueFamilyState computeState = default;
        QueueFamilyState transferState = default;
        uint index = 0;
        foreach (QueueFamilyProperties queueFamily in new ReadOnlySpan<QueueFamilyProperties>(queueFamilies, (int)queueFamilyCount))
        {
            if (queueFamily.QueueFlags.HasFlag(QueueFlags.GraphicsBit | QueueFlags.ComputeBit | QueueFlags.TransferBit) && queueFamily.QueueCount > graphicsState.QueueCount)
            {
                graphicsState = new(index, queueFamily.QueueCount);
            }
            else if (queueFamily.QueueFlags.HasFlag(QueueFlags.ComputeBit | QueueFlags.TransferBit))
            {
                computeState = new(index, queueFamily.QueueCount);
            }
            else if (queueFamily.QueueFlags.HasFlag(QueueFlags.TransferBit))
            {
                transferState = new(index, queueFamily.QueueCount);
            }

            index++;
        }

        Graphics = Allocate(ref graphicsState)!.Value;
        Present = Allocate(ref graphicsState) ?? Graphics;
        Compute = Allocate(ref computeState) ?? Allocate(ref graphicsState) ?? Graphics;
        Transfer = Allocate(ref transferState) ?? Allocate(ref computeState) ?? Allocate(ref graphicsState) ?? Graphics;

        float* queuePriorities = (float*)ZenithMarshal.AllocateAndFill(scope, [1.0f, 1.0f, 1.0f, 1.0f]);

        CreateInfos = [.. new QueueFamilyState[] { graphicsState, computeState, transferState }.Where(static item => item.AllocatedQueueCount is not 0).Select(item => new DeviceQueueCreateInfo()
        {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = item.Index,
            QueueCount = item.AllocatedQueueCount,
            PQueuePriorities = queuePriorities
        })];

        static (uint QueueFamilyIndex, uint QueueIndex)? Allocate(ref QueueFamilyState state)
        {
            return state.AllocatedQueueCount < state.QueueCount ? (state.Index, state.AllocatedQueueCount++) : null;
        }
    }
}

file struct QueueFamilyState(uint index, uint queueCount)
{
    public readonly uint Index = index;

    public readonly uint QueueCount = queueCount;

    public uint AllocatedQueueCount;
}
