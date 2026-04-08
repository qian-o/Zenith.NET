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

    extension(ResourceBinding[] resourceBindings)
    {
        internal DescriptorSetLayout DescriptorSetLayout(VKGraphicsContext context)
        {
            using ZenithMarshal.Scope scope = new();

            DescriptorSetLayoutCreateInfo createInfo = new()
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = (uint)resourceBindings.Length,
                PBindings = (DescriptorSetLayoutBinding*)ZenithMarshal.AllocateAndFill(scope, [.. resourceBindings.Select(Vulkan)]),
                Flags = DescriptorSetLayoutCreateFlags.PushDescriptorBit
            };

            context.Vk.CreateDescriptorSetLayout(context.Device, &createInfo, null, out DescriptorSetLayout descriptorSetLayout).Success();

            return descriptorSetLayout;

            static DescriptorSetLayoutBinding Vulkan(ResourceBinding resourceBinding, int index)
            {
                return new DescriptorSetLayoutBinding
                {
                    Binding = (uint)index,
                    DescriptorType = VKFormats.Vulkan(resourceBinding.Type),
                    DescriptorCount = resourceBinding.Count,
                    StageFlags = VkShaderStageFlags.All
                };
            }
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
