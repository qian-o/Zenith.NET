using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLShader : Shader
{
    public MTLLibrary Library;

    public MTLShader(MTLGraphicsContext context, ShaderDesc desc) : base(context, desc)
    {
        Library = context.Device.NewLibrary(DispatchData.Create(desc.ShaderBytes), out NSError error);
        error.Success();
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
