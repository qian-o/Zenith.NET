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

    public Vk Vk { get; } = Vk.GetApi();

    public ExtDebugUtils? DebugUtils { get; private set; }

    public KhrSurface? Surface { get; private set; }

    public KhrWin32Surface? Win32Surface { get; private set; }

    public KhrWaylandSurface? WaylandSurface { get; private set; }

    public KhrXlibSurface? XlibSurface { get; private set; }

    public KhrAndroidSurface? AndroidSurface { get; private set; }

    public ExtMetalSurface? MetalSurface { get; private set; }

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

            Vk.CreateInstance(&createInfo, null, (Instance*)Unsafe.AsPointer(ref Instance)).Success();

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
                    score += 1000;
                }
                else if (properties.DeviceType == PhysicalDeviceType.IntegratedGpu)
                {
                    score += 500;
                }
                else if (properties.DeviceType == PhysicalDeviceType.VirtualGpu)
                {
                    score += 250;
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
                score += properties.Limits.MaxDescriptorSetUniformBuffers / 16;
                score += properties.Limits.MaxDescriptorSetStorageBuffers / 16;
                score += properties.Limits.MaxDescriptorSetSampledImages / 16;
                score += properties.Limits.MaxDescriptorSetStorageImages / 16;
                score += properties.Limits.MaxDescriptorSetInputAttachments / 16;

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
        }

        throw new NotImplementedException();
    }

    protected override SwapChain CreateSwapChainImpl(SwapChainDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override FrameBuffer CreateFrameBufferImpl(FrameBufferDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override Shader CreateShaderImpl(ShaderDesc desc)
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

    protected override TextureView CreateTextureViewImpl(TextureViewDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override Sampler CreateSamplerImpl(SamplerDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override ResourceLayout CreateResourceLayoutImpl(ResourceLayoutDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override ResourceSet CreateResourceSetImpl(ResourceSetDesc desc)
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

    protected override RayTracingPipeline CreateRayTracingPipelineImpl(RayTracingPipelineDesc desc)
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
}
