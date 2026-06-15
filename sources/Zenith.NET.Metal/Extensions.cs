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
        internal ResourceHandle ToHandle()
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
        internal MTLComputePipeline Metal()
        {
            return (MTLComputePipeline)computePipeline;
        }
    }

    extension(GraphicsPipeline graphicsPipeline)
    {
        internal MTLGraphicsPipeline Metal()
        {
            return (MTLGraphicsPipeline)graphicsPipeline;
        }
    }

    extension(Heap heap)
    {
        internal MTLHeap Metal()
        {
            return (MTLHeap)heap;
        }
    }

    extension(MeshShadingPipeline meshShadingPipeline)
    {
        internal MTLMeshShadingPipeline Metal()
        {
            return (MTLMeshShadingPipeline)meshShadingPipeline;
        }
    }

    extension(QueryHeap queryHeap)
    {
        internal MTLQueryHeap Metal()
        {
            return (MTLQueryHeap)queryHeap;
        }
    }

    extension(Sampler sampler)
    {
        internal MTLSampler Metal()
        {
            return (MTLSampler)sampler;
        }
    }

    extension(Shader shader)
    {
        internal MTLShader Metal()
        {
            return (MTLShader)shader;
        }
    }

    extension(SwapChain swapChain)
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
        internal MTLTextureView Metal()
        {
            return (MTLTextureView)textureView;
        }
    }

    extension(TopLevelAccelerationStructure topLevelAccelerationStructure)
    {
    }
}