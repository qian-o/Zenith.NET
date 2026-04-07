using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal class DXResourceTable : ResourceTable
{
    private readonly DXDescriptorToken cbvSrvUavToken;
    private readonly DXDescriptorToken samplerToken;
    private readonly DXTextureView?[] srvTextureViews;
    private readonly DXTextureView?[] uavTextureViews;

    public DXResourceTable(DXGraphicsContext context, ResourceTableDesc desc) : base(context, desc)
    {
        desc.Slots.DirectX12(out DescriptorRange[] cbvSrvUavRanges, out DescriptorRange[] samplerRanges);

        if (cbvSrvUavRanges.Length > 0)
        {
            cbvSrvUavToken = Context.CbvSrvUavAllocator.Allocate((uint)cbvSrvUavRanges.Sum(static item => item.NumDescriptors));
        }

        if (samplerRanges.Length > 0)
        {
            samplerToken = Context.SamplerAllocator.Allocate((uint)samplerRanges.Sum(static item => item.NumDescriptors));
        }

        srvTextureViews = new DXTextureView?[Desc.Slots.Sum(slot => slot.Count)];
        uavTextureViews = new DXTextureView?[Desc.Slots.Sum(slot => slot.Count)];
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public void Bind(DXCommandBuffer commandBuffer, DXDescriptorTable cbvSrvUavTable, DXDescriptorTable samplerTable, bool isGraphics)
    {
        uint offset = 0;

        if (cbvSrvUavToken.Length > 0)
        {
            if (isGraphics)
            {
                commandBuffer.GraphicsCommandList4.SetGraphicsRootDescriptorTable(offset++, cbvSrvUavTable.GpuCurrentHandle);
            }
            else
            {
                commandBuffer.GraphicsCommandList4.SetComputeRootDescriptorTable(offset++, cbvSrvUavTable.GpuCurrentHandle);
            }

            cbvSrvUavTable.Write(cbvSrvUavToken);
        }

        if (samplerToken.Length > 0)
        {
            if (isGraphics)
            {
                commandBuffer.GraphicsCommandList4.SetGraphicsRootDescriptorTable(offset++, samplerTable.GpuCurrentHandle);
            }
            else
            {
                commandBuffer.GraphicsCommandList4.SetComputeRootDescriptorTable(offset++, samplerTable.GpuCurrentHandle);
            }

            samplerTable.Write(samplerToken);
        }
    }

    protected override void SetImpl(uint slot, IBindableResource[] resources)
    {
        ResourceSlot resourceSlot = Desc.Slots[slot];

        uint index = 0;
        if (resourceSlot.Type is ResourceType.Sampler)
        {
            index = (uint)Desc.Slots.Take((int)slot).Where(static item => item.Type is ResourceType.Sampler).Sum(static item => item.Count);
        }
        else
        {
            index = (uint)Desc.Slots.Take((int)slot).Where(static item => item.Type is not ResourceType.Sampler).Sum(static item => item.Count);
        }

        switch (resourceSlot.Type)
        {
            case ResourceType.ConstantBuffer:
                foreach (IBindableResource resource in resources)
                {
                    if (resource is Buffer buffer)
                    {
                        Context.Device.CopyDescriptorsSimple(1, cbvSrvUavToken[index], buffer.DirectX12().View.CbvHandle, DescriptorHeapType.CbvSrvUav);
                    }
                    else if (resource is BufferView bufferView)
                    {
                        Context.Device.CopyDescriptorsSimple(1, cbvSrvUavToken[index], bufferView.DirectX12().CbvHandle, DescriptorHeapType.CbvSrvUav);
                    }

                    index++;
                }
                break;

            case ResourceType.StructuredBuffer:
                foreach (IBindableResource resource in resources)
                {
                    if (resource is Buffer buffer)
                    {
                        Context.Device.CopyDescriptorsSimple(1, cbvSrvUavToken[index], buffer.DirectX12().View.SrvHandle, DescriptorHeapType.CbvSrvUav);
                    }
                    else if (resource is BufferView bufferView)
                    {
                        Context.Device.CopyDescriptorsSimple(1, cbvSrvUavToken[index], bufferView.DirectX12().SrvHandle, DescriptorHeapType.CbvSrvUav);
                    }

                    index++;
                }
                break;

            case ResourceType.StructuredBufferReadWrite:
                foreach (IBindableResource resource in resources)
                {
                    if (resource is Buffer buffer)
                    {
                        Context.Device.CopyDescriptorsSimple(1, cbvSrvUavToken[index], buffer.DirectX12().View.UavHandle, DescriptorHeapType.CbvSrvUav);
                    }
                    else if (resource is BufferView bufferView)
                    {
                        Context.Device.CopyDescriptorsSimple(1, cbvSrvUavToken[index], bufferView.DirectX12().UavHandle, DescriptorHeapType.CbvSrvUav);
                    }

                    index++;
                }
                break;

            case ResourceType.Texture:
                foreach (IBindableResource resource in resources)
                {
                    if (resource is Texture texture)
                    {
                        Context.Device.CopyDescriptorsSimple(1, cbvSrvUavToken[index], texture.DirectX12().View.SrvHandle, DescriptorHeapType.CbvSrvUav);

                        srvTextureViews[index] = texture.DirectX12().View;
                    }
                    else if (resource is TextureView textureView)
                    {
                        Context.Device.CopyDescriptorsSimple(1, cbvSrvUavToken[index], textureView.DirectX12().SrvHandle, DescriptorHeapType.CbvSrvUav);

                        srvTextureViews[index] = textureView.DirectX12();
                    }

                    index++;
                }
                break;

            case ResourceType.TextureReadWrite:
                foreach (IBindableResource resource in resources)
                {
                    if (resource is Texture texture)
                    {
                        Context.Device.CopyDescriptorsSimple(1, cbvSrvUavToken[index], texture.DirectX12().View.UavHandle, DescriptorHeapType.CbvSrvUav);

                        uavTextureViews[index] = texture.DirectX12().View;
                    }
                    else if (resource is TextureView textureView)
                    {
                        Context.Device.CopyDescriptorsSimple(1, cbvSrvUavToken[index], textureView.DirectX12().UavHandle, DescriptorHeapType.CbvSrvUav);

                        uavTextureViews[index] = textureView.DirectX12();
                    }

                    index++;
                }
                break;

            case ResourceType.AccelerationStructure:
                foreach (IBindableResource resource in resources)
                {
                    if (resource is TopLevelAccelerationStructure topLevelAccelerationStructure)
                    {
                        Context.Device.CopyDescriptorsSimple(1, cbvSrvUavToken[index], topLevelAccelerationStructure.DirectX12().Token.Handle, DescriptorHeapType.CbvSrvUav);
                    }

                    index++;
                }
                break;
        }
    }

    protected override void PreprocessImpl(CommandBuffer commandBuffer)
    {
        DXCommandBuffer dxCommandBuffer = commandBuffer.DirectX12();

        foreach (DXTextureView? textureView in srvTextureViews)
        {
            textureView?.TransitionStates(dxCommandBuffer, ResourceStates.AllShaderResource);
        }

        foreach (DXTextureView? textureView in uavTextureViews)
        {
            textureView?.TransitionStates(dxCommandBuffer, ResourceStates.UnorderedAccess);
        }
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        samplerToken.Dispose();
        cbvSrvUavToken.Dispose();
    }
}
