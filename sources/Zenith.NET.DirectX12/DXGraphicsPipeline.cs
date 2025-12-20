using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Zenith.NET.DirectX12;

internal unsafe class DXGraphicsPipeline : GraphicsPipeline
{
    public ComPtr<ID3D12RootSignature> RootSignature;

    public ComPtr<ID3D12PipelineState> PipelineState;

    public DXGraphicsPipeline(DXGraphicsContext context, GraphicsPipelineDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        GraphicsPipelineStateDesc graphicsPipelineStateDesc = new()
        {
            SampleMask = uint.MaxValue
        };

        // RenderStates - Output
        {
            BlendStateRenderTarget[] blendStateRenderTargets =
            [
                desc.RenderStates.BlendState.RenderTarget0,
                desc.RenderStates.BlendState.RenderTarget1,
                desc.RenderStates.BlendState.RenderTarget2,
                desc.RenderStates.BlendState.RenderTarget3,
                desc.RenderStates.BlendState.RenderTarget4,
                desc.RenderStates.BlendState.RenderTarget5,
                desc.RenderStates.BlendState.RenderTarget6,
                desc.RenderStates.BlendState.RenderTarget7
            ];

            graphicsPipelineStateDesc.RasterizerState = new()
            {
                FillMode = DXFormats.DirectX12(desc.RenderStates.RasterizerState.FillMode),
                CullMode = DXFormats.DirectX12(desc.RenderStates.RasterizerState.CullMode),
                FrontCounterClockwise = desc.RenderStates.RasterizerState.FrontFace is FrontFace.CounterClockwise,
                DepthBias = desc.RenderStates.RasterizerState.DepthBias,
                DepthBiasClamp = desc.RenderStates.RasterizerState.DepthBiasClamp,
                SlopeScaledDepthBias = desc.RenderStates.RasterizerState.SlopeScaledDepthBias,
                DepthClipEnable = desc.RenderStates.RasterizerState.DepthClipEnable,
                MultisampleEnable = desc.Output.SampleCount is not SampleCount.Count1,
                AntialiasedLineEnable = true
            };

            graphicsPipelineStateDesc.DepthStencilState = new()
            {
                DepthEnable = desc.RenderStates.DepthStencilState.DepthEnable,
                DepthWriteMask = desc.RenderStates.DepthStencilState.DepthWriteEnable ? DepthWriteMask.All : DepthWriteMask.Zero,
                DepthFunc = DXFormats.DirectX12(desc.RenderStates.DepthStencilState.DepthFunc),
                StencilEnable = desc.RenderStates.DepthStencilState.StencilEnable,
                StencilReadMask = desc.RenderStates.DepthStencilState.StencilReadMask,
                StencilWriteMask = desc.RenderStates.DepthStencilState.StencilWriteMask,
                FrontFace = new()
                {
                    StencilFailOp = DXFormats.DirectX12(desc.RenderStates.DepthStencilState.FrontFace.StencilFailOp),
                    StencilDepthFailOp = DXFormats.DirectX12(desc.RenderStates.DepthStencilState.FrontFace.StencilDepthFailOp),
                    StencilPassOp = DXFormats.DirectX12(desc.RenderStates.DepthStencilState.FrontFace.StencilPassOp),
                    StencilFunc = DXFormats.DirectX12(desc.RenderStates.DepthStencilState.FrontFace.StencilFunc)
                },
                BackFace = new()
                {
                    StencilFailOp = DXFormats.DirectX12(desc.RenderStates.DepthStencilState.BackFace.StencilFailOp),
                    StencilDepthFailOp = DXFormats.DirectX12(desc.RenderStates.DepthStencilState.BackFace.StencilDepthFailOp),
                    StencilPassOp = DXFormats.DirectX12(desc.RenderStates.DepthStencilState.BackFace.StencilPassOp),
                    StencilFunc = DXFormats.DirectX12(desc.RenderStates.DepthStencilState.BackFace.StencilFunc)
                }
            };

            graphicsPipelineStateDesc.BlendState = new()
            {
                AlphaToCoverageEnable = desc.RenderStates.BlendState.AlphaToCoverageEnable,
                IndependentBlendEnable = desc.RenderStates.BlendState.IndependentBlendEnable
            };

            for (int i = 0; i < blendStateRenderTargets.Length; i++)
            {
                graphicsPipelineStateDesc.BlendState.RenderTarget[i] = new()
                {
                    BlendEnable = blendStateRenderTargets[i].BlendEnable,
                    SrcBlend = DXFormats.DirectX12(blendStateRenderTargets[i].SrcBlend),
                    DestBlend = DXFormats.DirectX12(blendStateRenderTargets[i].DestBlend),
                    BlendOp = DXFormats.DirectX12(blendStateRenderTargets[i].BlendOp),
                    SrcBlendAlpha = DXFormats.DirectX12(blendStateRenderTargets[i].SrcBlendAlpha),
                    DestBlendAlpha = DXFormats.DirectX12(blendStateRenderTargets[i].DestBlendAlpha),
                    BlendOpAlpha = DXFormats.DirectX12(blendStateRenderTargets[i].BlendOpAlpha),
                    RenderTargetWriteMask = (byte)DXFormats.DirectX12(blendStateRenderTargets[i].Flags)
                };
            }

            graphicsPipelineStateDesc.NumRenderTargets = (uint)desc.Output.ColorAttachments.Length;

            for (int i = 0; i < desc.Output.ColorAttachments.Length; i++)
            {
                graphicsPipelineStateDesc.RTVFormats[i] = DXFormats.DirectX12(desc.Output.ColorAttachments[i]);
            }

            graphicsPipelineStateDesc.DSVFormat = desc.Output.DepthStencilAttachment.HasValue ? DXFormats.DirectX12(desc.Output.DepthStencilAttachment.Value) : Format.FormatUnknown;

            graphicsPipelineStateDesc.SampleDesc = DXFormats.DirectX12(desc.Output.SampleCount);
        }

        // Vertex - Hull - Domain - Geometry - Pixel
        {
            graphicsPipelineStateDesc.VS = desc.Vertex.DirectX12().GetShaderBytecode(scope);

            if (desc.Hull is not null)
            {
                graphicsPipelineStateDesc.HS = desc.Hull.DirectX12().GetShaderBytecode(scope);
            }

            if (desc.Domain is not null)
            {
                graphicsPipelineStateDesc.DS = desc.Domain.DirectX12().GetShaderBytecode(scope);
            }

            if (desc.Geometry is not null)
            {
                graphicsPipelineStateDesc.GS = desc.Geometry.DirectX12().GetShaderBytecode(scope);
            }

            graphicsPipelineStateDesc.PS = desc.Pixel.DirectX12().GetShaderBytecode(scope);
        }

        // ResourceLayouts
        {
            List<RootParameter> parameters = [];
            for (int i = 0; i < desc.ResourceLayouts.Length; i++)
            {
                DXResourceLayout resourceLayout = desc.ResourceLayouts[i].DirectX12();

                foreach (ShaderStageFlags stage in ZenithHelper.GraphicShaderStages())
                {
                    if (resourceLayout.DescriptorRanges(stage, (uint)i, out DescriptorRange[] cbvSrvUavRanges, out DescriptorRange[] samplerRanges))
                    {
                        if (cbvSrvUavRanges.Length > 0)
                        {
                            parameters.Add(new()
                            {
                                ParameterType = RootParameterType.TypeDescriptorTable,
                                ShaderVisibility = DXFormats.DirectX12(stage),
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
                                ShaderVisibility = DXFormats.DirectX12(stage),
                                DescriptorTable = new()
                                {
                                    NumDescriptorRanges = (uint)samplerRanges.Length,
                                    PDescriptorRanges = (DescriptorRange*)ZenithMarshal.AllocateAndFill(scope, samplerRanges)
                                }
                            });
                        }
                    }
                }
            }

            RootSignatureDesc rootSignatureDesc = new()
            {
                NumParameters = (uint)parameters.Count,
                PParameters = (RootParameter*)ZenithMarshal.AllocateAndFill(scope, [.. parameters]),
                Flags = RootSignatureFlags.AllowInputAssemblerInputLayout
            };

            ComPtr<ID3D10Blob> blob = default;
            ComPtr<ID3D10Blob> error = default;
            context.D3D12.SerializeRootSignature(&rootSignatureDesc, D3DRootSignatureVersion.Version1, ref blob, ref error).Success();
            context.Device.CreateRootSignature(0, blob.GetBufferPointer(), blob.GetBufferSize(), out RootSignature).Success();
            blob.Dispose();
            error.Dispose();

            graphicsPipelineStateDesc.PRootSignature = RootSignature;
        }

        // InputLayouts
        {
            List<InputElementDesc> inputElementDescs = [];
            for (int i = 0; i < desc.InputLayouts.Length; i++)
            {
                InputLayout inputLayout = desc.InputLayouts[i];

                foreach (InputElement element in inputLayout.Elements)
                {
                    inputElementDescs.Add(new()
                    {
                        SemanticName = (byte*)ZenithMarshal.StringToPointer(scope, element.Semantic.ToString().ToUpper(), StringEncoding.UTF8),
                        SemanticIndex =element.Index,
                        Format = DXFormats.DirectX12(element.Format),
                        InputSlot = (uint)i,
                        AlignedByteOffset = element.OffsetInBytes
                    });
                }
            }

            graphicsPipelineStateDesc.InputLayout = new()
            {
                PInputElementDescs = (InputElementDesc*)ZenithMarshal.AllocateAndFill(scope, [.. inputElementDescs]),
                NumElements = (uint)inputElementDescs.Count
            };
        }

        // PrimitiveTopology
        {
            graphicsPipelineStateDesc.PrimitiveTopologyType = DXFormats.DirectX12(desc.PrimitiveTopology);
        }

        context.Device.CreateGraphicsPipelineState(&graphicsPipelineStateDesc, out PipelineState).Success();
    }

    protected override void SetResourceName(string name)
    {
        PipelineState.SetName(name);
    }

    protected override void Destroy()
    {
        RootSignature.Dispose();
        PipelineState.Dispose();
    }
}
