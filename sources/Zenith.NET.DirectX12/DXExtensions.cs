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
    }

    extension(SwapChain swapChain)
    {
    }


    extension(FrameBuffer frameBuffer)
    {
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

    extension(ResourceSet resourceSet)
    {
    }

    extension(GraphicsPipeline graphicsPipeline)
    {
    }

    extension(ComputePipeline computePipeline)
    {
    }

    extension(RayTracingPipeline rayTracingPipeline)
    {
    }

    extension(MeshShadingPipeline meshShadingPipeline)
    {
    }

    extension(QueryHeap queryHeap)
    {
    }
}
