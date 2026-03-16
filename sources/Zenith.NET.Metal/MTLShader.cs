using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLShader : Shader
{
    public MTLLibrary Library;

    public MTLFunction Function;

    public MTLShader(MTLGraphicsContext context, ShaderDesc desc) : base(context, desc)
    {
        Library = context.Device.NewLibrary(DispatchData.Create(desc.ShaderBytes), out NSError error);
        error.Success();

        Function = Library.NewFunction(desc.EntryPoint);
    }

    protected override void SetResourceName(string name)
    {
        Function.Label = name;
    }

    protected override void Destroy()
    {
        Function.Dispose();
        Library.Dispose();
    }
}
