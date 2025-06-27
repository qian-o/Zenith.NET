using System.Runtime.CompilerServices;

namespace Zenith.NET;

public abstract class CommandBuffer(GraphicsContext context, CommandQueue queue) : GraphicsResource(context)
{
    protected CommandQueue Queue { get; } = queue;

    #region State Management
    public abstract void Begin();

    public abstract void End();

    public void Submit()
    {
        Queue.Submit(this);
    }

    public void Reset()
    {
        Context.Uploader.Release(this);

        ResetImpl();
    }

    protected abstract void ResetImpl();
    #endregion

    #region Buffer Operations
    public void UpdateBuffer<T>(ReadOnlySpan<T> data,
                                IBuffer buffer,
                                uint offsetInBytes)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Buffer upload validation is not implemented yet.");
        }

        uint sizeInBytes = (uint)(data.Length * Unsafe.SizeOf<T>());

        Buffer temporary = Context.Uploader.Buffer(this, sizeInBytes);
        temporary.Upload(data, 0);

        CopyBuffer(temporary, 0, buffer, offsetInBytes, sizeInBytes);
    }

    public abstract void CopyBuffer(IBuffer source,
                                    uint sourceOffsetInBytes,
                                    IBuffer destination,
                                    uint destinationOffsetInBytes,
                                    uint sizeInBytes);
    #endregion

    #region Texture Operations
    public void UpdateTexture<T>(ReadOnlySpan<T> data,
                                 ITexture texture,
                                 TextureSlice slice,
                                 TextureOffset offset,
                                 TextureExtent extent)
    {
        if (Context.UseDebugLayer)
        {
            throw new NotImplementedException("Texture upload validation is not implemented yet.");
        }

        uint sizeInBytes = (uint)(data.Length * Unsafe.SizeOf<T>());

        Buffer temporary = Context.Uploader.Buffer(this, sizeInBytes);
        temporary.Upload(data, 0);

        CopyTexture(temporary, 0, sizeInBytes, texture, slice, offset, extent);
    }

    public abstract void CopyTexture(IBuffer buffer,
                                     uint offsetInBytes,
                                     uint sizeInBytes,
                                     ITexture texture,
                                     TextureSlice slice,
                                     TextureOffset offset,
                                     TextureExtent extent);

    public abstract void CopyTexture(ITexture source,
                                     TextureOffset sourceOffset,
                                     ITexture destination,
                                     TextureOffset destinationOffset,
                                     TextureExtent extent);

    public abstract void ResolveTexture(ITexture source,
                                        TextureOffset sourceOffset,
                                        ITexture destination,
                                        TextureOffset destinationOffset);
    #endregion

    #region Acceleration Structure Operations
    public abstract BottomLevelAccelerationStructure BuildAccelerationStructure(BottomLevelAccelerationStructureDesc desc);

    public abstract TopLevelAccelerationStructure BuildAccelerationStructure(TopLevelAccelerationStructureDesc desc);

    public abstract void UpdateAccelerationStructure(TopLevelAccelerationStructure accelerationStructure,
                                                     TopLevelAccelerationStructureDesc newDesc);
    #endregion

    #region Rendering Operations
    public abstract void BeginRendering(FrameBuffer frameBuffer, ClearValue clearValue);

    public abstract void EndRendering();

    public abstract void SetScissors(Scissor[] scissors);

    public abstract void SetViewports(Viewport[] viewports);
    #endregion

    #region Pipeline Operations
    public abstract void SetGraphicsPipeline(GraphicsPipeline pipeline);

    public abstract void SetComputePipeline(ComputePipeline pipeline);

    public abstract void SetRayTracingPipeline(RayTracingPipeline pipeline);
    #endregion

    #region Resource Binding Operations
    public abstract void PrepareResources(ResourceSet[] resourceSets);

    public abstract void SetVertexBuffer(uint slot, Buffer buffer, uint offset = 0);

    public abstract void SetVertexBuffers(Buffer[] buffers, uint[] offsets);

    public abstract void SetIndexBuffer(Buffer buffer, IndexFormat format = IndexFormat.UInt16, uint offset = 0);

    public abstract void SetResourceSet(uint slot, ResourceSet resourceSet);
    #endregion

    #region Drawing Operations
    public abstract void Draw(uint vertexCount, uint instanceCount, uint firstVertex = 0, uint firstInstance = 0);

    public abstract void DrawIndirect(Buffer argBuffer, uint offset, uint drawCount);

    public abstract void DrawIndexed(uint indexCount,
                                     uint instanceCount,
                                     uint firstIndex = 0,
                                     int vertexOffset = 0,
                                     uint firstInstance = 0);

    public abstract void DrawIndexedIndirect(Buffer argBuffer, uint offset, uint drawCount);
    #endregion

    #region Compute Operations
    public abstract void Dispatch(uint groupCountX, uint groupCountY, uint groupCountZ);

    public abstract void DispatchIndirect(Buffer argBuffer, uint offset);
    #endregion

    #region Ray Tracing Operations
    public abstract void DispatchRays(uint width, uint height, uint depth);
    #endregion

    #region Debugging
    public abstract void BeginDebugEvent(string label);

    public abstract void EndDebugEvent();

    public abstract void InsertDebugMarker(string label);
    #endregion

    protected override void Destroy()
    {
        Context.Uploader.Release(this);
    }
}
