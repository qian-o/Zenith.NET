namespace Zenith.NET;

public abstract class ValidationLayer(GraphicsContext context) : GraphicsResource(context)
{
    public event EventHandler<ValidationMessageArgs>? ValidationMessage;

    protected void OnValidationMessage(ValidationMessageArgs args)
    {
        ValidationMessage?.Invoke(this, args);
    }

    internal void ValidateDesc(SwapChainDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(FrameBufferDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(ShaderDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(BufferDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(BufferViewDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(TextureDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(TextureViewDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(SamplerDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(ResourceLayoutDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(ResourceSetDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(GraphicsPipelineDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(ComputePipelineDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(RayTracingPipelineDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(MeshShadingPipelineDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(BottomLevelAccelerationStructureDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(TopLevelAccelerationStructureDesc desc)
    {
        throw new NotImplementedException();
    }

    internal void ValidateDesc(TopLevelAccelerationStructureDesc oldDesc, TopLevelAccelerationStructureDesc newDesc)
    {
        throw new NotImplementedException();
    }

    //private void OnValidationMessage(MessageSeverity severity, string message)
    //{
    //    OnValidationMessage(new(MessageSource.Framework, severity, message));
    //}
}
