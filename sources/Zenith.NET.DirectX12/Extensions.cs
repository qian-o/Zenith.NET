using System.Diagnostics;
using Silk.NET.Direct3D12;

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

    extension(ResourceSlot[] resourceSlots)
    {
        internal bool DirectX12(out DescriptorRange[] cbvSrvUavRanges, out DescriptorRange[] samplerRanges)
        {
            List<DescriptorRange> cbvSrvUavRangeList = [];
            List<DescriptorRange> samplerRangeList = [];

            uint cbvIndex = 0;
            uint srvIndex = 0;
            uint uavIndex = 0;
            uint samplerIndex = 0;
            uint cbvSrvUavRangeOffset = 0;
            uint samplerRangeOffset = 0;
            foreach (ResourceSlot resourceSlot in resourceSlots)
            {
                DescriptorRange range = new()
                {
                    RangeType = DXFormats.DirectX12(resourceSlot.Type),
                    NumDescriptors = resourceSlot.Count
                };

                switch (resourceSlot.Type)
                {
                    case ResourceType.ConstantBuffer:
                        range.BaseShaderRegister = cbvIndex++;
                        range.OffsetInDescriptorsFromTableStart = cbvSrvUavRangeOffset++;

                        cbvSrvUavRangeList.Add(range);
                        break;

                    case ResourceType.StructuredBuffer:
                    case ResourceType.Texture:
                    case ResourceType.AccelerationStructure:
                        range.BaseShaderRegister = srvIndex++;
                        range.OffsetInDescriptorsFromTableStart = cbvSrvUavRangeOffset++;

                        cbvSrvUavRangeList.Add(range);
                        break;

                    case ResourceType.StructuredBufferReadWrite:
                    case ResourceType.TextureReadWrite:
                        range.BaseShaderRegister = uavIndex++;
                        range.OffsetInDescriptorsFromTableStart = cbvSrvUavRangeOffset++;

                        cbvSrvUavRangeList.Add(range);
                        break;

                    case ResourceType.Sampler:
                        range.BaseShaderRegister = samplerIndex++;
                        range.OffsetInDescriptorsFromTableStart = samplerRangeOffset++;

                        samplerRangeList.Add(range);
                        break;
                }
            }

            cbvSrvUavRanges = [.. cbvSrvUavRangeList];
            samplerRanges = [.. samplerRangeList];

            return cbvSrvUavRanges.Length > 0 || samplerRanges.Length > 0;
        }
    }

    extension(CommandBuffer commandBuffer)
    {
        internal DXCommandBuffer DirectX12()
        {
            return (DXCommandBuffer)commandBuffer;
        }
    }

    extension(SwapChain swapChain)
    {
        internal DXSwapChain DirectX12()
        {
            return (DXSwapChain)swapChain;
        }
    }

    extension(FrameBuffer frameBuffer)
    {
        internal DXFrameBuffer DirectX12()
        {
            return (DXFrameBuffer)frameBuffer;
        }
    }

    extension(Shader shader)
    {
        internal DXShader DirectX12()
        {
            return (DXShader)shader;
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

    extension(Sampler sampler)
    {
        internal DXSampler DirectX12()
        {
            return (DXSampler)sampler;
        }
    }

    extension(BottomLevelAccelerationStructure bottomLevelAccelerationStructure)
    {
        internal DXBottomLevelAccelerationStructure DirectX12()
        {
            return (DXBottomLevelAccelerationStructure)bottomLevelAccelerationStructure;
        }
    }

    extension(TopLevelAccelerationStructure topLevelAccelerationStructure)
    {
        internal DXTopLevelAccelerationStructure DirectX12()
        {
            return (DXTopLevelAccelerationStructure)topLevelAccelerationStructure;
        }
    }

    extension(ResourceTable resourceTable)
    {
        internal DXResourceTable DirectX12()
        {
            return (DXResourceTable)resourceTable;
        }
    }

    extension(GraphicsPipeline graphicsPipeline)
    {
        internal DXGraphicsPipeline DirectX12()
        {
            return (DXGraphicsPipeline)graphicsPipeline;
        }
    }

    extension(ComputePipeline computePipeline)
    {
        internal DXComputePipeline DirectX12()
        {
            return (DXComputePipeline)computePipeline;
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
}
