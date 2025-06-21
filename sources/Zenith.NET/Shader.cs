namespace Zenith.NET;

public abstract class Shader(GraphicsContext context, ShaderDesc desc) : GraphicsResource(context)
{
    private ShaderDesc desc = desc;

    public ref readonly ShaderDesc Desc => ref desc;
}
