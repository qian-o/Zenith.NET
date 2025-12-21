using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal class DXResourceSet : ResourceSet
{
    private readonly DXDescriptorToken cbvSrvUavTable;
    private readonly DXDescriptorToken samplerTable;
    private readonly Dictionary<ShaderStageFlags, (DXDescriptorToken CbvSrvUavTable, DXDescriptorToken SamplerTable)> graphicsTables;

    public DXResourceSet(DXGraphicsContext context, ResourceSetDesc desc) : base(context, desc)
    {
        (cbvSrvUavTable, samplerTable) = GetTables(ShaderStageFlags.None);
        graphicsTables = ZenithHelper.GraphicShaderStages().ToDictionary(static item => item, GetTables);

        uint resourceStartIndex = 0;
        List<DXTextureView> srvTextureViews = [];
        List<DXTextureView> uavTextureViews = [];

        for (int i = 0; i < desc.Layout.Desc.Bindings.Length; i++)
        {
            ResourceBinding binding = desc.Layout.Desc.Bindings[i];

            for (uint j = 0; j < binding.Count; j++)
            {
                IBindableResource resource = desc.Resources[(int)(resourceStartIndex + j)];

                if (binding.Type is ResourceType.Texture or ResourceType.TextureReadWrite)
                {
                    List<DXTextureView> views = binding.Type is ResourceType.Texture ? srvTextureViews : uavTextureViews;

                    if (resource is Texture texture)
                    {
                        views.Add(texture.DirectX12().View);
                    }
                    else if (resource is TextureView textureView)
                    {
                        views.Add(textureView.DirectX12());
                    }
                }
            }

            resourceStartIndex += binding.Count;
        }

        SrvTextureViews = [.. srvTextureViews];
        UavTextureViews = [.. uavTextureViews];
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public DXTextureView[] SrvTextureViews { get; }

    public DXTextureView[] UavTextureViews { get; }

    public void TransitionStates(CommandBuffer commandBuffer)
    {
        foreach (DXTextureView textureView in SrvTextureViews)
        {
            textureView.TransitionStates(commandBuffer, ResourceStates.AllShaderResource);
        }

        foreach (DXTextureView textureView in UavTextureViews)
        {
            textureView.TransitionStates(commandBuffer, ResourceStates.UnorderedAccess);
        }
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        foreach ((DXDescriptorToken cbvSrvUavTable, DXDescriptorToken samplerTable) in graphicsTables.Values)
        {
            cbvSrvUavTable.Dispose();
            samplerTable.Dispose();
        }
        graphicsTables.Clear();

        samplerTable.Dispose();
        cbvSrvUavTable.Dispose();
    }

    private (DXDescriptorToken CbvSrvUavTable, DXDescriptorToken SamplerTable) GetTables(ShaderStageFlags stage)
    {
        DXDescriptorToken cbvSrvUavTable = default;
        DXDescriptorToken samplerTable = default;

        if (Desc.Layout.DirectX12().ResourceRanges(stage, out DXResourceRange[] cbvSrvUavRanges, out DXResourceRange[] samplerRanges))
        {
            if (cbvSrvUavRanges.Length > 0)
            {
                cbvSrvUavTable = Context.CbvSrvUavAllocator.Allocate((uint)cbvSrvUavRanges.Sum(static item => item.Count));

                uint index = 0;
                foreach (DXResourceRange range in cbvSrvUavRanges)
                {
                    IBindableResource[] resources = [.. Desc.Resources.Skip((int)range.Index).Take((int)range.Count)];

                    switch (range.Type)
                    {
                        case ResourceType.ConstantBuffer:
                            {
                                foreach (IBindableResource resource in resources)
                                {
                                    if (resource is Buffer buffer)
                                    {
                                        Context.Device.CopyDescriptorsSimple(1, cbvSrvUavTable[index], buffer.DirectX12().View.CbvHandle, DescriptorHeapType.CbvSrvUav);
                                    }
                                    else if (resource is BufferView bufferView)
                                    {
                                        Context.Device.CopyDescriptorsSimple(1, cbvSrvUavTable[index], bufferView.DirectX12().CbvHandle, DescriptorHeapType.CbvSrvUav);
                                    }

                                    index++;
                                }
                            }
                            break;

                        case ResourceType.StructuredBuffer:
                            {
                                foreach (IBindableResource resource in resources)
                                {
                                    if (resource is Buffer buffer)
                                    {
                                        Context.Device.CopyDescriptorsSimple(1, cbvSrvUavTable[index], buffer.DirectX12().View.SrvHandle, DescriptorHeapType.CbvSrvUav);
                                    }
                                    else if (resource is BufferView bufferView)
                                    {
                                        Context.Device.CopyDescriptorsSimple(1, cbvSrvUavTable[index], bufferView.DirectX12().SrvHandle, DescriptorHeapType.CbvSrvUav);
                                    }

                                    index++;
                                }
                            }
                            break;

                        case ResourceType.StructuredBufferReadWrite:
                            {
                                foreach (IBindableResource resource in resources)
                                {
                                    if (resource is Buffer buffer)
                                    {
                                        Context.Device.CopyDescriptorsSimple(1, cbvSrvUavTable[index], buffer.DirectX12().View.UavHandle, DescriptorHeapType.CbvSrvUav);
                                    }
                                    else if (resource is BufferView bufferView)
                                    {
                                        Context.Device.CopyDescriptorsSimple(1, cbvSrvUavTable[index], bufferView.DirectX12().UavHandle, DescriptorHeapType.CbvSrvUav);
                                    }

                                    index++;
                                }
                            }
                            break;

                        case ResourceType.Texture:
                            {
                                foreach (IBindableResource resource in resources)
                                {
                                    if (resource is Texture texture)
                                    {
                                        Context.Device.CopyDescriptorsSimple(1, cbvSrvUavTable[index], texture.DirectX12().View.SrvHandle, DescriptorHeapType.CbvSrvUav);
                                    }
                                    else if (resource is TextureView textureView)
                                    {
                                        Context.Device.CopyDescriptorsSimple(1, cbvSrvUavTable[index], textureView.DirectX12().SrvHandle, DescriptorHeapType.CbvSrvUav);
                                    }

                                    index++;
                                }
                            }
                            break;

                        case ResourceType.TextureReadWrite:
                            {
                                foreach (IBindableResource resource in resources)
                                {
                                    if (resource is Texture texture)
                                    {
                                        Context.Device.CopyDescriptorsSimple(1, cbvSrvUavTable[index], texture.DirectX12().View.UavHandle, DescriptorHeapType.CbvSrvUav);
                                    }
                                    else if (resource is TextureView textureView)
                                    {
                                        Context.Device.CopyDescriptorsSimple(1, cbvSrvUavTable[index], textureView.DirectX12().UavHandle, DescriptorHeapType.CbvSrvUav);
                                    }

                                    index++;
                                }
                            }
                            break;

                        case ResourceType.AccelerationStructure:
                            {
                                foreach (IBindableResource resource in resources)
                                {
                                    // TODO: Implement AccelerationStructure support.

                                    index++;
                                }
                            }
                            break;
                    }
                }
            }

            if (samplerRanges.Length > 0)
            {
                samplerTable = Context.SamplerAllocator.Allocate((uint)samplerRanges.Sum(static item => item.Count));

                uint index = 0;
                foreach (DXResourceRange range in samplerRanges)
                {
                    foreach (IBindableResource resource in Desc.Resources.Skip((int)range.Index).Take((int)range.Count))
                    {
                        if (resource is Sampler sampler)
                        {
                            Context.Device.CopyDescriptorsSimple(1, samplerTable[index], sampler.DirectX12().Token.Handle, DescriptorHeapType.Sampler);
                        }

                        index++;
                    }
                }
            }
        }

        return (cbvSrvUavTable, samplerTable);
    }
}
