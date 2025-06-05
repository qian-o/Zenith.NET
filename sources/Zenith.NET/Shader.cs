namespace Zenith.NET;

public abstract class Shader(GraphicsContext context, ShaderDesc desc) : GraphicsResource(context)
{
    public ShaderDesc Desc { get; } = desc;
}
