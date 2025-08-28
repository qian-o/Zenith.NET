using System.Runtime.CompilerServices;

namespace Zenith.NET;

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    private bool isRendering;

    public FrameBuffer? CurrentFrameBuffer { get; private set; }

    public Pipeline? CurrentPipeline { get; private set; }

    public abstract void Begin();

    public abstract void End();

    public void Submit()
    {
        queue.Submit(this);
    }

    public void Upload<T>(Buffer buffer, uint offsetInBytes, ReadOnlySpan<T> data)
    {
        uint sizeInBytes = (uint)(data.Length * Unsafe.SizeOf<T>());

        Buffer temporary = Context.Uploader.Buffer(this, sizeInBytes);
        temporary.Upload(data, 0);

        CopyBuffer(temporary, 0, buffer, offsetInBytes, sizeInBytes);
    }

    public void Upload<T>(Texture texture, TextureSlice slice, TextureOffset offset, TextureExtent extent, ReadOnlySpan<T> data)
    {
        uint sizeInBytes = (uint)(data.Length * Unsafe.SizeOf<T>());

        Buffer temporary = Context.Uploader.Buffer(this, sizeInBytes);
        temporary.Upload(data, 0);

        CopyBufferToTexture(temporary, 0, texture, slice, offset, extent);
    }

    public abstract void CopyBuffer(Buffer src, uint srcOffsetInBytes, Buffer dest, uint destOffsetInBytes, uint sizeInBytes);

    public abstract void CopyTexture(Texture src, TextureSlice srcSlice, TextureOffset srcOffset, Texture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent extent);

    public abstract void CopyBufferToTexture(Buffer src, uint srcOffsetInBytes, Texture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent destExtent);

    public abstract void CopyTextureToBuffer(Texture src, TextureSlice srcSlice, TextureOffset srcOffset, TextureExtent srcExtent, Buffer dest, uint destOffsetInBytes);

    public abstract void ResolveTexture(Texture src, TextureSlice srcSlice, Texture dest, TextureSlice destSlice);

    public abstract BottomLevelAccelerationStructure BuildAccelerationStructure(BottomLevelAccelerationStructureDesc desc);

    public abstract TopLevelAccelerationStructure BuildAccelerationStructure(TopLevelAccelerationStructureDesc desc);

    public abstract void UpdateAccelerationStructure(TopLevelAccelerationStructure accelerationStructure, TopLevelAccelerationStructureDesc newDesc);

    public abstract void SetScissors(Scissor[] scissors);

    public abstract void SetViewports(Viewport[] viewports);

    public void BindFrameBuffer(FrameBuffer frameBuffer, ClearValue clearValue)
    {
        EnsureRenderingEnded();

        BindFrameBufferImpl(frameBuffer, clearValue);

        CurrentFrameBuffer = frameBuffer;
    }

    public void BindPipeline(GraphicsPipeline pipeline)
    {
        BindPipelineImpl(pipeline);

        CurrentPipeline = pipeline;
    }

    public void BindPipeline(ComputePipeline pipeline)
    {
        BindPipelineImpl(pipeline);

        CurrentPipeline = pipeline;
    }

    public void BindPipeline(RayTracingPipeline pipeline)
    {
        BindPipelineImpl(pipeline);

        CurrentPipeline = pipeline;
    }

    public void BindResourceSets(ResourceSet[] sets)
    {
        if (CurrentPipeline is null)
        {
            return;
        }

        EnsureRenderingEnded();

        BindResourceSetsImpl(sets);
    }

    public void BindVertexBuffers(Buffer[] buffers, uint[] offsetsInBytes)
    {
        if (CurrentPipeline is not GraphicsPipeline)
        {
            return;
        }

        BindVertexBuffersImpl(buffers, offsetsInBytes);
    }

    public void BindIndexBuffer(Buffer buffer, uint offsetInBytes, IndexFormat format)
    {
        if (CurrentPipeline is not GraphicsPipeline)
        {
            return;
        }

        BindIndexBufferImpl(buffer, offsetInBytes, format);
    }

    public void Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
    {
        if (CurrentPipeline is not GraphicsPipeline)
        {
            return;
        }

        EnsureRenderingBegan();

        DrawImpl(vertexCount, instanceCount, firstVertex, firstInstance);
    }

    public void DrawIndirect(Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        if (CurrentPipeline is not GraphicsPipeline)
        {
            return;
        }

        EnsureRenderingBegan();

        DrawIndirectImpl(indirectBuffer, offsetInBytes, drawCount);
    }

    public void DrawIndexed(uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance)
    {
        if (CurrentPipeline is not GraphicsPipeline)
        {
            return;
        }

        EnsureRenderingBegan();

        DrawIndexedImpl(indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
    }

    public void DrawIndexedIndirect(Buffer indirectBuffer, uint offsetInBytes, uint drawCount)
    {
        if (CurrentPipeline is not GraphicsPipeline)
        {
            return;
        }

        EnsureRenderingBegan();

        DrawIndexedIndirectImpl(indirectBuffer, offsetInBytes, drawCount);
    }

    public void Dispatch(uint groupCountX, uint groupCountY, uint groupCountZ)
    {
        if (CurrentPipeline is not ComputePipeline)
        {
            return;
        }

        EnsureRenderingEnded();

        DispatchImpl(groupCountX, groupCountY, groupCountZ);
    }

    public void DispatchIndirect(Buffer indirectBuffer, uint offsetInBytes)
    {
        if (CurrentPipeline is not ComputePipeline)
        {
            return;
        }

        EnsureRenderingEnded();

        DispatchIndirectImpl(indirectBuffer, offsetInBytes);
    }

    public void DispatchRays(uint width, uint height, uint depth)
    {
        if (CurrentPipeline is not RayTracingPipeline)
        {
            return;
        }

        EnsureRenderingEnded();

        DispatchRaysImpl(width, height, depth);
    }

    public abstract void BeginDebugEvent(string label);

    public abstract void EndDebugEvent();

    public abstract void InsertDebugMarker(string label);

    internal void Reset()
    {
        EnsureRenderingEnded();

        ResetImpl();

        Context.Uploader.Release(this);

        CurrentFrameBuffer = null;
        CurrentPipeline = null;
    }

    protected override void Destroy()
    {
        Context.Uploader.Release(this);

        CurrentFrameBuffer = null;
        CurrentPipeline = null;
    }

    protected abstract void BindFrameBufferImpl(FrameBuffer frameBuffer, ClearValue clearValue);

    protected abstract void BindPipelineImpl(GraphicsPipeline pipeline);

    protected abstract void BindPipelineImpl(ComputePipeline pipeline);

    protected abstract void BindPipelineImpl(RayTracingPipeline pipeline);

    protected abstract void BindResourceSetsImpl(ResourceSet[] sets);

    protected abstract void BindVertexBuffersImpl(Buffer[] buffers, uint[] offsetsInBytes);

    protected abstract void BindIndexBufferImpl(Buffer buffer, uint offsetInBytes, IndexFormat format);

    protected abstract void DrawImpl(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance);

    protected abstract void DrawIndirectImpl(Buffer indirectBuffer, uint offsetInBytes, uint drawCount);

    protected abstract void DrawIndexedImpl(uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance);

    protected abstract void DrawIndexedIndirectImpl(Buffer indirectBuffer, uint offsetInBytes, uint drawCount);

    protected abstract void DispatchImpl(uint groupCountX, uint groupCountY, uint groupCountZ);

    protected abstract void DispatchIndirectImpl(Buffer indirectBuffer, uint offsetInBytes);

    protected abstract void DispatchRaysImpl(uint width, uint height, uint depth);

    protected abstract void BeginRenderingImpl();

    protected abstract void EndRenderingImpl();

    protected abstract void ResetImpl();

    private void EnsureRenderingBegan()
    {
        if (!isRendering && CurrentFrameBuffer is not null)
        {
            BeginRenderingImpl();

            isRendering = true;
        }
    }

    private void EnsureRenderingEnded()
    {
        if (isRendering)
        {
            EndRenderingImpl();

            isRendering = false;
        }
    }
}
