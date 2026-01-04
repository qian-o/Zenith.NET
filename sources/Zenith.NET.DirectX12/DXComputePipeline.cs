using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXComputePipeline : ComputePipeline
{
    public ComPtr<ID3D12RootSignature> RootSignature;

    public ComPtr<ID3D12PipelineState> PipelineState;

    public DXComputePipeline(DXGraphicsContext context, ComputePipelineDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        ComputePipelineStateDesc computePipelineStateDesc = new()
        {
            CS = desc.Compute.DirectX12().GetShaderBytecode(scope)
        };

        // ResourceLayouts
        {
            List<RootParameter> parameters = [];
            for (int i = 0; i < desc.ResourceLayouts.Length; i++)
            {
                if (desc.ResourceLayouts[i].DirectX12().DescriptorRanges(ShaderStageFlags.None, (uint)i, out DescriptorRange[] cbvSrvUavRanges, out DescriptorRange[] samplerRanges))
                {
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

            computePipelineStateDesc.PRootSignature = RootSignature;
        }

        context.Device.CreateComputePipelineState(&computePipelineStateDesc, out PipelineState).Success();
    }

    protected override void SetResourceName(string name)
    {
        PipelineState.SetName(name).Success();
    }

    protected override void Destroy()
    {
        PipelineState.Dispose();
        RootSignature.Dispose();
    }
}
