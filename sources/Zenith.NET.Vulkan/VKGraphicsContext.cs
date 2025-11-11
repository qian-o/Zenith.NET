using System.Runtime.CompilerServices;
using Silk.NET.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Zenith.NET;

internal unsafe class VKGraphicsContext(bool useValidationLayer) : GraphicsContext(Backend.Vulkan, useValidationLayer)
{
    private static readonly string[] InstanceLayers =
    [
        "VK_LAYER_KHRONOS_validation"
    ];

    private static readonly string[] InstanceExtensions =
    [
        ExtDebugUtils.ExtensionName,
        KhrSurface.ExtensionName,
        KhrWin32Surface.ExtensionName,
        KhrWaylandSurface.ExtensionName,
        KhrXlibSurface.ExtensionName,
        KhrAndroidSurface.ExtensionName,
        ExtMetalSurface.ExtensionName
    ];

    private static readonly string[] DeviceExtensions =
    [
        KhrSwapchain.ExtensionName,
        KhrExternalMemoryWin32.ExtensionName,
        KhrRayQuery.ExtensionName,
        KhrRayTracingPipeline.ExtensionName,
        KhrAccelerationStructure.ExtensionName,
        KhrDeferredHostOperations.ExtensionName,
        ExtMeshShader.ExtensionName
    ];

    public Instance Instance;

    public PhysicalDevice PhysicalDevice;

    public Device Device;

    public Queue GraphicsQueue;

    public Queue ComputeQueue;

    public Queue CopyQueue;

    public Vk Vk { get; } = Vk.GetApi();

    public VKDescriptorAllocator DescriptorAllocator => field ??= new(this);

    public ExtDebugUtils? DebugUtils { get; private set; }

    public KhrSurface? Surface { get; private set; }

    public KhrWin32Surface? Win32Surface { get; private set; }

    public KhrWaylandSurface? WaylandSurface { get; private set; }

    public KhrXlibSurface? XlibSurface { get; private set; }

    public KhrAndroidSurface? AndroidSurface { get; private set; }

    public ExtMetalSurface? MetalSurface { get; private set; }

    public uint[] QueueFamilyIndices { get; private set; } = [];

    public KhrSwapchain? Swapchain { get; private set; }

    public KhrExternalMemoryWin32? ExternalMemoryWin32 { get; private set; }

    public KhrRayTracingPipeline? RayTracingPipeline { get; private set; }

    public KhrAccelerationStructure? AccelerationStructure { get; private set; }

    public KhrDeferredHostOperations? DeferredHostOperations { get; private set; }

    public ExtMeshShader? MeshShader { get; private set; }

    public (SharingMode SharingMode, uint QueueFamilyIndexCount, nint PQueueFamilyIndices) GetSharingModeInfo(ZenithMarshal.Scope scope)
    {
        if (QueueFamilyIndices.Length is 1)
        {
            return (SharingMode.Exclusive, 0, 0);
        }
        else
        {
            uint* pQueueFamilyIndices = (uint*)ZenithMarshal.Allocate<uint>(scope, (uint)QueueFamilyIndices.Length);
            QueueFamilyIndices.CopyTo(new Span<uint>(pQueueFamilyIndices, QueueFamilyIndices.Length));

            return (SharingMode.Concurrent, (uint)QueueFamilyIndices.Length, (nint)pQueueFamilyIndices);
        }
    }

    public uint FindMemoryTypeIndex(uint memoryTypeBits, MemoryPropertyFlags flags)
    {
        PhysicalDeviceMemoryProperties properties;
        Vk.GetPhysicalDeviceMemoryProperties(PhysicalDevice, &properties);

        uint index = 0;
        foreach (MemoryType memoryType in properties.MemoryTypes.AsSpan())
        {
            if ((memoryTypeBits & (1 << (int)index)) is not 0 && memoryType.PropertyFlags.HasFlag(flags))
            {
                break;
            }

            index++;
        }

        return index;
    }

    protected override void Initialize(bool useValidationLayer,
                                       out Capabilities capabilities,
                                       out CommandQueue graphics,
                                       out CommandQueue compute,
                                       out CommandQueue copy,
                                       out ValidationLayer? validationLayer)
    {
        using ZenithMarshal.Scope scope = new();

        // Create instance
        {
            uint extensionCount = 0;
            Vk.EnumerateInstanceExtensionProperties((byte*)null, &extensionCount, (ExtensionProperties*)null).Success();

            ExtensionProperties* extensions = (ExtensionProperties*)ZenithMarshal.Allocate<ExtensionProperties>(scope, extensionCount);
            Vk.EnumerateInstanceExtensionProperties((byte*)null, &extensionCount, extensions).Success();

            string[] enabledExtensions = [.. new ReadOnlySpan<ExtensionProperties>(extensions, (int)extensionCount).ToArray().Select(static item => ZenithMarshal.StringFromPointer((nint)item.ExtensionName, StringEncoding.UTF8))];
            enabledExtensions = [.. enabledExtensions.Intersect(InstanceExtensions)];

            ApplicationInfo applicationInfo = new()
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = (byte*)ZenithMarshal.StringToPointer(scope, AppDomain.CurrentDomain.FriendlyName, StringEncoding.UTF8),
                ApplicationVersion = new Version32(1, 0, 0),
                PEngineName = (byte*)ZenithMarshal.StringToPointer(scope, "Zenith.NET", StringEncoding.UTF8),
                EngineVersion = new Version32(1, 0, 0),
                ApiVersion = Vk.Version13
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
                Vk.EnumerateInstanceLayerProperties(&layerCount, (LayerProperties*)null).Success();

                LayerProperties* layers = (LayerProperties*)ZenithMarshal.Allocate<LayerProperties>(scope, layerCount);
                Vk.EnumerateInstanceLayerProperties(&layerCount, layers).Success();

                string[] enabledLayers = [.. new ReadOnlySpan<LayerProperties>(layers, (int)layerCount).ToArray().Select(static item => ZenithMarshal.StringFromPointer((nint)item.LayerName, StringEncoding.UTF8))];
                enabledLayers = [.. enabledLayers.Intersect(InstanceLayers)];

                createInfo.EnabledLayerCount = (uint)enabledLayers.Length;
                createInfo.PpEnabledLayerNames = (byte**)ZenithMarshal.StringArrayToPointer(scope, enabledLayers, StringEncoding.UTF8);
            }

            Vk.CreateInstance(&createInfo, null, out Instance).Success();

            LamdaNativeContext context = new((proc) => Vk.GetInstanceProcAddr(Instance, (byte*)ZenithMarshal.StringToPointer(scope, proc, StringEncoding.UTF8)));

            DebugUtils = enabledExtensions.Contains(ExtDebugUtils.ExtensionName) ? new(context) : null;
            Surface = enabledExtensions.Contains(KhrSurface.ExtensionName) ? new(context) : null;
            Win32Surface = enabledExtensions.Contains(KhrWin32Surface.ExtensionName) ? new(context) : null;
            WaylandSurface = enabledExtensions.Contains(KhrWaylandSurface.ExtensionName) ? new(context) : null;
            XlibSurface = enabledExtensions.Contains(KhrXlibSurface.ExtensionName) ? new(context) : null;
            AndroidSurface = enabledExtensions.Contains(KhrAndroidSurface.ExtensionName) ? new(context) : null;
            MetalSurface = enabledExtensions.Contains(ExtMetalSurface.ExtensionName) ? new(context) : null;
        }

        // Select physical device and create logical device
        {
            uint physicalDeviceCount = 0;
            Vk.EnumeratePhysicalDevices(Instance, &physicalDeviceCount, (PhysicalDevice*)null).Success();

            PhysicalDevice* physicalDevices = (PhysicalDevice*)ZenithMarshal.Allocate<PhysicalDevice>(scope, physicalDeviceCount);
            Vk.EnumeratePhysicalDevices(Instance, &physicalDeviceCount, physicalDevices).Success();

            ulong bestScore = 0;
            foreach (PhysicalDevice physicalDevice in new ReadOnlySpan<PhysicalDevice>(physicalDevices, (int)physicalDeviceCount))
            {
                PhysicalDeviceProperties properties;
                Vk.GetPhysicalDeviceProperties(physicalDevice, &properties);

                PhysicalDeviceFeatures features;
                Vk.GetPhysicalDeviceFeatures(physicalDevice, &features);

                if (properties.ApiVersion < Vk.Version13)
                {
                    continue;
                }

                ulong score = 0;

                if (properties.DeviceType == PhysicalDeviceType.DiscreteGpu)
                {
                    score += 100000;
                }
                else if (properties.DeviceType == PhysicalDeviceType.IntegratedGpu)
                {
                    score += 10000;
                }
                else if (properties.DeviceType == PhysicalDeviceType.VirtualGpu)
                {
                    score += 1000;
                }

                score += properties.Limits.MaxImageDimension2D / 1000;
                score += properties.Limits.MaxMemoryAllocationCount / 1000;
                score += properties.Limits.MaxComputeSharedMemorySize / 1024;
                score += properties.Limits.MaxComputeWorkGroupInvocations / 64;
                score += properties.Limits.MaxComputeWorkGroupCount[0] / 1024;
                score += properties.Limits.MaxComputeWorkGroupCount[1] / 1024;
                score += properties.Limits.MaxComputeWorkGroupCount[2] / 1024;
                score += properties.Limits.MaxComputeWorkGroupSize[0] / 64;
                score += properties.Limits.MaxComputeWorkGroupSize[1] / 64;
                score += properties.Limits.MaxComputeWorkGroupSize[2] / 64;

                if (score > bestScore)
                {
                    bestScore = score;

                    PhysicalDevice = physicalDevice;
                }
            }

            if (PhysicalDevice.Handle is 0)
            {
                throw new NotSupportedException("No suitable Vulkan physical device found.");
            }

            uint graphicsQueueFamilyIndex = 0;
            uint graphicsQueueFamilyCount = 0;

            uint computeQueueFamilyIndex = 0;
            uint computeQueueFamilyCount = 0;

            uint copyQueueFamilyIndex = 0;
            uint copyQueueFamilyCount = 0;

            uint queueFamilyCount = 0;
            Vk.GetPhysicalDeviceQueueFamilyProperties(PhysicalDevice, &queueFamilyCount, (QueueFamilyProperties*)null);

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
                else if (queueFamilyProperties.QueueFlags.HasFlag(QueueFlags.TransferBit) && queueFamilyProperties.QueueCount > copyQueueFamilyCount)
                {
                    copyQueueFamilyIndex = index;
                    copyQueueFamilyCount = queueFamilyProperties.QueueCount;
                }

                index++;
            }

            HashSet<uint> queueFamilyIndices = [graphicsQueueFamilyIndex, computeQueueFamilyIndex, copyQueueFamilyIndex];

            uint queueCreateInfoCount;
            DeviceQueueCreateInfo* queueCreateInfos;
            Func<(Queue GraphicsQueue, Queue ComputeQueue, Queue CopyQueue, uint[] QueueFamilyIndices)> getQueues;
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
                    QueueFamilyIndex = copyQueueFamilyIndex,
                    QueueCount = 1,
                    PQueuePriorities = queuePriorities
                };

                getQueues = () =>
                {
                    Queue graphicsQueue = default;
                    Vk.GetDeviceQueue(Device, graphicsQueueFamilyIndex, 0, &graphicsQueue);

                    Queue computeQueue = default;
                    Vk.GetDeviceQueue(Device, computeQueueFamilyIndex, 0, &computeQueue);

                    Queue copyQueue = default;
                    Vk.GetDeviceQueue(Device, copyQueueFamilyIndex, 0, &copyQueue);

                    return (graphicsQueue, computeQueue, copyQueue, [graphicsQueueFamilyIndex, computeQueueFamilyIndex, copyQueueFamilyIndex]);
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

                    Queue copyQueue = default;
                    Vk.GetDeviceQueue(Device, graphicsQueueFamilyIndex, 2, &copyQueue);

                    return (graphicsQueue, computeQueue, copyQueue, [graphicsQueueFamilyIndex]);
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
            Vk.EnumerateDeviceExtensionProperties(PhysicalDevice, (byte*)null, &extensionCount, (ExtensionProperties*)null).Success();

            ExtensionProperties* extensions = (ExtensionProperties*)ZenithMarshal.Allocate<ExtensionProperties>(scope, extensionCount);
            Vk.EnumerateDeviceExtensionProperties(PhysicalDevice, (byte*)null, &extensionCount, extensions).Success();

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

            createInfo.AddNext(out PhysicalDeviceFeatures2 features2)
                      .AddNext(out PhysicalDeviceVulkan13Features _)
                      .AddNext(out PhysicalDeviceVulkan12Features _)
                      .AddNext(out PhysicalDeviceVulkan11Features _);

            if (enabledExtensions.Contains(KhrRayQuery.ExtensionName) || enabledExtensions.Contains(KhrRayTracingPipeline.ExtensionName))
            {
                createInfo.AddNext(out PhysicalDeviceRayQueryFeaturesKHR _)
                          .AddNext(out PhysicalDeviceRayTracingPipelineFeaturesKHR _)
                          .AddNext(out PhysicalDeviceAccelerationStructureFeaturesKHR _);
            }

            if (enabledExtensions.Contains(ExtMeshShader.ExtensionName))
            {
                createInfo.AddNext(out PhysicalDeviceMeshShaderFeaturesEXT _)
                          .AddNext(out PhysicalDeviceFragmentShadingRateFeaturesKHR _);
            }

            Vk.GetPhysicalDeviceFeatures2(PhysicalDevice, &features2);

            Vk.CreateDevice(PhysicalDevice, &createInfo, null, out Device).Success();

            (GraphicsQueue, ComputeQueue, CopyQueue, QueueFamilyIndices) = getQueues();

            LamdaNativeContext context = new((proc) => Vk.GetDeviceProcAddr(Device, (byte*)ZenithMarshal.StringToPointer(scope, proc, StringEncoding.UTF8)));

            Swapchain = enabledExtensions.Contains(KhrSwapchain.ExtensionName) ? new(context) : null;
            ExternalMemoryWin32 = enabledExtensions.Contains(KhrExternalMemoryWin32.ExtensionName) ? new(context) : null;
            RayTracingPipeline = enabledExtensions.Contains(KhrRayTracingPipeline.ExtensionName) ? new(context) : null;
            AccelerationStructure = enabledExtensions.Contains(KhrAccelerationStructure.ExtensionName) ? new(context) : null;
            DeferredHostOperations = enabledExtensions.Contains(KhrDeferredHostOperations.ExtensionName) ? new(context) : null;
            MeshShader = enabledExtensions.Contains(ExtMeshShader.ExtensionName) ? new(context) : null;
        }

        capabilities = new VKCapabilities(this);
        graphics = new VKCommandQueue(this, CommandQueueType.Graphics, GraphicsQueue, QueueFamilyIndices[0]);
        compute = new VKCommandQueue(this, CommandQueueType.Compute, ComputeQueue, QueueFamilyIndices.Length > 1 ? QueueFamilyIndices[1] : QueueFamilyIndices[0]);
        copy = new VKCommandQueue(this, CommandQueueType.Copy, CopyQueue, QueueFamilyIndices.Length > 2 ? QueueFamilyIndices[2] : QueueFamilyIndices[0]);
        validationLayer = useValidationLayer ? new VKValidationLayer(this) : null;
    }

    protected override SwapChain CreateSwapChainImpl(SwapChainDesc desc)
    {
        return new VKSwapChain(this, desc);
    }

    protected override FrameBuffer CreateFrameBufferImpl(FrameBufferDesc desc)
    {
        return new VKFrameBuffer(this, desc);
    }

    protected override Shader CreateShaderImpl(ShaderDesc desc)
    {
        return new VKShader(this, desc);
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

    protected override TextureView CreateTextureViewImpl(TextureViewDesc desc)
    {
        return new VKTextureView(this, desc);
    }

    protected override Sampler CreateSamplerImpl(SamplerDesc desc)
    {
        return new VKSampler(this, desc);
    }

    protected override ResourceLayout CreateResourceLayoutImpl(ResourceLayoutDesc desc)
    {
        return new VKResourceLayout(this, desc);
    }

    protected override ResourceSet CreateResourceSetImpl(ResourceSetDesc desc)
    {
        return new VKResourceSet(this, desc);
    }

    protected override GraphicsPipeline CreateGraphicsPipelineImpl(GraphicsPipelineDesc desc)
    {
        return new VKGraphicsPipeline(this, desc);
    }

    protected override ComputePipeline CreateComputePipelineImpl(ComputePipelineDesc desc)
    {
        return new VKComputePipeline(this, desc);
    }

    protected override RayTracingPipeline CreateRayTracingPipelineImpl(RayTracingPipelineDesc desc)
    {
        return new VKRayTracingPipeline(this, desc);
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
        Vk.DeviceWaitIdle(Device).Success();

        base.Destroy();

        Vk.DestroyDevice(Device, null);
        Vk.DestroyInstance(Instance, null);

        Vk.Dispose();
    }
}
