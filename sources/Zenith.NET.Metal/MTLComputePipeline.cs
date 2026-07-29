using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLComputePipeline : ComputePipeline
{
    public MTLComputePipelineState ComputePipelineState;

    public MTLComputePipeline(MTLGraphicsContext context, ComputePipelineDesc desc) : base(context, desc)
    {
        MTL4ComputePipelineDescriptor descriptor = new()
        {
            ComputeFunctionDescriptor = desc.ComputeShader.Metal().Descriptor,
            RequiredThreadsPerThreadgroup = new(desc.ComputeShader.Desc.ThreadGroupSize.X, desc.ComputeShader.Desc.ThreadGroupSize.Y, desc.ComputeShader.Desc.ThreadGroupSize.Z)
        };

        ComputePipelineState = context.Compiler.MakeComputePipelineState(descriptor, MTL4CompilerTaskOptions.Null, out NSError error);
        error.Success();
    }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        ComputePipelineState.Dispose();
    }
}
