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

    extension(NSAutoreleasePool)
    {
        public static T Run<T>(Func<T> func)
        {
            using NSAutoreleasePool _ = new();

            return func();
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
        internal MTLCommandBuffer Metal()
        {
            return (MTLCommandBuffer)commandBuffer;
        }
    }

    extension(SwapChain swapChain)
    {
        internal MTLSwapChain Metal()
        {
            return (MTLSwapChain)swapChain;
        }
    }

    extension(FrameBuffer frameBuffer)
    {
        internal MTLFrameBuffer Metal()
        {
            return (MTLFrameBuffer)frameBuffer;
        }
    }

    extension(Shader shader)
    {
        internal MTLShader Metal()
        {
            return (MTLShader)shader;
        }
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

    extension(Sampler sampler)
    {
        internal MTLSampler Metal()
        {
            return (MTLSampler)sampler;
        }
    }

    extension(BottomLevelAccelerationStructure bottomLevelAccelerationStructure)
    {
        internal MTLBottomLevelAccelerationStructure Metal()
        {
            return (MTLBottomLevelAccelerationStructure)bottomLevelAccelerationStructure;
        }
    }

    extension(TopLevelAccelerationStructure topLevelAccelerationStructure)
    {
        internal MTLTopLevelAccelerationStructure Metal()
        {
            return (MTLTopLevelAccelerationStructure)topLevelAccelerationStructure;
        }
    }

    extension(ResourceLayout resourceLayout)
    {
        internal MTLResourceLayout Metal()
        {
            return (MTLResourceLayout)resourceLayout;
        }
    }

    extension(ResourceTable resourceTable)
    {
        internal MTLResourceTable Metal()
        {
            return (MTLResourceTable)resourceTable;
        }
    }

    extension(GraphicsPipeline graphicsPipeline)
    {
        internal MTLGraphicsPipeline Metal()
        {
            return (MTLGraphicsPipeline)graphicsPipeline;
        }
    }

    extension(ComputePipeline computePipeline)
    {
        internal MTLComputePipeline Metal()
        {
            return (MTLComputePipeline)computePipeline;
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
}