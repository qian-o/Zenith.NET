using System.Diagnostics;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

public static unsafe class Extensions
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

    extension(ResourceBinding[] resourceBindings)
    {
        internal void DirectX12(out DescriptorRange[] cbvSrvUavRanges, out DescriptorRange[] samplerRanges)
        {
            List<DescriptorRange> cbvSrvUavRangeList = [];
            List<DescriptorRange> samplerRangeList = [];

            uint cbvIndex = 0;
            uint srvIndex = 0;
            uint uavIndex = 0;
            uint samplerIndex = 0;
            uint cbvSrvUavRangeOffset = 0;
            uint samplerRangeOffset = 0;
            foreach (ResourceBinding resourceBinding in resourceBindings)
            {
                DescriptorRange range = new()
                {
                    RangeType = DXFormats.DirectX12(resourceBinding.Type),
                    NumDescriptors = resourceBinding.Count
                };

                switch (resourceBinding.Type)
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
        }

        internal ComPtr<ID3D12RootSignature> RootSignature(DXGraphicsContext context)
        {
            using ZenithMarshal.Scope scope = new();

            resourceBindings.DirectX12(out DescriptorRange[] cbvSrvUavRanges, out DescriptorRange[] samplerRanges);

            List<RootParameter> parameters = [];

            if (cbvSrvUavRanges.Length > 0)
            {
                parameters.Add(new()
                {
                    ParameterType = RootParameterType.TypeDescriptorTable,
                    DescriptorTable = new()
                    {
                        NumDescriptorRanges = (uint)cbvSrvUavRanges.Length,
                        PDescriptorRanges = (DescriptorRange*)ZenithMarshal.AllocateAndFill(scope, cbvSrvUavRanges)
                    }
                });
            }

            if (samplerRanges.Length > 0)
            {
                parameters.Add(new()
                {
                    ParameterType = RootParameterType.TypeDescriptorTable,
                    DescriptorTable = new()
                    {
                        NumDescriptorRanges = (uint)samplerRanges.Length,
                        PDescriptorRanges = (DescriptorRange*)ZenithMarshal.AllocateAndFill(scope, samplerRanges)
                    }
                });
            }

            RootSignatureDesc desc = new()
            {
                NumParameters = (uint)parameters.Count,
                PParameters = (RootParameter*)ZenithMarshal.AllocateAndFill(scope, [.. parameters]),
                Flags = RootSignatureFlags.AllowInputAssemblerInputLayout
            };

            ComPtr<ID3D10Blob> blob = default;
            ComPtr<ID3D10Blob> error = default;
            context.D3D12.SerializeRootSignature(&desc, D3DRootSignatureVersion.Version1, ref blob, ref error).Success();
            context.Device.CreateRootSignature(0, blob.GetBufferPointer(), blob.GetBufferSize(), out ComPtr<ID3D12RootSignature> rootSignature).Success();
            blob.Dispose();
            error.Dispose();

            return rootSignature;
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
