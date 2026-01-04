using System.Diagnostics;
using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal static unsafe class VKExtensions
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

    extension<TChain, TNext>(ref TChain chain) where TChain : unmanaged, IChainable where TNext : unmanaged, IChainable
    {
        public void AddNext(out TNext next)
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

    extension(BottomLevelAccelerationStructure bottomLevelAccelerationStructure)
    {
        public VKBottomLevelAccelerationStructure Vulkan()
        {
            return (VKBottomLevelAccelerationStructure)bottomLevelAccelerationStructure;
        }
    }

    extension(TopLevelAccelerationStructure topLevelAccelerationStructure)
    {
        public VKTopLevelAccelerationStructure Vulkan()
        {
            return (VKTopLevelAccelerationStructure)topLevelAccelerationStructure;
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

    extension(RayTracingPipeline rayTracingPipeline)
    {
        public VKRayTracingPipeline Vulkan()
        {
            return (VKRayTracingPipeline)rayTracingPipeline;
        }
    }

    extension(MeshShadingPipeline meshShadingPipeline)
    {
        public VKMeshShadingPipeline Vulkan()
        {
            return (VKMeshShadingPipeline)meshShadingPipeline;
        }
    }

    extension(QueryHeap queryHeap)
    {
        public VKQueryHeap Vulkan()
        {
            return (VKQueryHeap)queryHeap;
        }
    }
}
