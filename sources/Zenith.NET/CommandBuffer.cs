using System.Runtime.CompilerServices;

namespace Zenith.NET;

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
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

    public abstract void BindResourceSets(ResourceSet[] sets);

    public abstract void BindVertexBuffers(Buffer[] buffers, uint[] offsetsInBytes);

    public abstract void BindIndexBuffer(Buffer buffer, uint offsetInBytes, IndexFormat format);

    public abstract void Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance);

    public abstract void DrawIndirect(Buffer indirectBuffer, uint offsetInBytes, uint drawCount);

    public abstract void DrawIndexed(uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance);

    public abstract void DrawIndexedIndirect(Buffer indirectBuffer, uint offsetInBytes, uint drawCount);

    public abstract void Dispatch(uint groupCountX, uint groupCountY, uint groupCountZ);

    public abstract void DispatchIndirect(Buffer indirectBuffer, uint offsetInBytes);

    public abstract void DispatchRays(uint width, uint height, uint depth);

    public abstract void BeginDebugEvent(string label);

    public abstract void EndDebugEvent();

    public abstract void InsertDebugMarker(string label);

    internal void Reset()
    {
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

    protected abstract void ResetImpl();
}
