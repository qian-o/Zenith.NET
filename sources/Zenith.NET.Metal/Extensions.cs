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

    extension(ulong value)
    {
        internal ResourceHandle ToResourceHandle()
        {
            return new((uint)value, (uint)(value >> 32));
        }
    }

    extension(BottomLevelAccelerationStructure bottomLevelAccelerationStructure)
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
        internal MTLBufferView Metal()
        {
            return (MTLBufferView)bufferView;
        }
    }

    extension(CommandBuffer commandBuffer)
    {
    }

    extension(CommandQueue commandQueue)
    {
        internal MTLCommandQueue Metal()
        {
            return (MTLCommandQueue)commandQueue;
        }
    }

    extension(ComputePipeline computePipeline)
    {
    }

    extension(GraphicsPipeline graphicsPipeline)
    {
    }

    extension(Heap heap)
    {
    }

    extension(MeshShadingPipeline meshShadingPipeline)
    {
    }

    extension(QueryHeap queryHeap)
    {
    }

    extension(Sampler sampler)
    {
    }

    extension(Shader shader)
    {
    }

    extension(SwapChain swapChain)
    {
    }

    extension(Texture texture)
    {
    }

    extension(TextureView textureView)
    {
    }

    extension(TopLevelAccelerationStructure topLevelAccelerationStructure)
    {
    }
}