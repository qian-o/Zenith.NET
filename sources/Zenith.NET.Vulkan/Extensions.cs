using System.Diagnostics;
using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

public static class Extensions
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

            unsafe
            {
                BaseInStructure* current = (BaseInStructure*)Unsafe.AsPointer(ref chain);
                while (current->PNext is not null)
                {
                    current = current->PNext;
                }

                current->PNext = (BaseInStructure*)Unsafe.AsPointer(ref next);
            }
        }
    }

    extension(ResourceSlot[] resourceSlots)
    {
        internal void Vulkan(out DescriptorSetLayoutBinding[] bindings, out VKDescriptorCounts counts)
        {
            bindings = new DescriptorSetLayoutBinding[resourceSlots.Length];

            uint uniformBufferCount = 0;
            uint storageBufferCount = 0;
            uint sampledImageCount = 0;
            uint storageImageCount = 0;
            uint samplerCount = 0;
            uint accelerationStructureCount = 0;

            for (int i = 0; i < resourceSlots.Length; i++)
            {
                ResourceSlot resourceSlot = resourceSlots[i];

                bindings[i] = new()
                {
                    Binding = (uint)i,
                    DescriptorType = VKFormats.Vulkan(resourceSlot.Type),
                    DescriptorCount = resourceSlot.Count,
                    StageFlags = VkShaderStageFlags.All
                };

                switch (resourceSlot.Type)
                {
                    case ResourceType.ConstantBuffer:
                        uniformBufferCount += resourceSlot.Count;
                        break;

                    case ResourceType.StructuredBuffer:
                    case ResourceType.StructuredBufferReadWrite:
                        storageBufferCount += resourceSlot.Count;
                        break;

                    case ResourceType.Texture:
                        sampledImageCount += resourceSlot.Count;
                        break;

                    case ResourceType.TextureReadWrite:
                        storageImageCount += resourceSlot.Count;
                        break;

                    case ResourceType.Sampler:
                        samplerCount += resourceSlot.Count;
                        break;

                    case ResourceType.AccelerationStructure:
                        accelerationStructureCount += resourceSlot.Count;
                        break;
                }
            }

            counts = new(uniformBufferCount, storageBufferCount, sampledImageCount, storageImageCount, samplerCount, accelerationStructureCount);
        }
    }

    extension(CommandBuffer commandBuffer)
    {
        internal VKCommandBuffer Vulkan()
        {
            return (VKCommandBuffer)commandBuffer;
        }
    }

    extension(SwapChain swapChain)
    {
        internal VKSwapChain Vulkan()
        {
            return (VKSwapChain)swapChain;
        }
    }

    extension(FrameBuffer frameBuffer)
    {
        internal VKFrameBuffer Vulkan()
        {
            return (VKFrameBuffer)frameBuffer;
        }
    }

    extension(Shader shader)
    {
        internal VKShader Vulkan()
        {
            return (VKShader)shader;
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

    extension(Sampler sampler)
    {
        internal VKSampler Vulkan()
        {
            return (VKSampler)sampler;
        }
    }

    extension(BottomLevelAccelerationStructure bottomLevelAccelerationStructure)
    {
        internal VKBottomLevelAccelerationStructure Vulkan()
        {
            return (VKBottomLevelAccelerationStructure)bottomLevelAccelerationStructure;
        }
    }

    extension(TopLevelAccelerationStructure topLevelAccelerationStructure)
    {
        internal VKTopLevelAccelerationStructure Vulkan()
        {
            return (VKTopLevelAccelerationStructure)topLevelAccelerationStructure;
        }
    }

    extension(ResourceTable resourceTable)
    {
        internal VKResourceTable Vulkan()
        {
            return (VKResourceTable)resourceTable;
        }
    }

    extension(GraphicsPipeline graphicsPipeline)
    {
        internal VKGraphicsPipeline Vulkan()
        {
            return (VKGraphicsPipeline)graphicsPipeline;
        }
    }

    extension(ComputePipeline computePipeline)
    {
        internal VKComputePipeline Vulkan()
        {
            return (VKComputePipeline)computePipeline;
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
}
