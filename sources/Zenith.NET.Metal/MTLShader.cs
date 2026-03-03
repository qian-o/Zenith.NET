using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLShader : Shader
{
    public MTLLibrary Library;

    public MTLShader(MTLGraphicsContext context, ShaderDesc desc) : base(context, desc)
    {
        using DispatchData dispatchData = DispatchData.Create(desc.ShaderBytes, default, 0);

        Library = context.Device.NewLibrary(dispatchData, out NSError error);
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
