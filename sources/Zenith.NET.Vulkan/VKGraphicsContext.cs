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
        KhrDeferredHostOperations.ExtensionName
    ];

    public VkInstance Instance;

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
                                       out CommandQueue direct,
                                       out CommandQueue compute,
                                       out CommandQueue copy,
                                       out ValidationLayer? validationLayer)
    {
        using ZenithMarshal.Scope scope = new();

        // Create instance
        {
            uint extensionCount = 0;
            Vk.EnumerateInstanceExtensionProperties((byte*)null, &extensionCount, (ExtensionProperties*)null);

            ExtensionProperties* availableExtensions = (ExtensionProperties*)ZenithMarshal.Allocate<ExtensionProperties>(scope, extensionCount);
            Vk.EnumerateInstanceExtensionProperties((byte*)null, &extensionCount, availableExtensions);

            string[] instanceExtensions = [.. new ReadOnlySpan<ExtensionProperties>(availableExtensions, (int)extensionCount).ToArray().Select(static item => ZenithMarshal.StringFromPointer((nint)item.ExtensionName, StringEncoding.UTF8))];
            instanceExtensions = [.. instanceExtensions.Intersect(InstanceExtensions)];

            ApplicationInfo ApplicationInfo = new()
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
                PApplicationInfo = &ApplicationInfo,
                EnabledExtensionCount = (uint)instanceExtensions.Length,
                PpEnabledExtensionNames = (byte**)ZenithMarshal.StringArrayToPointer(scope, instanceExtensions, StringEncoding.UTF8)
            };

            if (useValidationLayer)
            {
                uint layerCount = 0;
                Vk.EnumerateInstanceLayerProperties(&layerCount, (LayerProperties*)null);

                LayerProperties* availableLayers = (LayerProperties*)ZenithMarshal.Allocate<LayerProperties>(scope, layerCount);
                Vk.EnumerateInstanceLayerProperties(&layerCount, availableLayers);

                string[] validationLayers = [.. new ReadOnlySpan<LayerProperties>(availableLayers, (int)layerCount).ToArray().Select(static item => ZenithMarshal.StringFromPointer((nint)item.LayerName, StringEncoding.UTF8))];
                validationLayers = [.. validationLayers.Intersect(InstanceLayers)];

                createInfo.EnabledLayerCount = (uint)validationLayers.Length;
                createInfo.PpEnabledLayerNames = (byte**)ZenithMarshal.StringArrayToPointer(scope, validationLayers, StringEncoding.UTF8);
            }

            Vk.CreateInstance(&createInfo, null, (VkInstance*)Unsafe.AsPointer(ref Instance));

            LamdaNativeContext context = new((proc) => Vk.GetInstanceProcAddr(Instance, (byte*)ZenithMarshal.StringToPointer(scope, proc, StringEncoding.UTF8)));

            DebugUtils = instanceExtensions.Contains(ExtDebugUtils.ExtensionName) ? new(context) : null;
            Surface = instanceExtensions.Contains(KhrSurface.ExtensionName) ? new(context) : null;
            Win32Surface = instanceExtensions.Contains(KhrWin32Surface.ExtensionName) ? new(context) : null;
            WaylandSurface = instanceExtensions.Contains(KhrWaylandSurface.ExtensionName) ? new(context) : null;
            XlibSurface = instanceExtensions.Contains(KhrXlibSurface.ExtensionName) ? new(context) : null;
            AndroidSurface = instanceExtensions.Contains(KhrAndroidSurface.ExtensionName) ? new(context) : null;
            MetalSurface = instanceExtensions.Contains(ExtMetalSurface.ExtensionName) ? new(context) : null;
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
