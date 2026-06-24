using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Zenith.NET.Vulkan;

internal class VKGraphicsContext(bool useValidationLayer) : GraphicsContext(GraphicsApi.Vulkan, useValidationLayer)
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
        ExtDescriptorHeap.ExtensionName,
        ExtMeshShader.ExtensionName,
        KhrAccelerationStructure.ExtensionName,
        KhrDeferredHostOperations.ExtensionName,
        KhrExternalMemoryWin32.ExtensionName,
        KhrRayQuery.ExtensionName,
        KhrSwapchain.ExtensionName
    ];

    public Instance Instance;

    public PhysicalDevice PhysicalDevice;

    public Device Device;

    public Vk Vk { get; } = Vk.GetApi();

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void Initialize(bool useValidationLayer,
                                       out Capabilities capabilities,
                                       out CommandQueue graphicsQueue,
                                       out CommandQueue computeQueue,
                                       out CommandQueue copyQueue,
                                       out ValidationLayer? validationLayer)
    {
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
