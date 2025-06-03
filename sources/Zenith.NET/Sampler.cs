namespace Zenith.NET;

public abstract class Sampler(GraphicsContext context, SamplerDesc desc) : GraphicsResource(context)
{
    public SamplerDesc Desc { get; } = desc;
}
