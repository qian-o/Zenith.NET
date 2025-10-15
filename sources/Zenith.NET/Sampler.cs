namespace Zenith.NET;

public abstract class Sampler(GraphicsContext context, SamplerDesc desc) : GraphicsResource(context), IBindableResource
{
    private SamplerDesc desc = desc;

    public ref readonly SamplerDesc Desc => ref desc;
}
