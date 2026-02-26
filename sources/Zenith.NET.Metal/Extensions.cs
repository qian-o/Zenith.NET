using System.Diagnostics;
using Metal.NET;

namespace Zenith.NET.Metal;

public static class Extensions
{
    extension(GraphicsContext)
    {
        public static GraphicsContext CreateMetal(bool useValidationLayer)
        {
            return new MTLGraphicsContext(useValidationLayer);
        }
    }

    extension(NSError error)
    {
        internal void Success()
        {
            if (!error.IsNull)
            {
                Debug.WriteLine($"Metal call failed with error: {error.LocalizedDescription}");

                error.Dispose();
            }
        }
    }

    extension(CommandBuffer commandBuffer)
    {
    }

    extension(SwapChain swapChain)
    {
    }

    extension(FrameBuffer frameBuffer)
    {
    }

    extension(Shader shader)
    {
    }

    extension(Buffer buffer)
    {
        internal MTLBuffer Metal()
        {
            return (MTLBuffer)buffer;
        }
    }

    extension(BufferView bufferView)
    {
    }

    extension(Texture texture)
    {
        internal MTLTexture Metal()
        {
            return (MTLTexture)texture;
        }
    }

    extension(TextureView textureView)
    {
    }

    extension(Sampler sampler)
    {
    }

    extension(BottomLevelAccelerationStructure bottomLevelAccelerationStructure)
    {
    }

    extension(TopLevelAccelerationStructure topLevelAccelerationStructure)
    {
    }

    extension(ResourceLayout resourceLayout)
    {
    }

    extension(ResourceTable resourceTable)
    {
    }

    extension(GraphicsPipeline graphicsPipeline)
    {
    }

    extension(ComputePipeline computePipeline)
    {
    }

    extension(MeshShadingPipeline meshShadingPipeline)
    {
    }

    extension(QueryHeap queryHeap)
    {
    }
}