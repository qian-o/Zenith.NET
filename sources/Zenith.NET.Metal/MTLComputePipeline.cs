using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLComputePipeline : ComputePipeline
{
    public MTLComputePipelineState ComputePipelineState;

    public MTLComputePipeline(MTLGraphicsContext context, ComputePipelineDesc desc) : base(context, desc)
    {
        using MTL4ComputePipelineDescriptor descriptor = new()
        {
            ComputeFunctionDescriptor = desc.ComputeShader.Metal().Descriptor,
            RequiredThreadsPerThreadgroup = new(desc.ComputeShader.Desc.ThreadGroup.X, desc.ComputeShader.Desc.ThreadGroup.Y, desc.ComputeShader.Desc.ThreadGroup.Z)
        };

        ComputePipelineState = context.Compiler.MakeComputePipelineState(descriptor, default, out NSError error);
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
