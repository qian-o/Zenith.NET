using System.Diagnostics;
using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

public static unsafe class Extensions
{
    extension(GraphicsContext)
    {
        public static GraphicsContext CreateVulkan(bool useValidationLayer)
        {
            return new VKGraphicsContext(useValidationLayer);
        }
    }

    extension(Result result)
    {
        internal void Success()
        {
            if (result is not Result.Success)
            {
                Debug.WriteLine($"Vulkan call failed with error: {result}");
            }
        }
    }

    extension<TChain, TNext>(ref TChain chain) where TChain : unmanaged, IChainable where TNext : unmanaged, IChainable
    {
        internal void AddNext(out TNext next)
        {
            next = default;
            next.StructureType();

            BaseInStructure* current = (BaseInStructure*)Unsafe.AsPointer(ref chain);
            while (current->PNext is not null)
            {
                current = current->PNext;
            }

            current->PNext = (BaseInStructure*)Unsafe.AsPointer(ref next);
        }
    }

    extension(BottomLevelAccelerationStructure bottomLevelAccelerationStructure)
    {
        internal VKBottomLevelAccelerationStructure Vulkan()
        {
            return (VKBottomLevelAccelerationStructure)bottomLevelAccelerationStructure;
        }
    }

    extension(Buffer buffer)
    {
        internal VKBuffer Vulkan()
        {
            return (VKBuffer)buffer;
        }
    }

    extension(BufferView bufferView)
    {
        internal VKBufferView Vulkan()
        {
            return (VKBufferView)bufferView;
        }
    }

    extension(CommandBuffer commandBuffer)
    {
        internal VKCommandBuffer Vulkan()
        {
            return (VKCommandBuffer)commandBuffer;
        }
    }

    extension(CommandQueue commandQueue)
    {
        internal VKCommandQueue Vulkan()
        {
            return (VKCommandQueue)commandQueue;
        }
    }

    extension(ComputePipeline computePipeline)
    {
        internal VKComputePipeline Vulkan()
        {
            return (VKComputePipeline)computePipeline;
        }
    }

    extension(GraphicsPipeline graphicsPipeline)
    {
        internal VKGraphicsPipeline Vulkan()
        {
            return (VKGraphicsPipeline)graphicsPipeline;
        }
    }

    extension(Heap heap)
    {
        internal VKHeap Vulkan()
        {
            return (VKHeap)heap;
        }
    }

    extension(MeshShadingPipeline meshShadingPipeline)
    {
        internal VKMeshShadingPipeline Vulkan()
        {
            return (VKMeshShadingPipeline)meshShadingPipeline;
        }
    }

    extension(QueryHeap queryHeap)
    {
        internal VKQueryHeap Vulkan()
        {
            return (VKQueryHeap)queryHeap;
        }
    }

    extension(Sampler sampler)
    {
        internal VKSampler Vulkan()
        {
            return (VKSampler)sampler;
        }
    }

    extension(Shader shader)
    {
        internal VKShader Vulkan()
        {
            return (VKShader)shader;
        }
    }

    extension(SwapChain swapChain)
    {
    }

    extension(Texture texture)
    {
        internal VKTexture Vulkan()
        {
            return (VKTexture)texture;
        }
    }

    extension(TextureView textureView)
    {
        internal VKTextureView Vulkan()
        {
            return (VKTextureView)textureView;
        }
    }

    extension(TopLevelAccelerationStructure topLevelAccelerationStructure)
    {
        internal VKTopLevelAccelerationStructure Vulkan()
        {
            return (VKTopLevelAccelerationStructure)topLevelAccelerationStructure;
        }
    }
}
