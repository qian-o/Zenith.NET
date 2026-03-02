using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLShader : Shader
{
    public MTLLibrary Library;

    public MTLShader(MTLGraphicsContext context, ShaderDesc desc) : base(context, desc)
    {
        throw new NotImplementedException();
    }

    protected override void SetResourceName(string name)
    {
        Library.Label = name;
    }

    protected override void Destroy()
    {
        Library.Dispose();
    }
}
