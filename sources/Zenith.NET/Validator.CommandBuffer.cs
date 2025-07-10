using System.Numerics;
using System.Runtime.CompilerServices;

namespace Zenith.NET;

internal partial class Validator
{
    public void Begin(CommandBuffer commandBuffer)
    {
        if (commandBuffer.State is not CommandBufferState.Idle)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Command buffer can only be started when in Idle state. Current state: {commandBuffer.State}.");
        }
    }

    public void End(CommandBuffer commandBuffer)
    {
        if (commandBuffer.State is not CommandBufferState.Recording)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Command buffer can only be ended when in Recording state. Current state: {commandBuffer.State}.");
        }
    }

    public void Submit(CommandBuffer commandBuffer)
    {
        if (commandBuffer.State is not CommandBufferState.Completed)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Command buffer can only be submitted when in Completed state. Current state: {commandBuffer.State}.");
        }
    }

    public void UploadBuffer<T>(CommandBuffer commandBuffer,
                                        IBuffer buffer,
                                        uint offsetInBytes,
                                        ReadOnlySpan<T> data)
    {
        RecordingState(commandBuffer, nameof(CommandBuffer.UploadBuffer));

        if (buffer?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Buffer for upload must be a valid, non-disposed buffer.");

            return;
        }

        if (data.IsEmpty)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Upload data cannot be empty.");

            return;
        }

        ObtainBufferValues(buffer,
                           out uint sizeInBytes,
                           out _,
                           out _,
                           "buffer for upload");

        uint requestedSize = offsetInBytes + (uint)(data.Length * Unsafe.SizeOf<T>());

        if (requestedSize > sizeInBytes)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Upload size ({requestedSize} bytes) exceeds buffer size ({sizeInBytes} bytes).");
        }
    }

    public void CopyBuffer(CommandBuffer commandBuffer,
                                   IBuffer src,
                                   uint srcOffsetInBytes,
                                   IBuffer dest,
                                   uint destOffsetInBytes,
                                   uint sizeInBytes)
    {
        RecordingState(commandBuffer, nameof(CommandBuffer.CopyBuffer));

        if (src?.IsDisposed is not false || dest?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Source and destination buffers for copy must be valid, non-disposed buffers.");

            return;
        }

        if (sizeInBytes is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Copy size must be greater than 0.");

            return;
        }

        ObtainBufferValues(src,
                           out uint srcSizeInBytes,
                           out _,
                           out _,
                           "source buffer for copy");

        ObtainBufferValues(dest,
                           out uint destSizeInBytes,
                           out _,
                           out _,
                           "destination buffer for copy");

        if (srcOffsetInBytes + sizeInBytes > srcSizeInBytes)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Source buffer copy range exceeds source buffer size. Source size: {srcSizeInBytes} bytes, requested range: {srcOffsetInBytes} + {sizeInBytes} bytes.");
        }

        if (destOffsetInBytes + sizeInBytes > destSizeInBytes)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Destination buffer copy range exceeds destination buffer size. Destination size: {destSizeInBytes} bytes, requested range: {destOffsetInBytes} + {sizeInBytes} bytes.");
        }
    }

    public void UploadTexture<T>(CommandBuffer commandBuffer,
                                         ITexture texture,
                                         TextureSlice slice,
                                         TextureOffset offset,
                                         TextureExtent extent,
                                         ReadOnlySpan<T> data)
    {
        RecordingState(commandBuffer, nameof(CommandBuffer.UploadTexture));

        if (texture?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Texture for upload must be a valid, non-disposed texture.");

            return;
        }

        if (extent.Width is 0 || extent.Height is 0 || extent.Depth is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Texture extent must have non-zero width, height, and depth.");

            return;
        }

        if (data.IsEmpty)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Upload data cannot be empty.");

            return;
        }

        ObtainTextureValues(texture,
                            out TextureType type,
                            out _,
                            out uint width,
                            out uint height,
                            out uint depth,
                            out uint layers,
                            out uint mipLevels,
                            out _,
                            out _,
                            "texture for upload");

        TextureSlice(type, layers, mipLevels, slice, "texture slice for upload");

        TextureRange(width, height, depth, offset, extent, "texture offset and extent for upload");
    }

    public void CopyTexture(CommandBuffer commandBuffer,
                                    IBuffer src,
                                    uint srcOffsetInBytes,
                                    uint srcSizeInBytes,
                                    ITexture dest,
                                    TextureSlice destSlice,
                                    TextureOffset destOffset,
                                    TextureExtent destExtent)
    {
        RecordingState(commandBuffer, nameof(CommandBuffer.CopyTexture));

        if (src?.IsDisposed is not false || dest?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Source buffer and destination texture for copy must be valid, non-disposed resources.");

            return;
        }

        if (srcSizeInBytes is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Source size must be greater than 0.");

            return;
        }

        if (destExtent.Width is 0 || destExtent.Height is 0 || destExtent.Depth is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Destination texture extent must have non-zero width, height, and depth.");

            return;
        }

        ObtainBufferValues(src,
                           out uint srcBufferSizeInBytes,
                           out _,
                           out _,
                           "source buffer for copy");

        ObtainTextureValues(dest,
                            out TextureType type,
                            out _,
                            out uint width,
                            out uint height,
                            out uint depth,
                            out uint layers,
                            out uint mipLevels,
                            out _,
                            out _,
                            "destination texture for copy");

        if (srcOffsetInBytes + srcSizeInBytes > srcBufferSizeInBytes)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Source buffer copy range exceeds source buffer size. Source size: {srcBufferSizeInBytes} bytes, requested range: {srcOffsetInBytes} + {srcSizeInBytes} bytes.");
        }

        TextureSlice(type, layers, mipLevels, destSlice, "destination texture slice for copy");

        TextureRange(width, height, depth, destOffset, destExtent, "destination texture offset and extent for copy");
    }

    public void CopyTexture(CommandBuffer commandBuffer,
                                    ITexture src,
                                    TextureSlice srcSlice,
                                    TextureOffset srcOffset,
                                    ITexture dest,
                                    TextureSlice destSlice,
                                    TextureOffset destOffset,
                                    TextureExtent extent)
    {
        RecordingState(commandBuffer, nameof(CommandBuffer.CopyTexture));

        if (src?.IsDisposed is not false || dest?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Source and destination textures for copy must be valid, non-disposed textures.");

            return;
        }

        if (extent.Width is 0 || extent.Height is 0 || extent.Depth is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Texture extent must have non-zero width, height, and depth.");

            return;
        }

        ObtainTextureValues(src,
                            out TextureType srcType,
                            out _,
                            out uint srcWidth,
                            out uint srcHeight,
                            out uint srcDepth,
                            out uint srcLayers,
                            out uint srcMipLevels,
                            out _,
                            out _,
                            "source texture for copy");

        ObtainTextureValues(dest,
                            out TextureType destType,
                            out _,
                            out uint destWidth,
                            out uint destHeight,
                            out uint destDepth,
                            out uint destLayers,
                            out uint destMipLevels,
                            out _,
                            out _,
                            "destination texture for copy");

        TextureSlice(srcType, srcLayers, srcMipLevels, srcSlice, "source texture slice for copy");

        TextureRange(srcWidth, srcHeight, srcDepth, srcOffset, extent, "source texture offset and extent for copy");

        TextureSlice(destType, destLayers, destMipLevels, destSlice, "destination texture slice for copy");

        TextureRange(destWidth, destHeight, destDepth, destOffset, extent, "destination texture offset and extent for copy");
    }

    public void ResolveTexture(CommandBuffer commandBuffer,
                                       ITexture src,
                                       TextureSlice srcSlice,
                                       ITexture dest,
                                       TextureSlice destSlice)
    {
        DirectQueue(commandBuffer, nameof(CommandBuffer.ResolveTexture));

        RecordingState(commandBuffer, nameof(CommandBuffer.ResolveTexture));

        if (src?.IsDisposed is not false || dest?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Source and destination textures for resolve must be valid, non-disposed textures.");

            return;
        }

        ObtainTextureValues(src,
                            out TextureType srcType,
                            out _,
                            out _,
                            out _,
                            out _,
                            out uint srcLayers,
                            out uint srcMipLevels,
                            out SampleCount srcSampleCount,
                            out _,
                            "source texture for resolve");

        ObtainTextureValues(dest,
                            out TextureType destType,
                            out _,
                            out _,
                            out _,
                            out _,
                            out uint destLayers,
                            out uint destMipLevels,
                            out SampleCount destSampleCount,
                            out _,
                            "destination texture for resolve");

        TextureSlice(srcType, srcLayers, srcMipLevels, srcSlice, "source texture slice for resolve");

        TextureSlice(destType, destLayers, destMipLevels, destSlice, "destination texture slice for resolve");

        if (srcSampleCount is SampleCount.Count1)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Source texture for resolve must have a sample count greater than 1.");
        }

        if (destSampleCount is not SampleCount.Count1)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Destination texture for resolve must have a sample count of 1.");
        }
    }

    public void BuildBottomLevelAccelerationStructure(CommandBuffer commandBuffer,
                                                              BottomLevelAccelerationStructureDesc desc)
    {
        NotCopyQueue(commandBuffer, nameof(CommandBuffer.BuildBottomLevelAccelerationStructure));

        RecordingState(commandBuffer, nameof(CommandBuffer.BuildBottomLevelAccelerationStructure));

        if (desc.Geometries is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Bottom-level acceleration structure geometries cannot be null.");

            return;
        }

        if (desc.Geometries.Length is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Bottom-level acceleration structure must have at least 1 geometry.");
        }

        for (int i = 0; i < desc.Geometries.Length; i++)
        {
            RayTracingGeometry(desc.Geometries[i], $"geometry at index {i}");
        }
    }

    public void BuildTopLevelAccelerationStructure(CommandBuffer commandBuffer,
                                                           TopLevelAccelerationStructureDesc desc)
    {
        NotCopyQueue(commandBuffer, nameof(CommandBuffer.BuildTopLevelAccelerationStructure));

        RecordingState(commandBuffer, nameof(CommandBuffer.BuildTopLevelAccelerationStructure));

        if (desc.Instances is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Top-level acceleration structure instances cannot be null.");

            return;
        }

        if (desc.Instances.Length is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Top-level acceleration structure must have at least 1 instance.");
        }

        for (int i = 0; i < desc.Instances.Length; i++)
        {
            RayTracingInstance(desc.Instances[i], $"instance at index {i}");
        }
    }

    public void UpdateTopLevelAccelerationStructure(CommandBuffer commandBuffer,
                                                            TopLevelAccelerationStructure accelerationStructure,
                                                            TopLevelAccelerationStructureDesc newDesc)
    {
        DirectQueue(commandBuffer, nameof(CommandBuffer.UpdateTopLevelAccelerationStructure));

        RecordingState(commandBuffer, nameof(CommandBuffer.UpdateTopLevelAccelerationStructure));

        if (newDesc.Instances is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "New top-level acceleration structure instances cannot be null.");

            return;
        }

        if (newDesc.Instances.Length != accelerationStructure.Desc.Instances.Length)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"New top-level acceleration structure must have the same number of instances as the existing one. Existing: {accelerationStructure.Desc.Instances.Length}, New: {newDesc.Instances.Length}.");

            return;
        }

        for (int i = 0; i < newDesc.Instances.Length; i++)
        {
            RayTracingInstance(newDesc.Instances[i], $"instance at index {i}");
        }
    }

    public void BeginRendering(CommandBuffer commandBuffer, FrameBuffer frameBuffer, ClearValue clearValue)
    {
        DirectQueue(commandBuffer, nameof(CommandBuffer.BeginRendering));

        RecordingState(commandBuffer, nameof(CommandBuffer.BeginRendering));

        if (frameBuffer?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Frame buffer for rendering must be a valid, non-disposed frame buffer.");

            return;
        }

        if (clearValue.ColorValues is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Clear value color values cannot be null.");
        }
    }

    public void EndRendering(CommandBuffer commandBuffer)
    {
        DirectQueue(commandBuffer, nameof(CommandBuffer.EndRendering));

        RecordingState(commandBuffer, nameof(CommandBuffer.EndRendering));
    }

    public void SetScissors(CommandBuffer commandBuffer, Scissor[] scissors)
    {
        DirectQueue(commandBuffer, nameof(CommandBuffer.SetScissors));

        RecordingState(commandBuffer, nameof(CommandBuffer.SetScissors));

        if (scissors is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Scissors cannot be null.");
        }
    }

    public void SetViewports(CommandBuffer commandBuffer, Viewport[] viewports)
    {
        DirectQueue(commandBuffer, nameof(CommandBuffer.SetViewports));

        RecordingState(commandBuffer, nameof(CommandBuffer.SetViewports));

        if (viewports is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Viewports cannot be null.");
        }
    }

    public void SetGraphicsPipeline(CommandBuffer commandBuffer, GraphicsPipeline pipeline)
    {
        DirectQueue(commandBuffer, nameof(CommandBuffer.SetGraphicsPipeline));

        RecordingState(commandBuffer, nameof(CommandBuffer.SetGraphicsPipeline));

        if (pipeline?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Graphics pipeline must be a valid, non-disposed graphics pipeline.");
        }
    }

    public void SetComputePipeline(CommandBuffer commandBuffer, ComputePipeline pipeline)
    {
        NotCopyQueue(commandBuffer, nameof(CommandBuffer.SetComputePipeline));

        RecordingState(commandBuffer, nameof(CommandBuffer.SetComputePipeline));

        if (pipeline?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Compute pipeline must be a valid, non-disposed compute pipeline.");
        }
    }

    public void SetRayTracingPipeline(CommandBuffer commandBuffer, RayTracingPipeline pipeline)
    {
        NotCopyQueue(commandBuffer, nameof(CommandBuffer.SetRayTracingPipeline));

        RecordingState(commandBuffer, nameof(CommandBuffer.SetRayTracingPipeline));

        if (pipeline?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Ray tracing pipeline must be a valid, non-disposed ray tracing pipeline.");
        }
    }

    public void SetIndexBuffer(CommandBuffer commandBuffer, IBuffer buffer, uint offsetInBytes, IndexFormat format)
    {
        DirectQueue(commandBuffer, nameof(CommandBuffer.SetIndexBuffer));

        RecordingState(commandBuffer, nameof(CommandBuffer.SetIndexBuffer));

        CurrentPipeline<GraphicsPipeline>(commandBuffer, nameof(CommandBuffer.SetIndexBuffer));

        if (buffer?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Index buffer must be a valid, non-disposed buffer.");

            return;
        }

        ObtainBufferValues(buffer,
                           out uint sizeInBytes,
                           out _,
                           out BufferUsageFlags flags,
                           "index buffer");

        if (offsetInBytes > sizeInBytes)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Index buffer offset ({offsetInBytes} bytes) exceeds buffer size ({sizeInBytes} bytes).");
        }

        if (!flags.HasFlag(BufferUsageFlags.Index))
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Warning,
                                         $"Index buffer should have BufferUsageFlags.Index. Current flags: {flags}.");
        }

        DefinedEnum(format, "index format");
    }

    public void SetVertexBuffers(CommandBuffer commandBuffer, IBuffer[] buffers, uint[] offsetsInBytes)
    {
        DirectQueue(commandBuffer, nameof(CommandBuffer.SetVertexBuffers));

        RecordingState(commandBuffer, nameof(CommandBuffer.SetVertexBuffers));

        CurrentPipeline<GraphicsPipeline>(commandBuffer, nameof(CommandBuffer.SetVertexBuffers));

        if (buffers is null || offsetsInBytes is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Vertex buffers and offsets cannot be null.");

            return;
        }

        if (buffers.Length is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Vertex buffers must contain at least 1 buffer.");

            return;
        }

        if (buffers.Length != offsetsInBytes.Length)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Vertex buffers and offsets must have the same length.");

            return;
        }

        for (int i = 0; i < buffers.Length; i++)
        {
            IBuffer buffer = buffers[i];
            uint offset = offsetsInBytes[i];

            if (buffer?.IsDisposed is not false)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"Vertex buffer at index {i} must be a valid, non-disposed buffer.");

                continue;
            }

            ObtainBufferValues(buffer,
                               out uint sizeInBytes,
                               out _,
                               out BufferUsageFlags flags,
                               $"vertex buffer at index {i}");

            if (offset > sizeInBytes)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"Vertex buffer at index {i} offset ({offset} bytes) exceeds buffer size ({sizeInBytes} bytes).");
            }

            if (!flags.HasFlag(BufferUsageFlags.Vertex))
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Warning,
                                             $"Vertex buffer at index {i} should have BufferUsageFlags.Vertex. Current flags: {flags}.");
            }
        }
    }

    public void PrepareResourceSets(CommandBuffer commandBuffer, ResourceSet[] sets)
    {
        RecordingState(commandBuffer, nameof(CommandBuffer.PrepareResourceSets));

        if (commandBuffer.CurrentFrameBuffer is not null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Resource sets cannot be prepared while a frame buffer is active. End the frame buffer before preparing resource sets.");
        }

        Objects(sets, "resource sets");
    }

    public void BindResourceSets(CommandBuffer commandBuffer, ResourceSet[] sets)
    {
        NotCopyQueue(commandBuffer, nameof(CommandBuffer.BindResourceSets));

        RecordingState(commandBuffer, nameof(CommandBuffer.BindResourceSets));

        if (commandBuffer.CurrentPipeline is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Cannot bind resource set when no pipeline is set. Set a pipeline before binding resource sets.");
        }

        Objects(sets, "resource sets");
    }

    private void DirectQueue(CommandBuffer commandBuffer, string name)
    {
        if (commandBuffer.Queue.Type is not CommandQueueType.Direct)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} can only be performed on the direct command queue. Current queue type: {commandBuffer.Queue.Type}.");
        }
    }

    private void NotCopyQueue(CommandBuffer commandBuffer, string name)
    {
        if (commandBuffer.Queue.Type is CommandQueueType.Copy)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} cannot be performed on the copy command queue. Current queue type: {commandBuffer.Queue.Type}.");
        }
    }

    private void RecordingState(CommandBuffer commandBuffer, string name)
    {
        if (commandBuffer.State is not CommandBufferState.Recording)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} can only be performed when the command buffer is in the recording state. Current state: {commandBuffer.State}.");
        }
    }

    private void CurrentPipeline<TPipeline>(CommandBuffer commandBuffer, string name) where TPipeline : GraphicsResource
    {
        if (commandBuffer.CurrentPipeline is not TPipeline)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} can only be performed when the current pipeline is of type {typeof(TPipeline).Name}. Current pipeline type: {commandBuffer.CurrentPipeline?.GetType().Name ?? "null"}.");
        }
    }

    private void RayTracingGeometry(RayTracingGeometry geometry, string name)
    {
        DefinedEnum(geometry.Type, $"{name} type");

        if (geometry.Type is RayTracingGeometryType.Triangles)
        {
            RayTracingTriangles triangles = geometry.Triangles;

            if (triangles.VertexBuffer?.IsDisposed is not false)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"{name} vertex buffer must be a valid, non-disposed buffer.");

                return;
            }

            ObtainBufferValues(triangles.VertexBuffer,
                               out _,
                               out _,
                               out BufferUsageFlags vertexFlags,
                               $"{name} vertex buffer");

            if (!vertexFlags.HasFlag(BufferUsageFlags.AccelerationStructure))
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Warning,
                                             $"{name} vertex buffer should have BufferUsageFlags.AccelerationStructure. Current flags: {vertexFlags}.");
            }

            DefinedEnum(triangles.VertexFormat, $"{name} vertex format");

            if (triangles.VertexCount is 0)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"{name} must have at least 1 vertex.");
            }

            if (triangles.VertexStrideInBytes is 0)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"{name} vertex stride must be greater than 0.");
            }

            if (triangles.IndexBuffer is not null)
            {
                if (triangles.IndexBuffer.IsDisposed)
                {
                    context.PublishDebugCallback(MessageCategory.System,
                                                 MessageSeverity.Error,
                                                 $"{name} index buffer must be a valid, non-disposed buffer.");

                    return;
                }

                ObtainBufferValues(triangles.IndexBuffer,
                                   out _,
                                   out _,
                                   out BufferUsageFlags indexFlags,
                                   $"{name} index buffer");

                if (!indexFlags.HasFlag(BufferUsageFlags.AccelerationStructure))
                {
                    context.PublishDebugCallback(MessageCategory.System,
                                                 MessageSeverity.Warning,
                                                 $"{name} index buffer should have BufferUsageFlags.AccelerationStructure. Current flags: {indexFlags}.");
                }

                DefinedEnum(triangles.IndexFormat, $"{name} index format");

                if (triangles.IndexCount is 0)
                {
                    context.PublishDebugCallback(MessageCategory.System,
                                                 MessageSeverity.Error,
                                                 $"{name} must have at least 1 index.");
                }

                Transform(triangles.Transform, $"{name} transform");
            }
        }
        else if (geometry.Type is RayTracingGeometryType.AABBs)
        {
            RayTracingAABBs aabbs = geometry.AABBs;

            if (aabbs.Buffer?.IsDisposed is not false)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"{name} AABB buffer must be a valid, non-disposed buffer.");

                return;
            }

            ObtainBufferValues(aabbs.Buffer,
                               out _,
                               out _,
                               out BufferUsageFlags aabbFlags,
                               $"{name} AABB buffer");

            if (!aabbFlags.HasFlag(BufferUsageFlags.AccelerationStructure))
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Warning,
                                             $"{name} AABB buffer should have BufferUsageFlags.AccelerationStructure. Current flags: {aabbFlags}.");
            }

            if (aabbs.Count is 0)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"{name} must have at least 1 AABB.");
            }

            if (aabbs.StrideInBytes is 0)
            {
                context.PublishDebugCallback(MessageCategory.System,
                                             MessageSeverity.Error,
                                             $"{name} AABB stride must be greater than 0.");
            }
        }
    }

    private void RayTracingInstance(RayTracingInstance instance, string name)
    {
        if (instance.AccelerationStructure?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} acceleration structure must be a valid, non-disposed acceleration structure.");

            return;
        }

        Transform(instance.Transform, $"{name} transform");
    }

    private void Transform(Matrix4x4 matrix, string name)
    {
        if (matrix.M11 is 0 && matrix.M22 is 0 && matrix.M33 is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Warning,
                                         $"{name} has a zero scale transform. This will make the instance invisible in the scene.");
        }
    }
}
