using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLComputePipeline : ComputePipeline
{
    public MTLComputePipelineState ComputePipelineState;

    public MTLComputePipeline(MTLGraphicsContext context, ComputePipelineDesc desc) : base(context, desc)
    {
        MTL4ComputePipelineDescriptor descriptor = new()
        {
            ComputeFunctionDescriptor = desc.Compute.Metal().Descriptor,
            RequiredThreadsPerThreadgroup = new(desc.ThreadGroupSizeX, desc.ThreadGroupSizeY, desc.ThreadGroupSizeZ)
        };

        ComputePipelineState = context.Compiler.MakeComputePipelineState(descriptor, MTL4CompilerTaskOptions.Null, out NSError error);
        error.Success();
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        ComputePipelineState.Dispose();
    }
}
