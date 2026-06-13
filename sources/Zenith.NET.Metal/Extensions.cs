using System.Diagnostics;
using System.Runtime.CompilerServices;
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
            return Unsafe.As<ulong, ResourceHandle>(ref value);
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