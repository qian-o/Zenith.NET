using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXBufferView(DXGraphicsContext context, BufferViewDesc desc) : BufferView(context, desc)
{
    private DXDescriptorToken? cbvToken;
    private DXDescriptorToken? srvToken;
    private DXDescriptorToken? uavToken;

    public override ResourceHandle UniformHandle => (cbvToken ??= CreateCbvToken()).ResourceHandle;

    public override ResourceHandle StorageReadOnlyHandle => (srvToken ??= CreateSrvToken()).ResourceHandle;

    public override ResourceHandle StorageReadWriteHandle => (uavToken ??= CreateUavToken()).ResourceHandle;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        uavToken?.Dispose();
        srvToken?.Dispose();
        cbvToken?.Dispose();
    }

    private DXDescriptorToken CreateCbvToken()
    {
        DXDescriptorToken token = context.CbvSrvUavHeap.Allocate();

        ConstantBufferViewDesc viewDesc = new()
        {
            BufferLocation = Desc.Buffer.DirectX12().GPUVirtualAddress + Desc.OffsetInBytes,
            SizeInBytes = ZenithHelper.Align(Desc.SizeInBytes, 256u)
        };

        context.Device10.CreateConstantBufferView(&viewDesc, token.CpuHandle);

        return token;
    }

    private DXDescriptorToken CreateSrvToken()
    {
        DXDescriptorToken token = context.CbvSrvUavHeap.Allocate();

        ShaderResourceViewDesc viewDesc = new()
        {
            ViewDimension = SrvDimension.Buffer,
            Shader4ComponentMapping = DXGraphicsContext.Shader4ComponentMapping,
            Buffer = new()
            {
                FirstElement = Desc.OffsetInBytes / Desc.StrideInBytes,
                NumElements = Desc.SizeInBytes / Desc.StrideInBytes,
                StructureByteStride = Desc.StrideInBytes
            }
        };

        context.Device10.CreateShaderResourceView(Desc.Buffer.DirectX12().Resource, &viewDesc, token.CpuHandle);

        return token;
    }

    private DXDescriptorToken CreateUavToken()
    {
        DXDescriptorToken token = context.CbvSrvUavHeap.Allocate();

        UnorderedAccessViewDesc viewDesc = new()
        {
            ViewDimension = UavDimension.Buffer,
            Buffer = new()
            {
                FirstElement = Desc.OffsetInBytes / Desc.StrideInBytes,
                NumElements = Desc.SizeInBytes / Desc.StrideInBytes,
                StructureByteStride = Desc.StrideInBytes
            }
        };

        context.Device10.CreateUnorderedAccessView(Desc.Buffer.DirectX12().Resource, (ID3D12Resource*)null, &viewDesc, token.CpuHandle);

        return token;
    }
}
