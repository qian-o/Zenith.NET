using System.Numerics;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.Maths;

namespace Zenith.NET.DirectX12;

internal unsafe class DXCommandBuffer : CommandBuffer
{
    public ComPtr<ID3D12CommandAllocator> CommandAllocator;

    public ComPtr<ID3D12GraphicsCommandList7> CommandList;

    public DXCommandBuffer(DXGraphicsContext context, CommandQueue queue) : base(context, queue)
    {
        context.Device.CreateCommandAllocator(DXFormats.DirectX12(queue.Type), SilkMarshal.GuidPtrOf<ID3D12CommandAllocator>(), (void**)CommandAllocator.GetAddressOf()).Success();

        context.Device.CreateCommandList(0, DXFormats.DirectX12(queue.Type), CommandAllocator, default(ID3D12PipelineState*), SilkMarshal.GuidPtrOf<ID3D12GraphicsCommandList7>(), (void**)CommandList.GetAddressOf()).Success();
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void BarrierImpl(BarrierStages before, BarrierStages after)
    {
        (BarrierSync syncBefore, BarrierAccess accessBefore) = DXFormats.DirectX12(before);
        (BarrierSync syncAfter, BarrierAccess accessAfter) = DXFormats.DirectX12(after);

        GlobalBarrier barrier = new()
        {
            SyncBefore = syncBefore,
            SyncAfter = syncAfter,
            AccessBefore = accessBefore,
            AccessAfter = accessAfter
        };

        BarrierGroup barrierGroup = new()
        {
            Type = BarrierType.Global,
            NumBarriers = 1,
            PGlobalBarriers = &barrier
        };

        CommandList.Barrier(1, &barrierGroup);
    }

    protected override void TransitionImpl(Texture texture, TextureSubresource subresource, TextureLayout srcLayout, TextureLayout dstLayout)
    {
        DXTexture dxTexture = texture.DirectX12();

        (BarrierSync syncBefore, BarrierAccess accessBefore, BarrierLayout layoutBefore) = DXFormats.DirectX12(srcLayout);
        (BarrierSync syncAfter, BarrierAccess accessAfter, BarrierLayout layoutAfter) = DXFormats.DirectX12(dstLayout);

        DxTextureBarrier barrier = new()
        {
            SyncBefore = syncBefore,
            SyncAfter = syncAfter,
            AccessBefore = accessBefore,
            AccessAfter = accessAfter,
            LayoutBefore = layoutBefore,
            LayoutAfter = layoutAfter,
            PResource = dxTexture.Resource,
            Subresources = new()
            {
                IndexOrFirstMipLevel = subresource.MipLevel,
                NumMipLevels = 1,
                FirstArraySlice = subresource.ArrayLayer,
                NumArraySlices = 1,
                NumPlanes = ZenithHelper.HasStencil(dxTexture.Desc.Format) ? 2u : 1u
            }
        };

        BarrierGroup barrierGroup = new()
        {
            Type = BarrierType.Texture,
            NumBarriers = 1,
            PTextureBarriers = &barrier
        };

        CommandList.Barrier(1, &barrierGroup);
    }

    protected override void CopyBufferImpl(Buffer src, uint srcOffsetInBytes, Buffer dst, uint dstOffsetInBytes, uint sizeInBytes)
    {
        DXBuffer dxSrc = src.DirectX12();
        DXBuffer dxDst = dst.DirectX12();

        CommandList.CopyBufferRegion(dxDst.Resource, dstOffsetInBytes, dxSrc.Resource, srcOffsetInBytes, sizeInBytes);
    }

    protected override void CopyBufferToTextureImpl(Buffer src, uint srcOffsetInBytes, uint srcRowStrideInBytes, uint srcSliceStrideInBytes, Texture dst, TextureSubresource dstSubresource, Offset3D dstOffset, Extent3D dstExtent)
    {
        DXBuffer dxSrc = src.DirectX12();
        DXTexture dxDst = dst.DirectX12();

        TextureCopyLocation srcLocation = new()
        {
            PResource = dxSrc.Resource,
            Type = TextureCopyType.PlacedFootprint,
            PlacedFootprint = new()
            {
                Offset = srcOffsetInBytes,
                Footprint = new()
                {
                    Format = DXFormats.DirectX12(dxDst.Desc.Format),
                    Width = dstExtent.Width,
                    Height = dstExtent.Height,
                    Depth = dstExtent.Depth,
                    RowPitch = srcRowStrideInBytes
                }
            }
        };

        TextureCopyLocation dstLocation = new()
        {
            PResource = dxDst.Resource,
            Type = TextureCopyType.SubresourceIndex,
            SubresourceIndex = dxDst.SubresourceIndex(dstSubresource)
        };

        CommandList.CopyTextureRegion(&dstLocation, dstOffset.X, dstOffset.Y, dstOffset.Z, &srcLocation, default(Box*));
    }

    protected override void CopyTextureImpl(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Texture dst, TextureSubresource dstSubresource, Offset3D dstOffset, Extent3D extent)
    {
        DXTexture dxSrc = src.DirectX12();
        DXTexture dxDst = dst.DirectX12();

        TextureCopyLocation srcLocation = new()
        {
            PResource = dxSrc.Resource,
            Type = TextureCopyType.SubresourceIndex,
            SubresourceIndex = dxSrc.SubresourceIndex(srcSubresource)
        };

        Box srcBox = new()
        {
            Left = srcOffset.X,
            Top = srcOffset.Y,
            Front = srcOffset.Z,
            Right = srcOffset.X + extent.Width,
            Bottom = srcOffset.Y + extent.Height,
            Back = srcOffset.Z + extent.Depth
        };

        TextureCopyLocation dstLocation = new()
        {
            PResource = dxDst.Resource,
            Type = TextureCopyType.SubresourceIndex,
            SubresourceIndex = dxDst.SubresourceIndex(dstSubresource)
        };

        CommandList.CopyTextureRegion(&dstLocation, dstOffset.X, dstOffset.Y, dstOffset.Z, &srcLocation, &srcBox);
    }

    protected override void CopyTextureToBufferImpl(Texture src, TextureSubresource srcSubresource, Offset3D srcOffset, Extent3D srcExtent, Buffer dst, uint dstOffsetInBytes, uint dstRowStrideInBytes, uint dstSliceStrideInBytes)
    {
        DXTexture dxSrc = src.DirectX12();
        DXBuffer dxDst = dst.DirectX12();

        TextureCopyLocation srcLocation = new()
        {
            PResource = dxSrc.Resource,
            Type = TextureCopyType.SubresourceIndex,
            SubresourceIndex = dxSrc.SubresourceIndex(srcSubresource)
        };

        Box srcBox = new()
        {
            Left = srcOffset.X,
            Top = srcOffset.Y,
            Front = srcOffset.Z,
            Right = srcOffset.X + srcExtent.Width,
            Bottom = srcOffset.Y + srcExtent.Height,
            Back = srcOffset.Z + srcExtent.Depth
        };

        TextureCopyLocation dstLocation = new()
        {
            PResource = dxDst.Resource,
            Type = TextureCopyType.PlacedFootprint,
            PlacedFootprint = new()
            {
                Offset = dstOffsetInBytes,
                Footprint = new()
                {
                    Format = DXFormats.DirectX12(dxSrc.Desc.Format),
                    Width = srcExtent.Width,
                    Height = srcExtent.Height,
                    Depth = srcExtent.Depth,
                    RowPitch = dstRowStrideInBytes
                }
            }
        };

        CommandList.CopyTextureRegion(&dstLocation, 0, 0, 0, &srcLocation, &srcBox);
    }

    protected override void ResolveTextureImpl(Texture src, TextureSubresource srcSubresource, Texture dst, TextureSubresource dstSubresource)
    {
        DXTexture dxSrc = src.DirectX12();
        DXTexture dxDst = dst.DirectX12();

        CommandList.ResolveSubresource(dxDst.Resource, dxDst.SubresourceIndex(dstSubresource), dxSrc.Resource, dxSrc.SubresourceIndex(srcSubresource), DXFormats.DirectX12(dxDst.Desc.Format));
    }

    protected override BottomLevelAccelerationStructure BuildAccelerationStructureImpl(BottomLevelAccelerationStructureDesc desc)
    {
        return new DXBottomLevelAccelerationStructure(Context, this, desc);
    }

    protected override TopLevelAccelerationStructure BuildAccelerationStructureImpl(TopLevelAccelerationStructureDesc desc)
    {
        return new DXTopLevelAccelerationStructure(Context, this, desc);
    }

    protected override void UpdateAccelerationStructureImpl(BottomLevelAccelerationStructure accelerationStructure, BottomLevelAccelerationStructureDesc newDesc)
    {
        accelerationStructure.DirectX12().Update(this, newDesc);
    }

    protected override void UpdateAccelerationStructureImpl(TopLevelAccelerationStructure accelerationStructure, TopLevelAccelerationStructureDesc newDesc)
    {
        accelerationStructure.DirectX12().Update(this, newDesc);
    }

    protected override void BeginRenderPassImpl(ReadOnlySpan<ColorAttachment> colorAttachments, DepthStencilAttachment? depthStencilAttachment)
    {
        RenderPassRenderTargetDesc* pRenderTargets = stackalloc RenderPassRenderTargetDesc[colorAttachments.Length];
        for (int i = 0; i < colorAttachments.Length; i++)
        {
            ColorAttachment attachment = colorAttachments[i];

            DXTexture texture = attachment.Texture.DirectX12();

            ClearValue clearValue = new()
            {
                Format = DXFormats.DirectX12(texture.Desc.Format)
            };
            *(Vector4*)clearValue.Anonymous.Color = attachment.ClearColor;

            pRenderTargets[i] = new()
            {
                CpuDescriptor = texture.GetRtvHandle(attachment.Subresource),
                BeginningAccess = new()
                {
                    Type = DXFormats.DirectX12(attachment.LoadOp),
                    Clear = new()
                    {
                        ClearValue = clearValue
                    }
                },
                EndingAccess = new()
                {
                    Type = DXFormats.DirectX12(attachment.StoreOp)
                }
            };
        }

        RenderPassDepthStencilDesc* pDepthStencil = stackalloc RenderPassDepthStencilDesc[depthStencilAttachment.HasValue ? 1 : 0];
        if (depthStencilAttachment.HasValue)
        {
            DepthStencilAttachment attachment = depthStencilAttachment.Value;

            DXTexture texture = attachment.Texture.DirectX12();

            ClearValue clearValue = new()
            {
                Format = DXFormats.DirectX12(texture.Desc.Format),
                DepthStencil = new()
                {
                    Depth = attachment.ClearDepth,
                    Stencil = attachment.ClearStencil
                }
            };

            pDepthStencil[0] = new()
            {
                CpuDescriptor = texture.GetDsvHandle(attachment.Subresource),
                DepthBeginningAccess = new()
                {
                    Type = DXFormats.DirectX12(attachment.DepthLoadOp),
                    Clear = new()
                    {
                        ClearValue = clearValue
                    }
                },
                DepthEndingAccess = new()
                {
                    Type = ZenithHelper.HasDepth(texture.Desc.Format) ? DXFormats.DirectX12(attachment.DepthStoreOp) : RenderPassEndingAccessType.NoAccess
                },
                StencilBeginningAccess = new()
                {
                    Type = ZenithHelper.HasStencil(texture.Desc.Format) ? DXFormats.DirectX12(attachment.StencilLoadOp) : RenderPassBeginningAccessType.NoAccess,
                    Clear = new()
                    {
                        ClearValue = clearValue
                    }
                },
                StencilEndingAccess = new()
                {
                    Type = ZenithHelper.HasStencil(texture.Desc.Format) ? DXFormats.DirectX12(attachment.StencilStoreOp) : RenderPassEndingAccessType.NoAccess
                }
            };
        }

        CommandList.BeginRenderPass((uint)colorAttachments.Length, pRenderTargets, pDepthStencil, RenderPassFlags.None);
    }

    protected override void EndRenderPassImpl()
    {
        CommandList.EndRenderPass();
    }

    protected override void SetScissorsImpl(ReadOnlySpan<Scissor> scissors)
    {
        Box2D<int>* pRects = stackalloc Box2D<int>[scissors.Length];
        for (int i = 0; i < scissors.Length; i++)
        {
            pRects[i] = new()
            {
                Min = new()
                {
                    X = scissors[i].X,
                    Y = scissors[i].Y
                },
                Max = new()
                {
                    X = scissors[i].X + (int)scissors[i].Width,
                    Y = scissors[i].Y + (int)scissors[i].Height
                }
            };
        }

        CommandList.RSSetScissorRects((uint)scissors.Length, pRects);
    }

    protected override void SetViewportsImpl(ReadOnlySpan<Viewport> viewports)
    {
        DxViewport* pViewports = stackalloc DxViewport[viewports.Length];
        for (int i = 0; i < viewports.Length; i++)
        {
            pViewports[i] = new()
            {
                TopLeftX = viewports[i].X,
                TopLeftY = viewports[i].Y,
                Width = viewports[i].Width,
                Height = viewports[i].Height,
                MinDepth = viewports[i].MinDepth,
                MaxDepth = viewports[i].MaxDepth
            };
        }

        CommandList.RSSetViewports((uint)viewports.Length, pViewports);
    }

    protected override void SetPipelineImpl(GraphicsPipeline pipeline)
    {
        CommandList.SetPipelineState(pipeline.DirectX12().PipelineState);

        CommandList.IASetPrimitiveTopology(DXFormats.DirectX12(pipeline.Desc.PrimitiveTopology).Topology);
    }

    protected override void SetPipelineImpl(ComputePipeline pipeline)
    {
        CommandList.SetPipelineState(pipeline.DirectX12().PipelineState);
    }

    protected override void SetPipelineImpl(MeshShadingPipeline pipeline)
    {
        CommandList.SetPipelineState(pipeline.DirectX12().PipelineState);
    }

    protected override void SetStencilReferenceImpl(uint stencilReference)
    {
        CommandList.OMSetStencilRef(stencilReference);
    }

    protected override void SetBlendConstantImpl(Vector4 blendConstant)
    {
        CommandList.OMSetBlendFactor(&blendConstant.X);
    }

    protected override void SetVertexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, uint slot)
    {
        VertexBufferView view = new()
        {
            BufferLocation = buffer.DirectX12().GPUVirtualAddress + offsetInBytes,
            SizeInBytes = buffer.Desc.SizeInBytes - offsetInBytes,
            StrideInBytes = pipeline.Desc.InputLayouts[slot].StrideInBytes
        };

        CommandList.IASetVertexBuffers(slot, 1, &view);
    }

    protected override void SetIndexBufferImpl(GraphicsPipeline pipeline, Buffer buffer, uint offsetInBytes, IndexFormat indexFormat)
    {
        IndexBufferView view = new()
        {
            BufferLocation = buffer.DirectX12().GPUVirtualAddress + offsetInBytes,
            SizeInBytes = buffer.Desc.SizeInBytes - offsetInBytes,
            Format = DXFormats.DirectX12(indexFormat)
        };

        CommandList.IASetIndexBuffer(&view);
    }

    protected override void SetConstantBufferImpl(Pipeline pipeline, Buffer buffer, uint offsetInBytes)
    {
        if (pipeline is ComputePipeline)
        {
            CommandList.SetComputeRootSignature(Context.RootSignature);
            CommandList.SetComputeRootConstantBufferView(0, buffer.DirectX12().GPUVirtualAddress + offsetInBytes);
        }
        else
        {
            CommandList.SetGraphicsRootSignature(Context.RootSignature);
            CommandList.SetGraphicsRootConstantBufferView(0, buffer.DirectX12().GPUVirtualAddress + offsetInBytes);
        }
    }

    protected override void DrawImpl(GraphicsPipeline pipeline, uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        CommandList.DrawInstanced(vertexCount, instanceCount, firstVertex, firstInstance);
    }

    protected override void DrawIndirectImpl(GraphicsPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        CommandList.ExecuteIndirect(Context.DrawSignature, drawCount, indirectBuffer.DirectX12().Resource, offsetInBytes, default(ID3D12Resource*), 0);
    }

    protected override void DrawIndexedImpl(GraphicsPipeline pipeline, uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance)
    {
        CommandList.DrawIndexedInstanced(indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
    }

    protected override void DrawIndexedIndirectImpl(GraphicsPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        CommandList.ExecuteIndirect(Context.DrawIndexedSignature, drawCount, indirectBuffer.DirectX12().Resource, offsetInBytes, default(ID3D12Resource*), 0);
    }

    protected override void DispatchImpl(ComputePipeline pipeline, uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        CommandList.Dispatch(groupCountX, groupCountY, groupCountZ);
    }

    protected override void DispatchIndirectImpl(ComputePipeline pipeline, Buffer indirectBuffer, uint offsetInBytes)
    {
        CommandList.ExecuteIndirect(Context.DispatchSignature, 1, indirectBuffer.DirectX12().Resource, offsetInBytes, default(ID3D12Resource*), 0);
    }

    protected override void DispatchMeshImpl(MeshShadingPipeline pipeline, uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        CommandList.DispatchMesh(groupCountX, groupCountY, groupCountZ);
    }

    protected override void DispatchMeshIndirectImpl(MeshShadingPipeline pipeline, Buffer indirectBuffer, uint offsetInBytes, uint dispatchCount)
    {
        CommandList.ExecuteIndirect(Context.DispatchMeshSignature, dispatchCount, indirectBuffer.DirectX12().Resource, offsetInBytes, default(ID3D12Resource*), 0);
    }

    protected override void BeginQueryImpl(QueryHeap queryHeap, uint index)
    {
        CommandList.BeginQuery(queryHeap.DirectX12().QueryHeap, DXFormats.DirectX12(queryHeap.Desc.Type).Type, index);
    }

    protected override void EndQueryImpl(QueryHeap queryHeap, uint index)
    {
        DXQueryHeap dxQueryHeap = queryHeap.DirectX12();

        CommandList.EndQuery(dxQueryHeap.QueryHeap, DXFormats.DirectX12(dxQueryHeap.Desc.Type).Type, index);
        CommandList.ResolveQueryData(dxQueryHeap.QueryHeap, DXFormats.DirectX12(dxQueryHeap.Desc.Type).Type, index, 1, dxQueryHeap.Buffer.Resource, sizeof(ulong) * index);
    }

    protected override void WriteTimestampImpl(QueryHeap queryHeap, uint index)
    {
        DXQueryHeap dxQueryHeap = queryHeap.DirectX12();

        CommandList.EndQuery(dxQueryHeap.QueryHeap, DxQueryType.Timestamp, index);
        CommandList.ResolveQueryData(dxQueryHeap.QueryHeap, DxQueryType.Timestamp, index, 1, dxQueryHeap.Buffer.Resource, sizeof(ulong) * index);
    }

    protected override void BeginDebugEventImpl(string label)
    {
        using ZenithMarshal.Scope scope = new();

        uint size = PixHelpers.CalculateEventSize(label);

        ulong* buffer = (ulong*)ZenithMarshal.Allocate<byte>(scope, size);

        PixHelpers.FormatEventToBuffer(buffer, PixHelpers.Event, 0, label);

        CommandList.BeginEvent(PixHelpers.Version, buffer, size);
    }

    protected override void EndDebugEventImpl()
    {
        CommandList.EndEvent();
    }

    protected override void InsertDebugMarkerImpl(string label)
    {
        using ZenithMarshal.Scope scope = new();

        uint size = PixHelpers.CalculateEventSize(label);

        ulong* buffer = (ulong*)ZenithMarshal.Allocate<byte>(scope, size);

        PixHelpers.FormatEventToBuffer(buffer, PixHelpers.Marker, 0, label);

        CommandList.SetMarker(PixHelpers.Version, buffer, size);
    }

    protected override void BeginImpl()
    {
        if (Queue.Type is CommandQueueType.Copy)
        {
            return;
        }

        ID3D12DescriptorHeap** ppDescriptorHeaps = stackalloc ID3D12DescriptorHeap*[] { Context.CbvSrvUavHeap.Heap, Context.SamplerHeap.Heap };

        CommandList.SetDescriptorHeaps(2, ppDescriptorHeaps);
    }

    protected override void EndImpl()
    {
        CommandList.Close().Success();
    }

    protected override void ResetImpl()
    {
        CommandAllocator.Reset().Success();
        CommandList.Reset(CommandAllocator, default(ID3D12PipelineState*)).Success();
    }

    protected override void SetResourceName(string name)
    {
        CommandList.SetName(name).Success();
    }

    protected override void Destroy()
    {
        base.Destroy();

        CommandList.Dispose();
        CommandAllocator.Dispose();
    }
}
