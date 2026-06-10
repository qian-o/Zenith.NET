using System.Diagnostics;

namespace Zenith.NET.DirectX12;

public static class Extensions
{
    extension(GraphicsContext)
    {
        public static GraphicsContext CreateDirectX12(bool useValidationLayer)
        {
            return new DXGraphicsContext(useValidationLayer);
        }
    }

    extension(int result)
    {
        internal void Success()
        {
            if (result is not 0)
            {
                Debug.WriteLine($"DirectX call failed with error code: {result}");
            }
        }

        internal bool IsSuccess()
        {
            return result is 0;
        }
    }

    extension(BottomLevelAccelerationStructure bottomLevelAccelerationStructure)
    {
        internal DXBottomLevelAccelerationStructure DirectX12()
        {
            return (DXBottomLevelAccelerationStructure)bottomLevelAccelerationStructure;
        }
    }

    extension(Buffer buffer)
    {
        internal DXBuffer DirectX12()
        {
            return (DXBuffer)buffer;
        }
    }

    extension(BufferView bufferView)
    {
        internal DXBufferView DirectX12()
        {
            return (DXBufferView)bufferView;
        }
    }

    extension(CommandBuffer commandBuffer)
    {
        internal DXCommandBuffer DirectX12()
        {
            return (DXCommandBuffer)commandBuffer;
        }
    }

    extension(CommandQueue commandQueue)
    {
        internal DXCommandQueue DirectX12()
        {
            return (DXCommandQueue)commandQueue;
        }
    }

    extension(ComputePipeline computePipeline)
    {
        internal DXComputePipeline DirectX12()
        {
            return (DXComputePipeline)computePipeline;
        }
    }

    extension(GraphicsPipeline graphicsPipeline)
    {
        internal DXGraphicsPipeline DirectX12()
        {
            return (DXGraphicsPipeline)graphicsPipeline;
        }
    }

    extension(Heap heap)
    {
        internal DXHeap DirectX12()
        {
            return (DXHeap)heap;
        }
    }

    extension(MeshShadingPipeline meshShadingPipeline)
    {
        internal DXMeshShadingPipeline DirectX12()
        {
            return (DXMeshShadingPipeline)meshShadingPipeline;
        }
    }

    extension(QueryHeap queryHeap)
    {
        internal DXQueryHeap DirectX12()
        {
            return (DXQueryHeap)queryHeap;
        }
    }

    extension(Sampler sampler)
    {
        internal DXSampler DirectX12()
        {
            return (DXSampler)sampler;
        }
    }

    extension(Shader shader)
    {
        internal DXShader DirectX12()
        {
            return (DXShader)shader;
        }
    }

    extension(SwapChain swapChain)
    {
        internal DXSwapChain DirectX12()
        {
            return (DXSwapChain)swapChain;
        }
    }

    extension(Texture texture)
    {
        internal DXTexture DirectX12()
        {
            return (DXTexture)texture;
        }
    }

    extension(TextureView textureView)
    {
        internal DXTextureView DirectX12()
        {
            return (DXTextureView)textureView;
        }
    }

    extension(TopLevelAccelerationStructure topLevelAccelerationStructure)
    {
        internal DXTopLevelAccelerationStructure DirectX12()
        {
            return (DXTopLevelAccelerationStructure)topLevelAccelerationStructure;
        }
    }
}
