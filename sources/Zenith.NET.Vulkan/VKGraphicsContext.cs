using Silk.NET.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.ANDROID;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;

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
        KhrExternalFenceFd.ExtensionName,
        KhrExternalMemoryWin32.ExtensionName,
        KhrFragmentShadingRate.ExtensionName,
        KhrRayQuery.ExtensionName,
        KhrShaderUntypedPointers.ExtensionName,
        KhrSwapchain.ExtensionName
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

    public ExtMetalObjects? MetalObjects { get; private set; }

    public KhrAccelerationStructure? AccelerationStructure { get; private set; }

    public KhrDeferredHostOperations? DeferredHostOperations { get; private set; }

    public KhrExternalFenceFd? ExternalFenceFd { get; private set; }

    public KhrExternalMemoryWin32? ExternalMemoryWin32 { get; private set; }

    public KhrFragmentShadingRate? FragmentShadingRate { get; private set; }

    public KhrSwapchain? Swapchain { get; private set; }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void Initialize(bool useValidationLayer,
                                       out Capabilities capabilities,
                                       out CommandQueue graphicsQueue,
                                       out CommandQueue computeQueue,
                                       out CommandQueue transferQueue,
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
                PApplicationName = (byte*)ZenithMarshal.StringToPointer(scope, AppDomain.CurrentDomain.FriendlyName, StringEncoding.UTF8),
                PEngineName = (byte*)ZenithMarshal.StringToPointer(scope, "Zenith.NET", StringEncoding.UTF8),
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

                PhysicalDeviceFeatures features;
                Vk.GetPhysicalDeviceFeatures(physicalDevice, &features);

                if (properties.ApiVersion < apiVersion)
                {
                    continue;
                }

                ulong score = properties.DeviceType switch
                {
                    PhysicalDeviceType.DiscreteGpu => 100000,
                    PhysicalDeviceType.IntegratedGpu => 10000,
                    PhysicalDeviceType.VirtualGpu => 1000,
                    _ => 0
                };

                score += properties.Limits.MaxImageDimension2D;
                score += properties.Limits.MaxImageDimension3D / 16;
                score += properties.Limits.MaxImageArrayLayers;
                score += properties.Limits.MaxComputeSharedMemorySize / 1024;
                score += properties.Limits.MaxComputeWorkGroupInvocations;
                score += properties.Limits.MaxSamplerAllocationCount / 1024;
                score += properties.Limits.MaxStorageBufferRange / (1024 * 1024);
                score += properties.Limits.MaxUniformBufferRange / 1024;
                score += properties.Limits.MaxPushConstantsSize;

                if (features.SamplerAnisotropy)
                {
                    score += 2000;
                }

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

                if (features.FillModeNonSolid)
                {
                    score += 250;
                }

                if (features.TextureCompressionBC)
                {
                    score += 500;
                }

                if (features.ShaderInt64)
                {
                    score += 250;
                }

                if (score > bestScore)
                {
                    bestScore = score;

                    PhysicalDevice = physicalDevice;
                }
            }

            if (PhysicalDevice.Handle is 0)
            {
                throw new NotSupportedException("This device does not support Vulkan 1.4 or higher.");
            }

            uint graphicsQueueFamilyIndex = 0;
            uint graphicsQueueFamilyCount = 0;

            uint computeQueueFamilyIndex = 0;
            uint computeQueueFamilyCount = 0;

            uint transferQueueFamilyIndex = 0;
            uint transferQueueFamilyCount = 0;

            uint queueFamilyCount = 0;
            Vk.GetPhysicalDeviceQueueFamilyProperties(PhysicalDevice, &queueFamilyCount, default);

            QueueFamilyProperties* queueFamilies = (QueueFamilyProperties*)ZenithMarshal.Allocate<QueueFamilyProperties>(scope, queueFamilyCount);
            Vk.GetPhysicalDeviceQueueFamilyProperties(PhysicalDevice, &queueFamilyCount, queueFamilies);

            uint index = 0;
            foreach (QueueFamilyProperties queueFamilyProperties in new ReadOnlySpan<QueueFamilyProperties>(queueFamilies, (int)queueFamilyCount))
            {
                if (queueFamilyProperties.QueueFlags.HasFlag(QueueFlags.GraphicsBit) && queueFamilyProperties.QueueCount > graphicsQueueFamilyCount)
                {
                    graphicsQueueFamilyIndex = index;
                    graphicsQueueFamilyCount = queueFamilyProperties.QueueCount;
                }
                else if (queueFamilyProperties.QueueFlags.HasFlag(QueueFlags.ComputeBit) && queueFamilyProperties.QueueCount > computeQueueFamilyCount)
                {
                    computeQueueFamilyIndex = index;
                    computeQueueFamilyCount = queueFamilyProperties.QueueCount;
                }
                else if (queueFamilyProperties.QueueFlags.HasFlag(QueueFlags.TransferBit) && queueFamilyProperties.QueueCount > transferQueueFamilyCount)
                {
                    transferQueueFamilyIndex = index;
                    transferQueueFamilyCount = queueFamilyProperties.QueueCount;
                }

                index++;
            }

            HashSet<uint> queueFamilyIndices = [graphicsQueueFamilyIndex, computeQueueFamilyIndex, transferQueueFamilyIndex];

            uint queueCreateInfoCount;
            DeviceQueueCreateInfo* queueCreateInfos;
            Func<(Queue GraphicsQueue, Queue ComputeQueue, Queue TransferQueue, uint[] QueueFamilyIndices)> getQueues;
            if (queueFamilyIndices.Count is 3)
            {
                queueCreateInfoCount = 3;

                float* queuePriorities = (float*)ZenithMarshal.Allocate<float>(scope, 1);
                queuePriorities[0] = 1.0f;

                queueCreateInfos = (DeviceQueueCreateInfo*)ZenithMarshal.Allocate<DeviceQueueCreateInfo>(scope, 3);

                queueCreateInfos[0] = new()
                {
                    SType = StructureType.DeviceQueueCreateInfo,
                    QueueFamilyIndex = graphicsQueueFamilyIndex,
                    QueueCount = 1,
                    PQueuePriorities = queuePriorities
                };

                queueCreateInfos[1] = new()
                {
                    SType = StructureType.DeviceQueueCreateInfo,
                    QueueFamilyIndex = computeQueueFamilyIndex,
                    QueueCount = 1,
                    PQueuePriorities = queuePriorities
                };

                queueCreateInfos[2] = new()
                {
                    SType = StructureType.DeviceQueueCreateInfo,
                    QueueFamilyIndex = transferQueueFamilyIndex,
                    QueueCount = 1,
                    PQueuePriorities = queuePriorities
                };

                getQueues = () =>
                {
                    Queue graphicsQueue = default;
                    Vk.GetDeviceQueue(Device, graphicsQueueFamilyIndex, 0, &graphicsQueue);

                    Queue computeQueue = default;
                    Vk.GetDeviceQueue(Device, computeQueueFamilyIndex, 0, &computeQueue);

                    Queue transferQueue = default;
                    Vk.GetDeviceQueue(Device, transferQueueFamilyIndex, 0, &transferQueue);

                    return (graphicsQueue, computeQueue, transferQueue, [graphicsQueueFamilyIndex, computeQueueFamilyIndex, transferQueueFamilyIndex]);
                };
            }
            else if (graphicsQueueFamilyCount >= 3)
            {
                queueCreateInfoCount = 1;

                float* queuePriorities = (float*)ZenithMarshal.Allocate<float>(scope, 3);
                queuePriorities[0] = 1.0f;
                queuePriorities[1] = 1.0f;
                queuePriorities[2] = 1.0f;

                queueCreateInfos = (DeviceQueueCreateInfo*)ZenithMarshal.Allocate<DeviceQueueCreateInfo>(scope, 1);
                queueCreateInfos[0] = new()
                {
                    SType = StructureType.DeviceQueueCreateInfo,
                    QueueFamilyIndex = graphicsQueueFamilyIndex,
                    QueueCount = 3,
                    PQueuePriorities = queuePriorities
                };

                getQueues = () =>
                {
                    Queue graphicsQueue = default;
                    Vk.GetDeviceQueue(Device, graphicsQueueFamilyIndex, 0, &graphicsQueue);

                    Queue computeQueue = default;
                    Vk.GetDeviceQueue(Device, graphicsQueueFamilyIndex, 1, &computeQueue);

                    Queue transferQueue = default;
                    Vk.GetDeviceQueue(Device, graphicsQueueFamilyIndex, 2, &transferQueue);

                    return (graphicsQueue, computeQueue, transferQueue, [graphicsQueueFamilyIndex]);
                };
            }
            else
            {
                queueCreateInfoCount = 1;

                float* queuePriorities = (float*)ZenithMarshal.Allocate<float>(scope, 1);
                queuePriorities[0] = 1.0f;

                queueCreateInfos = (DeviceQueueCreateInfo*)ZenithMarshal.Allocate<DeviceQueueCreateInfo>(scope, 1);
                queueCreateInfos[0] = new()
                {
                    SType = StructureType.DeviceQueueCreateInfo,
                    QueueFamilyIndex = graphicsQueueFamilyIndex,
                    QueueCount = 1,
                    PQueuePriorities = queuePriorities
                };

                getQueues = () =>
                {
                    Queue graphicsQueue = default;
                    Vk.GetDeviceQueue(Device, graphicsQueueFamilyIndex, 0, &graphicsQueue);

                    return (graphicsQueue, graphicsQueue, graphicsQueue, [graphicsQueueFamilyIndex]);
                };
            }

            uint extensionCount = 0;
            Vk.EnumerateDeviceExtensionProperties(PhysicalDevice, default(byte*), &extensionCount, default).Success();

            ExtensionProperties* extensions = (ExtensionProperties*)ZenithMarshal.Allocate<ExtensionProperties>(scope, extensionCount);
            Vk.EnumerateDeviceExtensionProperties(PhysicalDevice, default(byte*), &extensionCount, extensions).Success();

            string[] enabledExtensions = [.. new ReadOnlySpan<ExtensionProperties>(extensions, (int)extensionCount).ToArray().Select(static item => ZenithMarshal.StringFromPointer((nint)item.ExtensionName, StringEncoding.UTF8))];
            enabledExtensions = [.. enabledExtensions.Intersect(DeviceExtensions)];

            DeviceCreateInfo createInfo = new()
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = queueCreateInfoCount,
                PQueueCreateInfos = queueCreateInfos,
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

            LamdaNativeContext context = new((proc) => Vk.GetDeviceProcAddr(Device, (byte*)ZenithMarshal.StringToPointer(scope, proc, StringEncoding.UTF8)));

            ExternalMemoryAndroidHardwareBuffer = enabledExtensions.Contains(AndroidExternalMemoryAndroidHardwareBuffer.ExtensionName) ? new(context) : null;
            DescriptorHeap = enabledExtensions.Contains(ExtDescriptorHeap.ExtensionName) ? new(context) : null;
            MeshShader = enabledExtensions.Contains(ExtMeshShader.ExtensionName) ? new(context) : null;
            MetalObjects = enabledExtensions.Contains(ExtMetalObjects.ExtensionName) ? new(context) : null;
            AccelerationStructure = enabledExtensions.Contains(KhrAccelerationStructure.ExtensionName) ? new(context) : null;
            DeferredHostOperations = enabledExtensions.Contains(KhrDeferredHostOperations.ExtensionName) ? new(context) : null;
            ExternalFenceFd = enabledExtensions.Contains(KhrExternalFenceFd.ExtensionName) ? new(context) : null;
            ExternalMemoryWin32 = enabledExtensions.Contains(KhrExternalMemoryWin32.ExtensionName) ? new(context) : null;
            FragmentShadingRate = enabledExtensions.Contains(KhrFragmentShadingRate.ExtensionName) ? new(context) : null;
            Swapchain = enabledExtensions.Contains(KhrSwapchain.ExtensionName) ? new(context) : null;
        }

        capabilities = new VKCapabilities(this);

        throw new NotImplementedException();
    }

    protected override SwapChain CreateSwapChainImpl(SwapChainDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override Heap CreateHeapImpl(HeapDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override SizeAndAlignment GetSizeAndAlignmentImpl(BufferDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override SizeAndAlignment GetSizeAndAlignmentImpl(TextureDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override Buffer CreateBufferImpl(BufferDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override BufferView CreateBufferViewImpl(BufferViewDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override Texture CreateTextureImpl(TextureDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override Texture CreateTextureImpl(TextureDesc desc, NativeTextureType nativeTextureType, nint nativeTexture)
    {
        throw new NotImplementedException();
    }

    protected override TextureView CreateTextureViewImpl(TextureViewDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override Sampler CreateSamplerImpl(SamplerDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override Shader CreateShaderImpl(ShaderDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override GraphicsPipeline CreateGraphicsPipelineImpl(GraphicsPipelineDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override ComputePipeline CreateComputePipelineImpl(ComputePipelineDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override MeshShadingPipeline CreateMeshShadingPipelineImpl(MeshShadingPipelineDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override QueryHeap CreateQueryHeapImpl(QueryHeapDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override void Destroy()
    {
        base.Destroy();

        Vk.Dispose();
    }
}
