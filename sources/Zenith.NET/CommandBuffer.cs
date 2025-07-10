using System.Runtime.CompilerServices;

namespace Zenith.NET;

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    public CommandQueue Queue { get; } = queue;

    public FrameBuffer? CurrentFrameBuffer { get; }

    public Pipeline? CurrentPipeline { get; }

    public abstract void Begin();

    public abstract void End();

    public abstract void Submit();

    public void UploadBuffer<T>(IBuffer buffer, uint offsetInBytes, ReadOnlySpan<T> data)
    {
        uint sizeInBytes = (uint)(data.Length * Unsafe.SizeOf<T>());

        Buffer temporary = Context.Uploader.Buffer(this, sizeInBytes);
        temporary.Upload(0, data);

        CopyBuffer(temporary, 0, buffer, offsetInBytes, sizeInBytes);
    }

    public abstract void CopyBuffer(IBuffer src, uint srcOffsetInBytes, IBuffer dest, uint destOffsetInBytes, uint sizeInBytes);

    public void UploadTexture<T>(ITexture texture, TextureSlice slice, TextureOffset offset, TextureExtent extent, ReadOnlySpan<T> data)
    {
        uint sizeInBytes = (uint)(data.Length * Unsafe.SizeOf<T>());

        Buffer temporary = Context.Uploader.Buffer(this, sizeInBytes);
        temporary.Upload(0, data);

        CopyTexture(temporary, 0, sizeInBytes, texture, slice, offset, extent);
    }

    public abstract void CopyTexture(IBuffer src, uint srcOffsetInBytes, uint srcSizeInBytes, ITexture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent destExtent);

    public abstract void CopyTexture(ITexture src, TextureSlice srcSlice, TextureOffset srcOffset, ITexture dest, TextureSlice destSlice, TextureOffset destOffset, TextureExtent extent);

    public abstract void ResolveTexture(ITexture src, TextureSlice srcSlice, ITexture dest, TextureSlice destSlice);

    public abstract BottomLevelAccelerationStructure BuildBottomLevelAccelerationStructure(BottomLevelAccelerationStructureDesc desc);

    public abstract TopLevelAccelerationStructure BuildTopLevelAccelerationStructure(TopLevelAccelerationStructureDesc desc);

    public abstract void UpdateTopLevelAccelerationStructure(TopLevelAccelerationStructure accelerationStructure, TopLevelAccelerationStructureDesc newDesc);

    public abstract void BeginRendering(FrameBuffer frameBuffer, ClearValue clearValue);

    public abstract void EndRendering();

    public abstract void SetScissors(Scissor[] scissors);

    public abstract void SetViewports(Viewport[] viewports);

    public abstract void SetGraphicsPipeline(GraphicsPipeline pipeline);

    public abstract void SetComputePipeline(ComputePipeline pipeline);

    public abstract void SetRayTracingPipeline(RayTracingPipeline pipeline);

    public abstract void SetIndexBuffer(IBuffer buffer, uint offsetInBytes, IndexFormat format);

    public abstract void SetVertexBuffers(IBuffer[] buffers, uint[] offsetsInBytes);

    public abstract void PrepareResourceSets(ResourceSet[] sets);

    public abstract void BindResourceSets(ResourceSet[] sets);

    public abstract void Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance);

    public abstract void DrawIndirect(IBuffer indirectBuffer, uint offsetInBytes, uint drawCount);

    public abstract void DrawIndexed(uint indexCount, uint instanceCount, uint firstIndex, int vertexOffset, uint firstInstance);

    public abstract void DrawIndexedIndirect(IBuffer indirectBuffer, uint offsetInBytes, uint drawCount);

    public abstract void Dispatch(uint groupCountX, uint groupCountY, uint groupCountZ);

    public abstract void DispatchIndirect(IBuffer indirectBuffer, uint offsetInBytes);

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

    protected abstract void ResetImpl();
}
