using System.Diagnostics;
using Silk.NET.Vulkan;

namespace Zenith.NET;

internal static class VKExtensions
{
    extension(Result result)
    {
        public void Success()
        {
            if (result is not Result.Success)
            {
                Debug.WriteLine($"Vulkan call failed with error: {result}");
            }
        }
    }

    extension(CommandBuffer commandBuffer)
    {
        public VKCommandBuffer Vulkan()
        {
            return (VKCommandBuffer)commandBuffer;
        }
    }

    extension(SwapChain swapChain)
    {
        public VKSwapChain Vulkan()
        {
            return (VKSwapChain)swapChain;
        }
    }


    extension(FrameBuffer frameBuffer)
    {
        public VKFrameBuffer Vulkan()
        {
            return (VKFrameBuffer)frameBuffer;
        }
    }

    extension(Shader shader)
    {
        public VKShader Vulkan()
        {
            return (VKShader)shader;
        }
    }

    extension(Buffer buffer)
    {
        public VKBuffer Vulkan()
        {
            return (VKBuffer)buffer;
        }
    }

    extension(BufferView bufferView)
    {
        public VKBufferView Vulkan()
        {
            return (VKBufferView)bufferView;
        }
    }

    extension(Texture texture)
    {
        public VKTexture Vulkan()
        {
            return (VKTexture)texture;
        }
    }

    extension(TextureView textureView)
    {
        public VKTextureView Vulkan()
        {
            return (VKTextureView)textureView;
        }
    }

    extension(Sampler sampler)
    {
        public VKSampler Vulkan()
        {
            return (VKSampler)sampler;
        }
    }

    extension(ResourceLayout resourceLayout)
    {
        public VKResourceLayout Vulkan()
        {
            return (VKResourceLayout)resourceLayout;
        }
    }

    extension(ResourceSet resourceSet)
    {
        public VKResourceSet Vulkan()
        {
            return (VKResourceSet)resourceSet;
        }
    }

    extension(GraphicsPipeline graphicsPipeline)
    {
        public VKGraphicsPipeline Vulkan()
        {
            return (VKGraphicsPipeline)graphicsPipeline;
        }
    }

    extension(ComputePipeline computePipeline)
    {
        public VKComputePipeline Vulkan()
        {
            return (VKComputePipeline)computePipeline;
        }
    }
}
