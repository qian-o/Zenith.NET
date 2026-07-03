using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKBufferView(VKGraphicsContext context, BufferViewDesc desc) : BufferView(context, desc)
{
    private VKDescriptorToken? constantToken;
    private VKDescriptorToken? storageReadOnlyToken;
    private VKDescriptorToken? storageReadWriteToken;

    public override ResourceHandle ConstantHandle => (constantToken ??= CreateToken(DescriptorType.UniformBuffer)).ResourceHandle;

    public override ResourceHandle StorageReadOnlyHandle => (storageReadOnlyToken ??= CreateToken(DescriptorType.StorageBuffer)).ResourceHandle;

    public override ResourceHandle StorageReadWriteHandle => (storageReadWriteToken ??= CreateToken(DescriptorType.StorageBuffer)).ResourceHandle;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        storageReadWriteToken?.Dispose();
        storageReadOnlyToken?.Dispose();
        constantToken?.Dispose();
    }

    private VKDescriptorToken CreateToken(DescriptorType type)
    {
        DeviceAddressRangeEXT addressRange = new(Desc.Buffer.Vulkan().DeviceAddress + Desc.OffsetInBytes, Desc.SizeInBytes);

        return context.ResourceHeap.Allocate(new ResourceDescriptorInfoEXT()
        {
            SType = StructureType.ResourceDescriptorInfoExt(),
            Type = type,
            Data = new()
            {
                PAddressRange = &addressRange
            }
        });
    }
}
