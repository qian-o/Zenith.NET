using System.Diagnostics;

namespace Zenith.NET.DirectX12;

internal static class DXExtensions
{
    extension(int result)
    {
        public void Success()
        {
            if (result is not 0)
            {
                Debug.WriteLine($"DirectX call failed with error code: {result}");
            }
        }

        public bool IsSuccess()
        {
            return result is 0;
        }
    }

    extension(CommandBuffer commandBuffer)
    {
        public DXCommandBuffer DirectX12()
        {
            return (DXCommandBuffer)commandBuffer;
        }
    }

    extension(SwapChain swapChain)
    {
        public DXSwapChain DirectX12()
        {
            return (DXSwapChain)swapChain;
        }
    }


    extension(FrameBuffer frameBuffer)
    {
        public DXFrameBuffer DirectX12()
        {
            return (DXFrameBuffer)frameBuffer;
        }
    }

    extension(Shader shader)
    {
        public DXShader DirectX12()
        {
            return (DXShader)shader;
        }
    }

    extension(Buffer buffer)
    {
        public DXBuffer DirectX12()
        {
            return (DXBuffer)buffer;
        }
    }

    extension(BufferView bufferView)
    {
        public DXBufferView DirectX12()
        {
            return (DXBufferView)bufferView;
        }
    }

    extension(Texture texture)
    {
        public DXTexture DirectX12()
        {
            return (DXTexture)texture;
        }
    }

    extension(TextureView textureView)
    {
        public DXTextureView DirectX12()
        {
            return (DXTextureView)textureView;
        }
    }

    extension(Sampler sampler)
    {
        public DXSampler DirectX12()
        {
            return (DXSampler)sampler;
        }
    }

    extension(BottomLevelAccelerationStructure bottomLevelAccelerationStructure)
    {
    }

    extension(TopLevelAccelerationStructure topLevelAccelerationStructure)
    {
    }

    extension(ResourceLayout resourceLayout)
    {
        public DXResourceLayout DirectX12()
        {
            return (DXResourceLayout)resourceLayout;
        }
    }

    extension(ResourceSet resourceSet)
    {
        public DXResourceSet DirectX12()
        {
            return (DXResourceSet)resourceSet;
        }
    }

    extension(GraphicsPipeline graphicsPipeline)
    {
        public DXGraphicsPipeline DirectX12()
        {
            return (DXGraphicsPipeline)graphicsPipeline;
        }
    }

    extension(ComputePipeline computePipeline)
    {
        public DXComputePipeline DirectX12()
        {
            return (DXComputePipeline)computePipeline;
        }
    }

    extension(RayTracingPipeline rayTracingPipeline)
    {
    }

    extension(MeshShadingPipeline meshShadingPipeline)
    {
    }

    extension(QueryHeap queryHeap)
    {
        public DXQueryHeap DirectX12()
        {
            return (DXQueryHeap)queryHeap;
        }
    }
}
