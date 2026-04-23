using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal class DXResourceTable : ResourceTable
{
    private readonly DXDescriptorToken cbvSrvUavToken;
    private readonly DXDescriptorToken samplerToken;

    public DXResourceTable(DXGraphicsContext context, ResourceTableDesc desc) : base(context, desc)
    {
        desc.Bindings.DirectX12(out DescriptorRange[] cbvSrvUavRanges, out DescriptorRange[] samplerRanges);

        if (cbvSrvUavRanges.Length > 0)
        {
            cbvSrvUavToken = Context.CbvSrvUavAllocator.Allocate((uint)cbvSrvUavRanges.Sum(static item => item.NumDescriptors));
        }

        if (samplerRanges.Length > 0)
        {
            samplerToken = Context.SamplerAllocator.Allocate((uint)samplerRanges.Sum(static item => item.NumDescriptors));
        }

        SrvTextureViews = new DXTextureView?[cbvSrvUavToken.Length];
        UavTextureViews = new DXTextureView?[cbvSrvUavToken.Length];
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public DXTextureView?[] SrvTextureViews { get; }

    public DXTextureView?[] UavTextureViews { get; }

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

    protected override void WriteImpl(uint binding, IBindableResource[] resources)
    {
        switch (Desc.Bindings[binding].Type)
        {
            case ResourceType.ConstantBuffer:
                {
                    uint index = (uint)Desc.Bindings.Take((int)binding).Where(static item => item.Type is not ResourceType.Sampler).Sum(static item => item.Count);

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
                }
                break;

            case ResourceType.StructuredBuffer:
                {
                    uint index = (uint)Desc.Bindings.Take((int)binding).Where(static item => item.Type is not ResourceType.Sampler).Sum(static item => item.Count);

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
                }
                break;

            case ResourceType.StructuredBufferReadWrite:
                {
                    uint index = (uint)Desc.Bindings.Take((int)binding).Where(static item => item.Type is not ResourceType.Sampler).Sum(static item => item.Count);

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
                }
                break;

            case ResourceType.Texture:
                {
                    uint index = (uint)Desc.Bindings.Take((int)binding).Where(static item => item.Type is not ResourceType.Sampler).Sum(static item => item.Count);

                    foreach (IBindableResource resource in resources)
                    {
                        if (resource is Texture texture)
                        {
                            Context.Device.CopyDescriptorsSimple(1, cbvSrvUavToken[index], texture.DirectX12().View.SrvHandle, DescriptorHeapType.CbvSrvUav);

                            SrvTextureViews[index] = texture.DirectX12().View;
                        }
                        else if (resource is TextureView textureView)
                        {
                            Context.Device.CopyDescriptorsSimple(1, cbvSrvUavToken[index], textureView.DirectX12().SrvHandle, DescriptorHeapType.CbvSrvUav);

                            SrvTextureViews[index] = textureView.DirectX12();
                        }

                        index++;
                    }
                }
                break;

            case ResourceType.TextureReadWrite:
                {
                    uint index = (uint)Desc.Bindings.Take((int)binding).Where(static item => item.Type is not ResourceType.Sampler).Sum(static item => item.Count);

                    foreach (IBindableResource resource in resources)
                    {
                        if (resource is Texture texture)
                        {
                            Context.Device.CopyDescriptorsSimple(1, cbvSrvUavToken[index], texture.DirectX12().View.UavHandle, DescriptorHeapType.CbvSrvUav);

                            UavTextureViews[index] = texture.DirectX12().View;
                        }
                        else if (resource is TextureView textureView)
                        {
                            Context.Device.CopyDescriptorsSimple(1, cbvSrvUavToken[index], textureView.DirectX12().UavHandle, DescriptorHeapType.CbvSrvUav);

                            UavTextureViews[index] = textureView.DirectX12();
                        }

                        index++;
                    }
                }
                break;

            case ResourceType.Sampler:
                {
                    uint index = (uint)Desc.Bindings.Take((int)binding).Where(static item => item.Type is ResourceType.Sampler).Sum(static item => item.Count);

                    foreach (IBindableResource resource in resources)
                    {
                        if (resource is Sampler sampler)
                        {
                            Context.Device.CopyDescriptorsSimple(1, samplerToken[index], sampler.DirectX12().Token.Handle, DescriptorHeapType.Sampler);
                        }

                        index++;
                    }
                }
                break;

            case ResourceType.AccelerationStructure:
                {
                    uint index = (uint)Desc.Bindings.Take((int)binding).Where(static item => item.Type is not ResourceType.Sampler).Sum(static item => item.Count);

                    foreach (IBindableResource resource in resources)
                    {
                        if (resource is TopLevelAccelerationStructure topLevelAccelerationStructure)
                        {
                            Context.Device.CopyDescriptorsSimple(1, cbvSrvUavToken[index], topLevelAccelerationStructure.DirectX12().Token.Handle, DescriptorHeapType.CbvSrvUav);
                        }

                        index++;
                    }
                }
                break;
        }
    }

    protected override void PreprocessImpl(CommandBuffer commandBuffer)
    {
        DXCommandBuffer dxCommandBuffer = commandBuffer.DirectX12();

        foreach (DXTextureView? textureView in SrvTextureViews)
        {
            textureView?.TransitionStates(dxCommandBuffer, ResourceStates.AllShaderResource);
        }

        foreach (DXTextureView? textureView in UavTextureViews)
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
