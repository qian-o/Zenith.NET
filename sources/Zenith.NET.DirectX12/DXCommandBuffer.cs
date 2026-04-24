using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;
using Silk.NET.Maths;

namespace Zenith.NET.DirectX12;

internal unsafe class DXCommandBuffer : CommandBuffer
{
    private const uint TextureDataPitchAlignment = 256;

    private const uint TextureDataPlacementAlignment = 512;

    private readonly DXDescriptorTable? cbvSrvUavTable;
    private readonly DXDescriptorTable? samplerTable;
    private DXDescriptorToken[] currentRenderPassTokens = [];

    public ComPtr<ID3D12CommandAllocator> CommandAllocator;

    public ComPtr<ID3D12CommandList> CommandList;

    public ComPtr<ID3D12GraphicsCommandList4> GraphicsCommandList4;

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

        CommandList.QueryInterface(out GraphicsCommandList4).Success();

        if (CommandList.QueryInterface(out ComPtr<ID3D12GraphicsCommandList6> graphicsCommandList6).IsSuccess())
        {
            GraphicsCommandList6 = graphicsCommandList6;
        }
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    protected override void CopyBufferImpl(Buffer src, uint srcOffsetInBytes, Buffer dest, uint destOffsetInBytes, uint sizeInBytes)
    {
        GraphicsCommandList4.CopyBufferRegion(dest.DirectX12().Resource, destOffsetInBytes, src.DirectX12().Resource, srcOffsetInBytes, sizeInBytes);
    }

    protected override void CopyBufferToTextureImpl(Buffer src,
                                                    uint srcOffsetInBytes,
                                                    TextureDataLayout srcLayout,
                                                    Texture dest,
                                                    TextureSubresource destSubresource,
                                                    Offset3D destOffset,
                                                    Extent3D destExtent)
    {
        DXBuffer dxSrc = src.DirectX12();
        DXTexture dxDest = dest.DirectX12();

        (uint blockWidth, uint blockHeight, uint blocksWide, _) = ZenithHelper.BlockLayout(dxDest.Desc.Format, destExtent.Width, destExtent.Height);

        uint offsetX = ZenithHelper.Align(destOffset.X, blockWidth);
        uint offsetY = ZenithHelper.Align(destOffset.Y, blockHeight);
        uint extentWidth = ZenithHelper.Align(destExtent.Width, blockWidth);
        uint extentHeight = ZenithHelper.Align(destExtent.Height, blockHeight);

        for (uint i = 0; i < destExtent.Depth; i++)
        {
            TextureCopyLocation srcLocation = new()
            {
                PResource = dxSrc.Resource,
                Type = TextureCopyType.PlacedFootprint,
                PlacedFootprint = new()
                {
                    Offset = srcOffsetInBytes + (srcLayout.SlicePitchInBytes * i),
                    Footprint = new()
                    {
                        Format = DXFormats.DirectX12(dxDest.Desc.Format),
                        Width = extentWidth,
                        Height = extentHeight,
                        Depth = 1,
                        RowPitch = srcLayout.RowPitchInBytes
                    }
                }
            };

            Box srcBox = new(0, 0, 0, extentWidth, extentHeight, 1);

            TextureCopyLocation destLocation = new()
            {
                PResource = dxDest.Resource,
                Type = TextureCopyType.SubresourceIndex,
                SubresourceIndex = dxDest.SubresourceIndex(destSubresource)
            };

            GraphicsCommandList4.CopyTextureRegion(&destLocation, offsetX, offsetY, destOffset.Z + i, &srcLocation, &srcBox);
        }
    }

    protected override void CopyTextureImpl(Texture src,
                                            TextureSubresource srcSubresource,
                                            Offset3D srcOffset,
                                            Texture dest,
                                            TextureSubresource destSubresource,
                                            Offset3D destOffset,
                                            Extent3D extent)
    {
        DXTexture dxSrc = src.DirectX12();
        DXTexture dxDest = dest.DirectX12();

        TextureCopyLocation srcLocation = new()
        {
            PResource = dxSrc.Resource,
            Type = TextureCopyType.SubresourceIndex,
            SubresourceIndex = dxSrc.SubresourceIndex(srcSubresource)
        };

        Box srcBox = new(srcOffset.X, srcOffset.Y, srcOffset.Z, srcOffset.X + extent.Width, srcOffset.Y + extent.Height, srcOffset.Z + extent.Depth);

        TextureCopyLocation destLocation = new()
        {
            PResource = dxDest.Resource,
            Type = TextureCopyType.SubresourceIndex,
            SubresourceIndex = dxDest.SubresourceIndex(destSubresource)
        };

        GraphicsCommandList4.CopyTextureRegion(&destLocation, destOffset.X, destOffset.Y, destOffset.Z, &srcLocation, &srcBox);
    }

    protected override void CopyTextureToBufferImpl(Texture src,
                                                    TextureSubresource srcSubresource,
                                                    Offset3D srcOffset,
                                                    Extent3D srcExtent,
                                                    Buffer dest,
                                                    uint destOffsetInBytes,
                                                    TextureDataLayout destLayout)
    {
        DXTexture dxSrc = src.DirectX12();
        DXBuffer dxDest = dest.DirectX12();

        (uint blockWidth, uint blockHeight, uint blocksWide, _) = ZenithHelper.BlockLayout(dxSrc.Desc.Format, srcExtent.Width, srcExtent.Height);

        uint offsetX = ZenithHelper.Align(srcOffset.X, blockWidth);
        uint offsetY = ZenithHelper.Align(srcOffset.Y, blockHeight);
        uint extentWidth = ZenithHelper.Align(srcExtent.Width, blockWidth);
        uint extentHeight = ZenithHelper.Align(srcExtent.Height, blockHeight);

        TextureCopyLocation srcLocation = new()
        {
            PResource = dxSrc.Resource,
            Type = TextureCopyType.SubresourceIndex,
            SubresourceIndex = dxSrc.SubresourceIndex(srcSubresource)
        };

        Box srcBox = new(offsetX, offsetY, srcOffset.Z, offsetX + extentWidth, offsetY + extentHeight, srcOffset.Z + srcExtent.Depth);

        for (uint i = 0; i < srcExtent.Depth; i++)
        {
            Box sliceBox = new(offsetX, offsetY, srcOffset.Z + i, offsetX + extentWidth, offsetY + extentHeight, srcOffset.Z + i + 1);

            TextureCopyLocation destLocation = new()
            {
                PResource = dxDest.Resource,
                Type = TextureCopyType.PlacedFootprint,
                PlacedFootprint = new()
                {
                    Offset = destOffsetInBytes + (destLayout.SlicePitchInBytes * i),
                    Footprint = new()
                    {
                        Format = DXFormats.DirectX12(dxSrc.Desc.Format),
                        Width = extentWidth,
                        Height = extentHeight,
                        Depth = 1,
                        RowPitch = destLayout.RowPitchInBytes
                    }
                }
            };

            GraphicsCommandList4.CopyTextureRegion(&destLocation, 0, 0, 0, &srcLocation, &sliceBox);
        }
    }

    protected override TextureDataLayout GetTextureCopyLayout(PixelFormat format, Extent3D extent)
    {
        uint rowPitchInBytes = ZenithHelper.Align(ZenithHelper.RowPitchInBytes(format, extent.Width, extent.Height), TextureDataPitchAlignment);
        (_, _, _, uint blocksHigh) = ZenithHelper.BlockLayout(format, extent.Width, extent.Height);

        uint slicePitchInBytes = ZenithHelper.Align(rowPitchInBytes * blocksHigh, TextureDataPlacementAlignment);

        return new()
        {
            SizeInBytes = slicePitchInBytes * extent.Depth,
            RowPitchInBytes = rowPitchInBytes,
            SlicePitchInBytes = slicePitchInBytes
        };
    }

    protected override void ResolveTextureImpl(Texture src,
                                               TextureSubresource srcSubresource,
                                               Texture dest,
                                               TextureSubresource destSubresource)
    {
        DXTexture dxSrc = src.DirectX12();
        DXTexture dxDest = dest.DirectX12();

        GraphicsCommandList4.ResolveSubresource(dxDest.Resource, dxDest.SubresourceIndex(destSubresource), dxSrc.Resource, dxSrc.SubresourceIndex(srcSubresource), DXFormats.DirectX12(dxDest.Desc.Format));
    }

    protected override BottomLevelAccelerationStructure BuildAccelerationStructureImpl(BottomLevelAccelerationStructureDesc desc)
    {
        return new DXBottomLevelAccelerationStructure(Context, desc, this);
    }

    protected override TopLevelAccelerationStructure BuildAccelerationStructureImpl(TopLevelAccelerationStructureDesc desc)
    {
        return new DXTopLevelAccelerationStructure(Context, desc, this);
    }

    protected override void UpdateAccelerationStructureImpl(TopLevelAccelerationStructure accelerationStructure, TopLevelAccelerationStructureDesc newDesc)
    {
        accelerationStructure.DirectX12().Update(this, newDesc);
    }

    protected override void BeginRenderPassImpl(ReadOnlySpan<ColorAttachment> colorAttachments,
                                                DepthStencilAttachment? depthStencilAttachment)
    {
        ReleaseRenderPassTokens();

        using ZenithMarshal.Scope scope = new();

        uint colorAttachmentCount = (uint)colorAttachments.Length;
        bool hasDepthStencilAttachment = depthStencilAttachment is not null;

        RenderPassRenderTargetDesc* renderTargets = (RenderPassRenderTargetDesc*)ZenithMarshal.Allocate<RenderPassRenderTargetDesc>(scope, colorAttachmentCount);
        RenderPassDepthStencilDesc* depthStencil = hasDepthStencilAttachment ? (RenderPassDepthStencilDesc*)ZenithMarshal.Allocate<RenderPassDepthStencilDesc>(scope, 1) : null;

        currentRenderPassTokens = new DXDescriptorToken[colorAttachmentCount + (hasDepthStencilAttachment ? 1 : 0)];

        for (uint i = 0; i < colorAttachmentCount; i++)
        {
            ColorAttachment attachment = colorAttachments[(int)i];

            renderTargets[i] = new()
            {
                CpuDescriptor = (currentRenderPassTokens[i] = attachment.Texture.DirectX12().CreateRtvToken(attachment.Subresource)).Handle,
                BeginningAccess = new()
                {
                    Type = RenderPassBeginningAccessType.Preserve
                },
                EndingAccess = new()
                {
                    Type = attachment.StoreOp is StoreOp.Store ? RenderPassEndingAccessType.Preserve : RenderPassEndingAccessType.Discard
                }
            };

            ref RenderPassRenderTargetDesc renderTarget = ref renderTargets[i];

            switch (attachment.LoadOp)
            {
                case LoadOp.Clear:
                    renderTarget.BeginningAccess.Type = RenderPassBeginningAccessType.Clear;
                    renderTarget.BeginningAccess.Clear.ClearValue.Format = DXFormats.DirectX12(attachment.Texture.Desc.Format);

                    fixed (float* colorPtr = renderTarget.BeginningAccess.Clear.ClearValue.Anonymous.Color)
                    {
                        attachment.ClearColor.CopyTo(new Span<float>(colorPtr, 4));
                    }
                    break;

                case LoadOp.DontCare:
                    renderTarget.BeginningAccess.Type = RenderPassBeginningAccessType.Discard;
                    break;
            }
        }

        if (hasDepthStencilAttachment)
        {
            DepthStencilAttachment attachment = depthStencilAttachment!.Value;
            PixelFormat format = attachment.Texture.Desc.Format;
            bool hasDepth = ZenithHelper.HasDepth(format);
            bool hasStencil = ZenithHelper.HasStencil(format);

            depthStencil[0] = new()
            {
                CpuDescriptor = (currentRenderPassTokens[colorAttachmentCount] = attachment.Texture.DirectX12().CreateDsvToken(attachment.Subresource)).Handle,
                DepthBeginningAccess = new()
                {
                    Type = hasDepth ? RenderPassBeginningAccessType.Preserve : RenderPassBeginningAccessType.NoAccess
                },
                StencilBeginningAccess = new()
                {
                    Type = hasStencil ? RenderPassBeginningAccessType.Preserve : RenderPassBeginningAccessType.NoAccess
                },
                DepthEndingAccess = new()
                {
                    Type = hasDepth ? attachment.DepthStoreOp is StoreOp.Store ? RenderPassEndingAccessType.Preserve : RenderPassEndingAccessType.Discard : RenderPassEndingAccessType.NoAccess
                },
                StencilEndingAccess = new()
                {
                    Type = hasStencil ? attachment.StencilStoreOp is StoreOp.Store ? RenderPassEndingAccessType.Preserve : RenderPassEndingAccessType.Discard : RenderPassEndingAccessType.NoAccess
                }
            };

            ref RenderPassDepthStencilDesc depthStencilDesc = ref depthStencil[0];

            if (depthStencilDesc.DepthBeginningAccess.Type is not RenderPassBeginningAccessType.NoAccess)
            {
                switch (attachment.DepthLoadOp)
                {
                    case LoadOp.Clear:
                        depthStencilDesc.DepthBeginningAccess.Type = RenderPassBeginningAccessType.Clear;
                        depthStencilDesc.DepthBeginningAccess.Clear.ClearValue.Format = DXFormats.DirectX12(format);
                        depthStencilDesc.DepthBeginningAccess.Clear.ClearValue.DepthStencil.Depth = attachment.ClearDepth;
                        break;

                    case LoadOp.DontCare:
                        depthStencilDesc.DepthBeginningAccess.Type = RenderPassBeginningAccessType.Discard;
                        break;
                }
            }

            if (depthStencilDesc.StencilBeginningAccess.Type is not RenderPassBeginningAccessType.NoAccess)
            {
                switch (attachment.StencilLoadOp)
                {
                    case LoadOp.Clear:
                        depthStencilDesc.StencilBeginningAccess.Type = RenderPassBeginningAccessType.Clear;
                        depthStencilDesc.StencilBeginningAccess.Clear.ClearValue.Format = DXFormats.DirectX12(format);
                        depthStencilDesc.StencilBeginningAccess.Clear.ClearValue.DepthStencil.Stencil = attachment.ClearStencil;
                        break;

                    case LoadOp.DontCare:
                        depthStencilDesc.StencilBeginningAccess.Type = RenderPassBeginningAccessType.Discard;
                        break;
                }
            }
        }

        GraphicsCommandList4.BeginRenderPass(colorAttachmentCount, renderTargets, depthStencil, RenderPassFlags.None);
    }

    protected override void EndRenderPassImpl()
    {
        GraphicsCommandList4.EndRenderPass();

        ReleaseRenderPassTokens();
    }

    protected override void SetScissorsImpl(ReadOnlySpan<Scissor> scissors)
    {
        Span<Box2D<int>> dxScissors = scissors.Length <= 8 ? stackalloc Box2D<int>[scissors.Length] : new Box2D<int>[scissors.Length];

        for (int i = 0; i < scissors.Length; i++)
        {
            Scissor scissor = scissors[i];

            dxScissors[i] = new(new(scissor.X, scissor.Y), new((int)(scissor.X + scissor.Width), (int)(scissor.Y + scissor.Height)));
        }

        GraphicsCommandList4.RSSetScissorRects((uint)dxScissors.Length, ref dxScissors[0]);
    }

    protected override void SetViewportsImpl(ReadOnlySpan<Viewport> viewports)
    {
        Span<DxViewport> dxViewports = viewports.Length <= 8 ? stackalloc DxViewport[viewports.Length] : new DxViewport[viewports.Length];

        for (int i = 0; i < viewports.Length; i++)
        {
            Viewport viewport = viewports[i];

            dxViewports[i] = new(viewport.X, viewport.Y, viewport.Width, viewport.Height, viewport.MinDepth, viewport.MaxDepth);
        }

        GraphicsCommandList4.RSSetViewports((uint)dxViewports.Length, ref dxViewports[0]);
    }

    protected override void SetPipelineImpl(GraphicsPipeline pipeline)
    {
        DXGraphicsPipeline dxPipeline = pipeline.DirectX12();

        GraphicsCommandList4.SetPipelineState(dxPipeline.PipelineState);
        GraphicsCommandList4.SetGraphicsRootSignature(dxPipeline.RootSignature);

        GraphicsCommandList4.OMSetStencilRef(dxPipeline.Desc.RenderStates.StencilReference);

        if (dxPipeline.Desc.RenderStates.BlendFactor.HasValue)
        {
            float[] blendFactor =
            [
                dxPipeline.Desc.RenderStates.BlendFactor.Value.X,
                dxPipeline.Desc.RenderStates.BlendFactor.Value.Y,
                dxPipeline.Desc.RenderStates.BlendFactor.Value.Z,
                dxPipeline.Desc.RenderStates.BlendFactor.Value.W
            ];

            GraphicsCommandList4.OMSetBlendFactor(ref blendFactor[0]);
        }

        GraphicsCommandList4.IASetPrimitiveTopology(DXFormats.DirectX12(pipeline.Desc.PrimitiveTopology).PrimitiveTopology);
    }

    protected override void SetPipelineImpl(ComputePipeline pipeline)
    {
        DXComputePipeline dxPipeline = pipeline.DirectX12();

        GraphicsCommandList4.SetPipelineState(dxPipeline.PipelineState);
        GraphicsCommandList4.SetComputeRootSignature(dxPipeline.RootSignature);
    }

    protected override void SetPipelineImpl(MeshShadingPipeline pipeline)
    {
        DXMeshShadingPipeline dxPipeline = pipeline.DirectX12();

        GraphicsCommandList4.SetPipelineState(dxPipeline.PipelineState);
        GraphicsCommandList4.SetGraphicsRootSignature(dxPipeline.RootSignature);

        GraphicsCommandList4.OMSetStencilRef(dxPipeline.Desc.RenderStates.StencilReference);

        if (dxPipeline.Desc.RenderStates.BlendFactor.HasValue)
        {
            float[] blendFactor =
            [
                dxPipeline.Desc.RenderStates.BlendFactor.Value.X,
                dxPipeline.Desc.RenderStates.BlendFactor.Value.Y,
                dxPipeline.Desc.RenderStates.BlendFactor.Value.Z,
                dxPipeline.Desc.RenderStates.BlendFactor.Value.W
            ];

            GraphicsCommandList4.OMSetBlendFactor(ref blendFactor[0]);
        }
    }

    protected override void SetVertexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, uint index)
    {
        VertexBufferView view = new()
        {
            BufferLocation = buffer.DirectX12().GPUVirtualAddress + offsetInBytes,
            SizeInBytes = buffer.Desc.SizeInBytes - offsetInBytes,
            StrideInBytes = pipeline.Desc.InputLayouts[index].StrideInBytes
        };

        GraphicsCommandList4.IASetVertexBuffers(index, 1, &view);
    }

    protected override void SetIndexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, IndexFormat format)
    {
        IndexBufferView view = new()
        {
            BufferLocation = buffer.DirectX12().GPUVirtualAddress + offsetInBytes,
            SizeInBytes = buffer.Desc.SizeInBytes - offsetInBytes,
            Format = DXFormats.DirectX12(format)
        };

        GraphicsCommandList4.IASetIndexBuffer(&view);
    }

    protected override void PushResourceTableImpl(Pipeline pipeline, ResourceTable resourceTable)
    {
        if (cbvSrvUavTable is null || samplerTable is null)
        {
            return;
        }

        resourceTable.DirectX12().Bind(this, cbvSrvUavTable, samplerTable, pipeline is GraphicsPipeline or MeshShadingPipeline);
    }

    protected override void DrawImpl(GraphicsPipeline pipeline, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        GraphicsCommandList4.DrawInstanced(vertexCount, instanceCount, firstVertex, firstInstance);
    }

    protected override void DrawIndirectImpl(GraphicsPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        GraphicsCommandList4.ExecuteIndirect(Context.DrawSignature, drawCount, indirectBuffer.DirectX12().Resource, offsetInBytes, (ID3D12Resource*)null, 0);
    }

    protected override void DrawIndexedImpl(GraphicsPipeline pipeline, uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance)
    {
        GraphicsCommandList4.DrawIndexedInstanced(indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
    }

    protected override void DrawIndexedIndirectImpl(GraphicsPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        GraphicsCommandList4.ExecuteIndirect(Context.DrawIndexedSignature, drawCount, indirectBuffer.DirectX12().Resource, offsetInBytes, (ID3D12Resource*)null, 0);
    }

    protected override void DispatchImpl(ComputePipeline pipeline, uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        GraphicsCommandList4.Dispatch(groupCountX, groupCountY, groupCountZ);
    }

    protected override void DispatchIndirectImpl(ComputePipeline pipeline, Buffer indirectBuffer, uint offsetInBytes)
    {
        GraphicsCommandList4.ExecuteIndirect(Context.DispatchSignature, 1, indirectBuffer.DirectX12().Resource, offsetInBytes, (ID3D12Resource*)null, 0);
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
        GraphicsCommandList4.BeginQuery(queryHeap.DirectX12().QueryHeap, DXFormats.DirectX12(queryHeap.Desc.Type).Type, index);
    }

    protected override void EndQueryImpl(QueryHeap queryHeap, uint index)
    {
        DXQueryHeap dxQueryHeap = queryHeap.DirectX12();

        GraphicsCommandList4.EndQuery(dxQueryHeap.QueryHeap, DXFormats.DirectX12(dxQueryHeap.Desc.Type).Type, index);

        GraphicsCommandList4.ResolveQueryData(dxQueryHeap.QueryHeap, DXFormats.DirectX12(dxQueryHeap.Desc.Type).Type, index, 1, dxQueryHeap.Buffer.Resource, sizeof(ulong) * index);
    }

    protected override void WriteTimestampImpl(QueryHeap queryHeap, uint index)
    {
        DXQueryHeap dxQueryHeap = queryHeap.DirectX12();

        GraphicsCommandList4.EndQuery(dxQueryHeap.QueryHeap, DxQueryType.Timestamp, index);

        GraphicsCommandList4.ResolveQueryData(dxQueryHeap.QueryHeap, DxQueryType.Timestamp, index, 1, dxQueryHeap.Buffer.Resource, sizeof(ulong) * index);
    }

    protected override void BeginDebugEventImpl(string label)
    {
        using ZenithMarshal.Scope scope = new();

        uint size = PixHelpers.CalculateEventSize(label);

        ulong* buffer = (ulong*)ZenithMarshal.Allocate<byte>(scope, size);

        PixHelpers.FormatEventToBuffer(buffer, PixHelpers.Event, 0, label);

        GraphicsCommandList4.BeginEvent(PixHelpers.Version, buffer, size);
    }

    protected override void EndDebugEventImpl()
    {
        GraphicsCommandList4.EndEvent();
    }

    protected override void InsertDebugMarkerImpl(string label)
    {
        using ZenithMarshal.Scope scope = new();

        uint size = PixHelpers.CalculateEventSize(label);

        ulong* buffer = (ulong*)ZenithMarshal.Allocate<byte>(scope, size);

        PixHelpers.FormatEventToBuffer(buffer, PixHelpers.Marker, 0, label);

        GraphicsCommandList4.SetMarker(PixHelpers.Version, buffer, size);
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
            GraphicsCommandList4.SetDescriptorHeaps((uint)descriptorHeaps.Length, ppDescriptorHeaps);
        }
    }

    protected override void EndImpl()
    {
        GraphicsCommandList4.Close().Success();
    }

    protected override void ResetImpl()
    {
        ReleaseRenderPassTokens();

        cbvSrvUavTable?.Reset();
        samplerTable?.Reset();

        CommandAllocator.Reset().Success();
        GraphicsCommandList4.Reset(CommandAllocator, (ID3D12PipelineState*)null).Success();
    }

    protected override void SetResourceName(string name)
    {
        CommandList.SetName(name).Success();
    }

    protected override void Destroy()
    {
        ReleaseRenderPassTokens();

        base.Destroy();

        GraphicsCommandList6?.Dispose();
        GraphicsCommandList4.Dispose();
        CommandList.Dispose();
        CommandAllocator.Dispose();

        samplerTable?.Dispose();
        cbvSrvUavTable?.Dispose();
    }

    private void ReleaseRenderPassTokens()
    {
        foreach (DXDescriptorToken token in currentRenderPassTokens)
        {
            token.Dispose();
        }

        currentRenderPassTokens = [];
    }
}
