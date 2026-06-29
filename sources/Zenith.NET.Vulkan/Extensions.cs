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
    }

    extension(Buffer buffer)
    {
    }

    extension(BufferView bufferView)
    {
    }

    extension(CommandBuffer commandBuffer)
    {
    }

    extension(CommandQueue commandQueue)
    {
    }

    extension(ComputePipeline computePipeline)
    {
    }

    extension(GraphicsPipeline graphicsPipeline)
    {
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
