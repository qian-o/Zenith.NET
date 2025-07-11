using System.Runtime.CompilerServices;

namespace Zenith.NET;

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    public CommandQueue Queue { get; } = queue;

    public FrameBuffer? CurrentFrameBuffer { get; private set; }

    public Pipeline? CurrentPipeline { get; private set; }

    public abstract void Begin();

    public abstract void End();

    public abstract void Submit();

    public void UploadBuffer<T>(Buffer buffer, uint offsetInBytes, ReadOnlySpan<T> data)
    {
        uint sizeInBytes = (uint)(data.Length * Unsafe.SizeOf<T>());

        Buffer temporary = Context.Uploader.Buffer(this, sizeInBytes);
        temporary.Upload(data, 0);

        CopyBuffer(temporary, 0, buffer, offsetInBytes, sizeInBytes);
    }

    public abstract void CopyBuffer(Buffer src, uint srcOffsetInBytes, Buffer dest, uint destOffsetInBytes, uint sizeInBytes);

    public void UploadTexture<T>(Texture texture, TextureSlice slice, TextureOffset offset, TextureExtent extent, ReadOnlySpan<T> data)
    {
        uint sizeInBytes = (uint)(data.Length * Unsafe.SizeOf<T>());

        Buffer temporary = Context.Uploader.Buffer(this, sizeInBytes);
        temporary.Upload(data, 0);

        CopyTexture(temporary, 0, sizeInBytes, texture, slice, offset, extent);
    }

    public abstract void CopyTexture(Buffer src, uint srcOffsetInBytes, uint srcSizeInBytes, Texture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent destExtent);

    public abstract void CopyTexture(Texture src, TextureSlice srcSlice, TextureOffset srcOffset, Texture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent extent);

    public abstract void ResolveTexture(Texture src, TextureSlice srcSlice, Texture dest, TextureSlice destSlice);

    public abstract BottomLevelAccelerationStructure BuildBottomLevelAccelerationStructure(BottomLevelAccelerationStructureDesc desc);

    public abstract TopLevelAccelerationStructure BuildTopLevelAccelerationStructure(TopLevelAccelerationStructureDesc desc);

    public abstract void UpdateTopLevelAccelerationStructure(TopLevelAccelerationStructure accelerationStructure, TopLevelAccelerationStructureDesc newDesc);

    public void BeginRendering(FrameBuffer frameBuffer, ClearValue clearValue)
    {
        BeginRenderingImpl(frameBuffer, clearValue);

        CurrentFrameBuffer = frameBuffer;
    }

    public void EndRendering()
    {
        EndRenderingImpl();

        CurrentFrameBuffer = null;
    }

    public abstract void SetScissors(Scissor[] scissors);

    public abstract void SetViewports(Viewport[] viewports);

    public void SetGraphicsPipeline(GraphicsPipeline pipeline)
    {
        SetGraphicsPipelineImpl(pipeline);

        CurrentPipeline = pipeline;
    }

    public void SetComputePipeline(ComputePipeline pipeline)
    {
        SetComputePipelineImpl(pipeline);

        CurrentPipeline = pipeline;
    }

    public void SetRayTracingPipeline(RayTracingPipeline pipeline)
    {
        SetRayTracingPipelineImpl(pipeline);

        CurrentPipeline = pipeline;
    }

    public abstract void SetIndexBuffer(Buffer buffer, uint offsetInBytes, IndexFormat format);

    public abstract void SetVertexBuffers(Buffer[] buffers, uint[] offsetsInBytes);

    public abstract void PrepareResourceSets(ResourceSet[] sets);

    public abstract void BindResourceSets(ResourceSet[] sets);

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
        Context.Uploader.Release(this);

        ResetImpl();
    }

    protected override void Destroy()
    {
        Context.Uploader.Release(this);
    }

    protected abstract void BeginRenderingImpl(FrameBuffer frameBuffer, ClearValue clearValue);

    protected abstract void EndRenderingImpl();

    protected abstract void SetGraphicsPipelineImpl(GraphicsPipeline pipeline);

    protected abstract void SetComputePipelineImpl(ComputePipeline pipeline);

    protected abstract void SetRayTracingPipelineImpl(RayTracingPipeline pipeline);

    protected abstract void ResetImpl();
}
