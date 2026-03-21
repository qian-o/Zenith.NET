using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLComputePipeline : ComputePipeline
{
    public MTLComputePipelineState ComputePipelineState;

    public MTLComputePipeline(MTLGraphicsContext context, ComputePipelineDesc desc) : base(context, desc)
    {
        MTL4ComputePipelineDescriptor descriptor = new()
        {
            ComputeFunctionDescriptor = new MTL4LibraryFunctionDescriptor()
            {
                Name = desc.Compute.Desc.EntryPoint,
                Library = desc.Compute.Metal().Library
            },
            RequiredThreadsPerThreadgroup = new(desc.ThreadGroupSizeX, desc.ThreadGroupSizeY, desc.ThreadGroupSizeZ)
        };

        ComputePipelineState = context.Compiler.MakeComputePipelineState(descriptor, MTL4CompilerTaskOptions.Null, out NSError error);
        error.Success();
    }

    public void Bind(MTL4ComputeCommandEncoder commandEncoder)
    {
        commandEncoder.SetComputePipelineState(ComputePipelineState);
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        ComputePipelineState.Dispose();
    }
}
