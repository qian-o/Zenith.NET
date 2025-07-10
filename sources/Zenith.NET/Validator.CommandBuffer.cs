using System.Numerics;
using System.Runtime.CompilerServices;

namespace Zenith.NET;

internal partial class Validator
{
    public void ValidateBegin(CommandBuffer commandBuffer)
    {
        if (commandBuffer.State is not CommandBufferState.Idle)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Command buffer can only be started when in Idle state. Current state: {commandBuffer.State}.");
        }
    }

    public void ValidateEnd(CommandBuffer commandBuffer)
    {
        if (commandBuffer.State is not CommandBufferState.Recording)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Command buffer can only be ended when in Recording state. Current state: {commandBuffer.State}.");
        }
    }

    public void ValidateSubmit(CommandBuffer commandBuffer)
    {
        if (commandBuffer.State is not CommandBufferState.Completed)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"Command buffer can only be submitted when in Completed state. Current state: {commandBuffer.State}.");
        }
    }

    public void ValidateUploadBuffer<T>(CommandBuffer commandBuffer,
                                        IBuffer buffer,
                                        uint offsetInBytes,
                                        ReadOnlySpan<T> data)
    {
        ValidateRecordingState(commandBuffer, nameof(CommandBuffer.UploadBuffer));

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

    public void ValidateCopyBuffer(CommandBuffer commandBuffer,
                                   IBuffer src,
                                   uint srcOffsetInBytes,
                                   IBuffer dest,
                                   uint destOffsetInBytes,
                                   uint sizeInBytes)
    {
        ValidateRecordingState(commandBuffer, nameof(CommandBuffer.CopyBuffer));

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

    public void ValidateUploadTexture<T>(CommandBuffer commandBuffer,
                                         ITexture texture,
                                         TextureSlice slice,
                                         TextureOffset offset,
                                         TextureExtent extent,
                                         ReadOnlySpan<T> data)
    {
        ValidateRecordingState(commandBuffer, nameof(CommandBuffer.UploadTexture));

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

        ValidateTextureSlice(type, layers, mipLevels, slice, "texture slice for upload");

        ValidateTextureRange(width, height, depth, offset, extent, "texture offset and extent for upload");
    }

    public void ValidateCopyTexture(CommandBuffer commandBuffer,
                                    IBuffer src,
                                    uint srcOffsetInBytes,
                                    uint srcSizeInBytes,
                                    ITexture dest,
                                    TextureSlice destSlice,
                                    TextureOffset destOffset,
                                    TextureExtent destExtent)
    {
        ValidateRecordingState(commandBuffer, nameof(CommandBuffer.CopyTexture));

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

        ValidateTextureSlice(type, layers, mipLevels, destSlice, "destination texture slice for copy");

        ValidateTextureRange(width, height, depth, destOffset, destExtent, "destination texture offset and extent for copy");
    }

    public void ValidateCopyTexture(CommandBuffer commandBuffer,
                                    ITexture src,
                                    TextureSlice srcSlice,
                                    TextureOffset srcOffset,
                                    ITexture dest,
                                    TextureSlice destSlice,
                                    TextureOffset destOffset,
                                    TextureExtent extent)
    {
        ValidateRecordingState(commandBuffer, nameof(CommandBuffer.CopyTexture));

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

        ValidateTextureSlice(srcType, srcLayers, srcMipLevels, srcSlice, "source texture slice for copy");

        ValidateTextureRange(srcWidth, srcHeight, srcDepth, srcOffset, extent, "source texture offset and extent for copy");

        ValidateTextureSlice(destType, destLayers, destMipLevels, destSlice, "destination texture slice for copy");

        ValidateTextureRange(destWidth, destHeight, destDepth, destOffset, extent, "destination texture offset and extent for copy");
    }

    public void ValidateResolveTexture(CommandBuffer commandBuffer,
                                       ITexture src,
                                       TextureSlice srcSlice,
                                       ITexture dest,
                                       TextureSlice destSlice)
    {
        ValidateDirectQueue(commandBuffer, nameof(CommandBuffer.ResolveTexture));

        ValidateRecordingState(commandBuffer, nameof(CommandBuffer.ResolveTexture));

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

        ValidateTextureSlice(srcType, srcLayers, srcMipLevels, srcSlice, "source texture slice for resolve");

        ValidateTextureSlice(destType, destLayers, destMipLevels, destSlice, "destination texture slice for resolve");

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

    public void ValidateBuildBottomLevelAccelerationStructure(CommandBuffer commandBuffer,
                                                              BottomLevelAccelerationStructureDesc desc)
    {
        ValidateNotCopyQueue(commandBuffer, nameof(CommandBuffer.BuildBottomLevelAccelerationStructure));

        ValidateRecordingState(commandBuffer, nameof(CommandBuffer.BuildBottomLevelAccelerationStructure));

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
            ValidateRayTracingGeometry(desc.Geometries[i], $"geometry at index {i}");
        }
    }

    public void ValidateBuildTopLevelAccelerationStructure(CommandBuffer commandBuffer,
                                                           TopLevelAccelerationStructureDesc desc)
    {
        ValidateNotCopyQueue(commandBuffer, nameof(CommandBuffer.BuildTopLevelAccelerationStructure));

        ValidateRecordingState(commandBuffer, nameof(CommandBuffer.BuildTopLevelAccelerationStructure));

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
            ValidateRayTracingInstance(desc.Instances[i], $"instance at index {i}");
        }
    }

    public void ValidateUpdateTopLevelAccelerationStructure(CommandBuffer commandBuffer,
                                                            TopLevelAccelerationStructure accelerationStructure,
                                                            TopLevelAccelerationStructureDesc newDesc)
    {
        ValidateDirectQueue(commandBuffer, nameof(CommandBuffer.UpdateTopLevelAccelerationStructure));

        ValidateRecordingState(commandBuffer, nameof(CommandBuffer.UpdateTopLevelAccelerationStructure));

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
            ValidateRayTracingInstance(newDesc.Instances[i], $"instance at index {i}");
        }
    }

    public void ValidateBeginRendering(CommandBuffer commandBuffer, FrameBuffer frameBuffer, ClearValue clearValue)
    {
        ValidateDirectQueue(commandBuffer, nameof(CommandBuffer.BeginRendering));

        ValidateRecordingState(commandBuffer, nameof(CommandBuffer.BeginRendering));

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

    public void ValidateEndRendering(CommandBuffer commandBuffer)
    {
        ValidateDirectQueue(commandBuffer, nameof(CommandBuffer.EndRendering));

        ValidateRecordingState(commandBuffer, nameof(CommandBuffer.EndRendering));
    }

    public void ValidateSetScissors(CommandBuffer commandBuffer, Scissor[] scissors)
    {
        ValidateDirectQueue(commandBuffer, nameof(CommandBuffer.SetScissors));

        ValidateRecordingState(commandBuffer, nameof(CommandBuffer.SetScissors));

        if (scissors is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Scissors cannot be null.");
        }
    }

    public void ValidateSetViewports(CommandBuffer commandBuffer, Viewport[] viewports)
    {
        ValidateDirectQueue(commandBuffer, nameof(CommandBuffer.SetViewports));

        ValidateRecordingState(commandBuffer, nameof(CommandBuffer.SetViewports));

        if (viewports is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Viewports cannot be null.");
        }
    }

    public void ValidateSetGraphicsPipeline(CommandBuffer commandBuffer, GraphicsPipeline pipeline)
    {
        ValidateDirectQueue(commandBuffer, nameof(CommandBuffer.SetGraphicsPipeline));

        ValidateRecordingState(commandBuffer, nameof(CommandBuffer.SetGraphicsPipeline));

        if (pipeline?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Graphics pipeline must be a valid, non-disposed graphics pipeline.");
        }
    }

    public void ValidateSetComputePipeline(CommandBuffer commandBuffer, ComputePipeline pipeline)
    {
        ValidateNotCopyQueue(commandBuffer, nameof(CommandBuffer.SetComputePipeline));

        ValidateRecordingState(commandBuffer, nameof(CommandBuffer.SetComputePipeline));

        if (pipeline?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Compute pipeline must be a valid, non-disposed compute pipeline.");
        }
    }

    public void ValidateSetRayTracingPipeline(CommandBuffer commandBuffer, RayTracingPipeline pipeline)
    {
        ValidateNotCopyQueue(commandBuffer, nameof(CommandBuffer.SetRayTracingPipeline));

        ValidateRecordingState(commandBuffer, nameof(CommandBuffer.SetRayTracingPipeline));

        if (pipeline?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Ray tracing pipeline must be a valid, non-disposed ray tracing pipeline.");
        }
    }

    public void ValidateSetIndexBuffer(CommandBuffer commandBuffer, IBuffer buffer, uint offsetInBytes, IndexFormat format)
    {
        ValidateDirectQueue(commandBuffer, nameof(CommandBuffer.SetIndexBuffer));

        ValidateRecordingState(commandBuffer, nameof(CommandBuffer.SetIndexBuffer));

        ValidateCurrentPipeline<GraphicsPipeline>(commandBuffer, nameof(CommandBuffer.SetIndexBuffer));

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

        ValidateDefinedEnum(format, "index format");
    }

    public void ValidateSetVertexBuffers(CommandBuffer commandBuffer, IBuffer[] buffers, uint[] offsetsInBytes)
    {
        ValidateDirectQueue(commandBuffer, nameof(CommandBuffer.SetVertexBuffers));

        ValidateRecordingState(commandBuffer, nameof(CommandBuffer.SetVertexBuffers));

        ValidateCurrentPipeline<GraphicsPipeline>(commandBuffer, nameof(CommandBuffer.SetVertexBuffers));

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

    public void ValidatePrepareResourceSets(CommandBuffer commandBuffer, ResourceSet[] sets)
    {
        ValidateRecordingState(commandBuffer, nameof(CommandBuffer.PrepareResourceSets));

        if (commandBuffer.CurrentFrameBuffer is not null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Resource sets cannot be prepared while a frame buffer is active. End the frame buffer before preparing resource sets.");
        }

        ValidateObjects(sets, "resource sets");
    }

    public void ValidateBindResourceSets(CommandBuffer commandBuffer, ResourceSet[] sets)
    {
        ValidateNotCopyQueue(commandBuffer, nameof(CommandBuffer.BindResourceSets));

        ValidateRecordingState(commandBuffer, nameof(CommandBuffer.BindResourceSets));

        if (commandBuffer.CurrentPipeline is null)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         "Cannot bind resource set when no pipeline is set. Set a pipeline before binding resource sets.");
        }

        ValidateObjects(sets, "resource sets");
    }

    private void ValidateDirectQueue(CommandBuffer commandBuffer, string name)
    {
        if (commandBuffer.Queue.Type is not CommandQueueType.Direct)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} can only be performed on the direct command queue. Current queue type: {commandBuffer.Queue.Type}.");
        }
    }

    private void ValidateNotCopyQueue(CommandBuffer commandBuffer, string name)
    {
        if (commandBuffer.Queue.Type is CommandQueueType.Copy)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} cannot be performed on the copy command queue. Current queue type: {commandBuffer.Queue.Type}.");
        }
    }

    private void ValidateRecordingState(CommandBuffer commandBuffer, string name)
    {
        if (commandBuffer.State is not CommandBufferState.Recording)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} can only be performed when the command buffer is in the recording state. Current state: {commandBuffer.State}.");
        }
    }

    private void ValidateCurrentPipeline<TPipeline>(CommandBuffer commandBuffer, string name) where TPipeline : GraphicsResource
    {
        if (commandBuffer.CurrentPipeline is not TPipeline)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} can only be performed when the current pipeline is of type {typeof(TPipeline).Name}. Current pipeline type: {commandBuffer.CurrentPipeline?.GetType().Name ?? "null"}.");
        }
    }

    private void ValidateRayTracingGeometry(RayTracingGeometry geometry, string name)
    {
        ValidateDefinedEnum(geometry.Type, $"{name} type");

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

            ValidateDefinedEnum(triangles.VertexFormat, $"{name} vertex format");

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

                ValidateDefinedEnum(triangles.IndexFormat, $"{name} index format");

                if (triangles.IndexCount is 0)
                {
                    context.PublishDebugCallback(MessageCategory.System,
                                                 MessageSeverity.Error,
                                                 $"{name} must have at least 1 index.");
                }

                ValidateTransform(triangles.Transform, $"{name} transform");
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

    private void ValidateRayTracingInstance(RayTracingInstance instance, string name)
    {
        if (instance.AccelerationStructure?.IsDisposed is not false)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Error,
                                         $"{name} acceleration structure must be a valid, non-disposed acceleration structure.");

            return;
        }

        ValidateTransform(instance.Transform, $"{name} transform");
    }

    private void ValidateTransform(Matrix4x4 matrix, string name)
    {
        if (matrix.M11 is 0 && matrix.M22 is 0 && matrix.M33 is 0)
        {
            context.PublishDebugCallback(MessageCategory.System,
                                         MessageSeverity.Warning,
                                         $"{name} has a zero scale transform. This will make the instance invisible in the scene.");
        }
    }
}
