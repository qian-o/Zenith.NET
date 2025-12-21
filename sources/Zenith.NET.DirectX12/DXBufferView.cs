using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXBufferView(DXGraphicsContext context, BufferViewDesc desc) : BufferView(context, desc)
{
    private DXDescriptorToken? cbvToken;
    private DXDescriptorToken? srvToken;
    private DXDescriptorToken? uavToken;

    public CpuDescriptorHandle CbvHandle => (cbvToken ??= CreateCbvToken()).Handle;

    public CpuDescriptorHandle SrvHandle => (srvToken ??= CreateSrvToken()).Handle;

    public CpuDescriptorHandle UavHandle => (uavToken ??= CreateUavToken()).Handle;

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
        DXDescriptorToken token = context.CbvSrvUavAllocator.Allocate(1);

        ConstantBufferViewDesc viewDesc = new()
        {
            BufferLocation = Desc.Buffer.DirectX12().Resource.GetGPUVirtualAddress(),
            SizeInBytes = Desc.SizeInBytes
        };

        context.Device.CreateConstantBufferView(&viewDesc, token.Handle);

        return token;
    }

    private DXDescriptorToken CreateSrvToken()
    {
        DXDescriptorToken token = context.CbvSrvUavAllocator.Allocate(1);

        ShaderResourceViewDesc viewDesc = new()
        {
            ViewDimension = SrvDimension.Buffer,
            Shader4ComponentMapping = DXGraphicsContext.Shader4ComponentMapping,
            Buffer = new()
            {
                NumElements = Desc.SizeInBytes / Desc.StrideInBytes,
                StructureByteStride = Desc.StrideInBytes
            }
        };

        context.Device.CreateShaderResourceView(Desc.Buffer.DirectX12().Resource, &viewDesc, token.Handle);

        return token;
    }

    private DXDescriptorToken CreateUavToken()
    {
        DXDescriptorToken token = context.CbvSrvUavAllocator.Allocate(1);

        UnorderedAccessViewDesc viewDesc = new()
        {
            ViewDimension = UavDimension.Buffer,
            Buffer = new()
            {
                NumElements = Desc.SizeInBytes / Desc.StrideInBytes,
                StructureByteStride = Desc.StrideInBytes
            }
        };

        context.Device.CreateUnorderedAccessView(Desc.Buffer.DirectX12().Resource, (ID3D12Resource*)null, &viewDesc, token.Handle);

        return token;
    }
}
