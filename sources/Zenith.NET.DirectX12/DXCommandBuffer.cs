using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.Maths;

namespace Zenith.NET.DirectX12;

internal unsafe class DXCommandBuffer : CommandBuffer
{
    private readonly DXDescriptorTable? cbvSrvUavTable;
    private readonly DXDescriptorTable? samplerTable;

    public ComPtr<ID3D12CommandAllocator> CommandAllocator;

    public ComPtr<ID3D12CommandList> CommandList;

    public ComPtr<ID3D12GraphicsCommandList> GraphicsCommandList;

    public ComPtr<ID3D12GraphicsCommandList4>? GraphicsCommandList4;

    public ComPtr<ID3D12GraphicsCommandList6>? GraphicsCommandList6;

    public DXCommandBuffer(DXGraphicsContext context, DXCommandQueue queue) : base(context, queue)
    {
        if (queue.Type is not CommandQueueType.Copy)
        {
            cbvSrvUavTable = new(context, DescriptorHeapType.CbvSrvUav, 2048);
            samplerTable = new(context, DescriptorHeapType.Sampler, 1024);
        }

        context.Device.CreateCommandAllocator(DXFormats.DirectX12(queue.Type), out CommandAllocator).Success();

        context.Device.CreateCommandList(0, DXFormats.DirectX12(queue.Type), CommandAllocator, (ComPtr<ID3D12PipelineState>)null, out CommandList).Success();

        CommandList.QueryInterface(out GraphicsCommandList).Success();

        if (CommandList.QueryInterface(out ComPtr<ID3D12GraphicsCommandList4> graphicsCommandList4).IsSuccess())
        {
            GraphicsCommandList4 = graphicsCommandList4;
        }

        if (CommandList.QueryInterface(out ComPtr<ID3D12GraphicsCommandList6> graphicsCommandList6).IsSuccess())
        {
            GraphicsCommandList6 = graphicsCommandList6;
        }
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    protected override void CopyBufferImpl(Buffer src, uint srcOffsetInBytes, Buffer dest, uint destOffsetInBytes, uint sizeInBytes)
    {
        DXBuffer dxSrc = src.DirectX12();
        DXBuffer dxDest = dest.DirectX12();

        ResourceStates srcOldStates = dxSrc.States;
        ResourceStates destOldStates = dxDest.States;

        dxSrc.TransitionStates(this, ResourceStates.CopySource);
        dxDest.TransitionStates(this, ResourceStates.CopyDest);

        GraphicsCommandList.CopyBufferRegion(dxDest.Resource, destOffsetInBytes, dxSrc.Resource, srcOffsetInBytes, sizeInBytes);

        dxSrc.TransitionStates(this, srcOldStates);
        dxDest.TransitionStates(this, destOldStates);
    }

    protected override void CopyBufferToTextureImpl(Buffer src, uint srcOffsetInBytes, Texture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent destExtent)
    {
        DXBuffer dxSrc = src.DirectX12();
        DXTexture dxDest = dest.DirectX12();

        ResourceStates srcOldStates = dxSrc.States;
        ResourceStates destOldStates = dxDest.States[ZenithHelper.SubresourceIndex(dxDest.Desc, destSlice)];

        dxSrc.TransitionStates(this, ResourceStates.CopySource);
        dxDest.TransitionStates(this, destSlice, ResourceStates.CopyDest);

        TextureCopyLocation srcLocation = new()
        {
            PResource = dxSrc.Resource,
            Type = TextureCopyType.PlacedFootprint,
            PlacedFootprint = new()
            {
                Offset = srcOffsetInBytes,
                Footprint = new()
                {
                    Format = DXFormats.DirectX12(dxDest.Desc.Format),
                    Width = destExtent.Width,
                    Height = destExtent.Height,
                    Depth = destExtent.Depth,
                    RowPitch = ZenithHelper.Align(ZenithHelper.SizeInBytes(dxDest.Desc.Format) * destExtent.Width, GraphicsContext.TextureRowPitchAlignment)
                }
            }
        };

        TextureCopyLocation destLocation = new()
        {
            PResource = dxDest.Resource,
            Type = TextureCopyType.SubresourceIndex,
            SubresourceIndex = ZenithHelper.SubresourceIndex(dxDest.Desc, destSlice)
        };

        GraphicsCommandList.CopyTextureRegion(&destLocation, destOffset.X, destOffset.Y, destOffset.Z, &srcLocation, (Box*)null);

        dxSrc.TransitionStates(this, srcOldStates);
        dxDest.TransitionStates(this, destSlice, destOldStates);
    }

    protected override void CopyTextureImpl(Texture src, TextureSlice srcSlice, TextureOffset srcOffset, Texture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent extent)
    {
        DXTexture dxSrc = src.DirectX12();
        DXTexture dxDest = dest.DirectX12();

        ResourceStates srcOldStates = dxSrc.States[ZenithHelper.SubresourceIndex(dxSrc.Desc, srcSlice)];
        ResourceStates destOldStates = dxDest.States[ZenithHelper.SubresourceIndex(dxDest.Desc, destSlice)];

        dxSrc.TransitionStates(this, srcSlice, ResourceStates.CopySource);
        dxDest.TransitionStates(this, destSlice, ResourceStates.CopyDest);

        TextureCopyLocation srcLocation = new()
        {
            PResource = dxSrc.Resource,
            Type = TextureCopyType.SubresourceIndex,
            SubresourceIndex = ZenithHelper.SubresourceIndex(dxSrc.Desc, srcSlice)
        };

        Box srcBox = new(srcOffset.X, srcOffset.Y, srcOffset.Z, srcOffset.X + extent.Width, srcOffset.Y + extent.Height, srcOffset.Z + extent.Depth);

        TextureCopyLocation destLocation = new()
        {
            PResource = dxDest.Resource,
            Type = TextureCopyType.SubresourceIndex,
            SubresourceIndex = ZenithHelper.SubresourceIndex(dxDest.Desc, destSlice)
        };

        GraphicsCommandList.CopyTextureRegion(&destLocation, destOffset.X, destOffset.Y, destOffset.Z, &srcLocation, &srcBox);

        dxSrc.TransitionStates(this, srcSlice, srcOldStates);
        dxDest.TransitionStates(this, destSlice, destOldStates);
    }

    protected override void CopyTextureToBufferImpl(Texture src, TextureSlice srcSlice, TextureOffset srcOffset, TextureExtent srcExtent, Buffer dest, uint destOffsetInBytes)
    {
        DXTexture dxSrc = src.DirectX12();
        DXBuffer dxDest = dest.DirectX12();

        ResourceStates srcOldStates = dxSrc.States[ZenithHelper.SubresourceIndex(dxSrc.Desc, srcSlice)];
        ResourceStates destOldStates = dxDest.States;

        dxSrc.TransitionStates(this, srcSlice, ResourceStates.CopySource);
        dxDest.TransitionStates(this, ResourceStates.CopyDest);

        TextureCopyLocation srcLocation = new()
        {
            PResource = dxSrc.Resource,
            Type = TextureCopyType.SubresourceIndex,
            SubresourceIndex = ZenithHelper.SubresourceIndex(dxSrc.Desc, srcSlice)
        };

        Box srcBox = new(srcOffset.X, srcOffset.Y, srcOffset.Z, srcOffset.X + srcExtent.Width, srcOffset.Y + srcExtent.Height, srcOffset.Z + srcExtent.Depth);

        TextureCopyLocation destLocation = new()
        {
            PResource = dxDest.Resource,
            Type = TextureCopyType.PlacedFootprint,
            PlacedFootprint = new()
            {
                Offset = destOffsetInBytes,
                Footprint = new()
                {
                    Format = DXFormats.DirectX12(dxSrc.Desc.Format),
                    Width = srcExtent.Width,
                    Height = srcExtent.Height,
                    Depth = srcExtent.Depth,
                    RowPitch = ZenithHelper.Align(ZenithHelper.SizeInBytes(dxSrc.Desc.Format) * srcExtent.Width, GraphicsContext.TextureRowPitchAlignment)
                }
            }
        };

        GraphicsCommandList.CopyTextureRegion(&destLocation, 0, 0, 0, &srcLocation, &srcBox);

        dxSrc.TransitionStates(this, srcSlice, srcOldStates);
        dxDest.TransitionStates(this, destOldStates);
    }

    protected override void ResolveTextureImpl(Texture src, TextureSlice srcSlice, Texture dest, TextureSlice destSlice)
    {
        DXTexture dxSrc = src.DirectX12();
        DXTexture dxDest = dest.DirectX12();

        ResourceStates srcOldStates = dxSrc.States[ZenithHelper.SubresourceIndex(dxSrc.Desc, srcSlice)];
        ResourceStates destOldStates = dxDest.States[ZenithHelper.SubresourceIndex(dxDest.Desc, destSlice)];

        dxSrc.TransitionStates(this, srcSlice, ResourceStates.CopySource);
        dxDest.TransitionStates(this, destSlice, ResourceStates.CopyDest);

        GraphicsCommandList.ResolveSubresource(dxDest.Resource, ZenithHelper.SubresourceIndex(dxDest.Desc, destSlice), dxSrc.Resource, ZenithHelper.SubresourceIndex(dxSrc.Desc, srcSlice), DXFormats.DirectX12(dxDest.Desc.Format));

        dxSrc.TransitionStates(this, srcSlice, srcOldStates);
        dxDest.TransitionStates(this, destSlice, destOldStates);
    }

    protected override BottomLevelAccelerationStructure BuildAccelerationStructureImpl(BottomLevelAccelerationStructureDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override TopLevelAccelerationStructure BuildAccelerationStructureImpl(TopLevelAccelerationStructureDesc desc)
    {
        throw new NotImplementedException();
    }

    protected override void UpdateAccelerationStructureImpl(TopLevelAccelerationStructure accelerationStructure, TopLevelAccelerationStructureDesc newDesc)
    {
        throw new NotImplementedException();
    }

    protected override void PreprocessResourceSetsImpl(ResourceSet[] resourceSets)
    {
        foreach (ResourceSet resourceSet in resourceSets)
        {
            resourceSet.DirectX12().TransitionStates(this);
        }
    }

    protected override void SetScissorsImpl(Scissor[] scissors)
    {
        Box2D<int>[] dxScissors = [.. scissors.Select(static item => new Box2D<int>(new(item.X, item.Y), new((int)(item.X + item.Width), (int)(item.Y + item.Height))))];

        GraphicsCommandList.RSSetScissorRects((uint)scissors.Length, ref dxScissors[0]);
    }

    protected override void SetViewportsImpl(Viewport[] viewports)
    {
        DxViewport[] dxViewports = [.. viewports.Select(static item => new DxViewport(item.X, item.Y, item.Width, item.Height, item.MinDepth, item.MaxDepth))];

        GraphicsCommandList.RSSetViewports((uint)viewports.Length, ref dxViewports[0]);
    }

    protected override void BindPipelineImpl(GraphicsPipeline pipeline)
    {
        DXGraphicsPipeline dxPipeline = pipeline.DirectX12();

        GraphicsCommandList.SetPipelineState(dxPipeline.PipelineState);
        GraphicsCommandList.SetGraphicsRootSignature(dxPipeline.RootSignature);

        GraphicsCommandList.OMSetStencilRef(dxPipeline.Desc.RenderStates.StencilReference);

        if (dxPipeline.Desc.RenderStates.BlendFactor.HasValue)
        {
            float[] blendFactor =
            [
                dxPipeline.Desc.RenderStates.BlendFactor.Value.X,
                dxPipeline.Desc.RenderStates.BlendFactor.Value.Y,
                dxPipeline.Desc.RenderStates.BlendFactor.Value.Z,
                dxPipeline.Desc.RenderStates.BlendFactor.Value.W
            ];

            GraphicsCommandList.OMSetBlendFactor(ref blendFactor[0]);
        }

        GraphicsCommandList.IASetPrimitiveTopology(DXFormats.DirectX12(pipeline.Desc.PrimitiveTopology).PrimitiveTopology);
    }

    protected override void BindPipelineImpl(ComputePipeline pipeline)
    {
        throw new NotImplementedException();
    }

    protected override void BindPipelineImpl(RayTracingPipeline pipeline)
    {
        throw new NotImplementedException();
    }

    protected override void BindPipelineImpl(MeshShadingPipeline pipeline)
    {
        throw new NotImplementedException();
    }

    protected override void BindVertexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, uint index)
    {
        VertexBufferView view = new()
        {
            BufferLocation = buffer.DirectX12().Resource.GetGPUVirtualAddress() + offsetInBytes,
            SizeInBytes = buffer.Desc.SizeInBytes - offsetInBytes,
            StrideInBytes = pipeline.Desc.InputLayouts[index].StrideInBytes
        };

        GraphicsCommandList.IASetVertexBuffers(index, 1, &view);
    }

    protected override void BindIndexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, IndexFormat format)
    {
        IndexBufferView view = new()
        {
            BufferLocation = buffer.DirectX12().Resource.GetGPUVirtualAddress() + offsetInBytes,
            SizeInBytes = buffer.Desc.SizeInBytes - offsetInBytes,
            Format = DXFormats.DirectX12(format)
        };

        GraphicsCommandList.IASetIndexBuffer(&view);
    }

    protected override void BindResourceSetImpl(Pipeline pipeline, ResourceSet resourceSet, uint index)
    {
        if (cbvSrvUavTable is null || samplerTable is null)
        {
            return;
        }

        (bool isGraphics, uint offset) = pipeline switch
        {
            GraphicsPipeline graphicsPipeline => (true, (uint)graphicsPipeline.Desc.ResourceLayouts.Take((int)index).Sum(static item => item.DirectX12().GraphicsRootParameterCount)),
            ComputePipeline computePipeline => (false, (uint)computePipeline.Desc.ResourceLayouts.Take((int)index).Sum(static item => item.DirectX12().RootParameterCount)),
            RayTracingPipeline rayTracingPipeline => (false, (uint)rayTracingPipeline.Desc.ResourceLayouts.Take((int)index).Sum(static item => item.DirectX12().RootParameterCount)),
            MeshShadingPipeline meshShadingPipeline => (true, (uint)meshShadingPipeline.Desc.ResourceLayouts.Take((int)index).Sum(static item => item.DirectX12().GraphicsRootParameterCount)),
            _ => (true, 0u)
        };

        resourceSet.DirectX12().Bind(this, cbvSrvUavTable, samplerTable, isGraphics, offset);
    }

    protected override void DrawImpl(GraphicsPipeline pipeline, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        GraphicsCommandList.DrawInstanced(vertexCount, instanceCount, firstVertex, firstInstance);
    }

    protected override void DrawIndirectImpl(GraphicsPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        GraphicsCommandList.ExecuteIndirect(Context.DrawSignature, drawCount, indirectBuffer.DirectX12().Resource, offsetInBytes, (ID3D12Resource*)null, 0);
    }

    protected override void DrawIndexedImpl(GraphicsPipeline pipeline, uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance)
    {
        GraphicsCommandList.DrawIndexedInstanced(indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
    }

    protected override void DrawIndexedIndirectImpl(GraphicsPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        GraphicsCommandList.ExecuteIndirect(Context.DrawIndexedSignature, drawCount, indirectBuffer.DirectX12().Resource, offsetInBytes, (ID3D12Resource*)null, 0);
    }

    protected override void DispatchImpl(ComputePipeline pipeline, uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        GraphicsCommandList.Dispatch(groupCountX, groupCountY, groupCountZ);
    }

    protected override void DispatchIndirectImpl(ComputePipeline pipeline, Buffer indirectBuffer, uint offsetInBytes)
    {
        GraphicsCommandList.ExecuteIndirect(Context.DispatchSignature, 1, indirectBuffer.DirectX12().Resource, offsetInBytes, (ID3D12Resource*)null, 0);
    }

    protected override void DispatchRaysImpl(RayTracingPipeline pipeline, uint width, uint height, uint depth)
    {
        throw new NotImplementedException();
    }

    protected override void DispatchMeshImpl(MeshShadingPipeline pipeline, uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        GraphicsCommandList6?.DispatchMesh(groupCountX, groupCountY, groupCountZ);
    }

    protected override void DispatchMeshIndirectImpl(MeshShadingPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint dispatchCount)
    {
        GraphicsCommandList6?.ExecuteIndirect(Context.DispatchMeshSignature, dispatchCount, indirectBuffer.DirectX12().Resource, offsetInBytes, (ID3D12Resource*)null, 0);
    }

    protected override void BeginQueryImpl(QueryHeap queryHeap, uint index)
    {
        throw new NotImplementedException();
    }

    protected override void EndQueryImpl(QueryHeap queryHeap, uint index)
    {
        throw new NotImplementedException();
    }

    protected override void WriteTimestampImpl(QueryHeap queryHeap, uint index)
    {
        throw new NotImplementedException();
    }

    protected override void BeginDebugEventImpl(string label)
    {
        using ZenithMarshal.Scope scope = new();

        uint size = PixHelpers.CalculateEventSize(label);

        ulong* buffer = (ulong*)ZenithMarshal.Allocate<byte>(scope, size);

        PixHelpers.FormatEventToBuffer(buffer, PixHelpers.Event, 0, label);

        GraphicsCommandList.BeginEvent(PixHelpers.Version, buffer, size);
    }

    protected override void EndDebugEventImpl()
    {
        GraphicsCommandList.EndEvent();
    }

    protected override void InsertDebugMarkerImpl(string label)
    {
        using ZenithMarshal.Scope scope = new();

        uint size = PixHelpers.CalculateEventSize(label);

        ulong* buffer = (ulong*)ZenithMarshal.Allocate<byte>(scope, size);

        PixHelpers.FormatEventToBuffer(buffer, PixHelpers.Marker, 0, label);

        GraphicsCommandList.SetMarker(PixHelpers.Version, buffer, size);
    }

    protected override void BeginImpl()
    {
        if (cbvSrvUavTable is null || samplerTable is null)
        {
            return;
        }

        ComPtr<ID3D12DescriptorHeap>[] descriptorHeaps = [cbvSrvUavTable.Heap, samplerTable.Heap];

        fixed (ID3D12DescriptorHeap** ppDescriptorHeaps = descriptorHeaps[0])
        {
            GraphicsCommandList.SetDescriptorHeaps((uint)descriptorHeaps.Length, ppDescriptorHeaps);
        }
    }

    protected override void EndImpl()
    {
        GraphicsCommandList.Close().Success();
    }

    protected override void ResetImpl()
    {
        cbvSrvUavTable?.Reset();
        samplerTable?.Reset();

        CommandAllocator.Reset().Success();
        GraphicsCommandList.Reset(CommandAllocator, (ID3D12PipelineState*)null).Success();
    }

    protected override void BeginRenderingImpl(FrameBuffer frameBuffer, ClearValue? clearValue)
    {
        DXFrameBuffer dxFrameBuffer = frameBuffer.DirectX12();

        dxFrameBuffer.PrepareAttachmentsForRendering(this);

        GraphicsCommandList.OMSetRenderTargets(dxFrameBuffer.ColorAttachmentCount, dxFrameBuffer.RtvHandles, false, dxFrameBuffer.DsvHandle);

        if (clearValue.HasValue)
        {
            bool clearColor = clearValue.Value.Flags.HasFlag(ClearFlags.Color);
            bool clearDepth = clearValue.Value.Flags.HasFlag(ClearFlags.Depth);
            bool clearStencil = clearValue.Value.Flags.HasFlag(ClearFlags.Stencil);

            if (clearColor)
            {
                for (int i = 0; i < dxFrameBuffer.ColorAttachmentCount; i++)
                {
                    ref float x = ref clearValue.Value.ColorValues[i].X;

                    GraphicsCommandList.ClearRenderTargetView(dxFrameBuffer.RtvHandles[i], ref x, 0, null);
                }
            }

            if ((clearDepth || clearStencil) && dxFrameBuffer.HasDepthStencilAttachment)
            {
                DxClearFlags clearFlags = (DxClearFlags)((clearDepth ? (int)DxClearFlags.Depth : 0) + (clearDepth ? (int)DxClearFlags.Stencil : 0));

                GraphicsCommandList.ClearDepthStencilView(*dxFrameBuffer.DsvHandle, clearFlags, clearValue.Value.Depth, clearValue.Value.Stencil, 0, (Box2D<int>*)null);
            }
        }
    }

    protected override void EndRenderingImpl(FrameBuffer frameBuffer)
    {
        frameBuffer.DirectX12().FinalizeColorAttachmentsForPresent(this);
    }

    protected override void SetResourceName(string name)
    {
        CommandList.SetName(name).Success();
    }

    protected override void Destroy()
    {
        base.Destroy();

        GraphicsCommandList6?.Dispose();
        GraphicsCommandList4?.Dispose();
        GraphicsCommandList.Dispose();

        CommandList.Dispose();

        CommandAllocator.Dispose();

        samplerTable?.Dispose();
        cbvSrvUavTable?.Dispose();
    }
}
