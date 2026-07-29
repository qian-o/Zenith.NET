using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLShader : Shader
{
    public MTLLibrary Library;

    public MTL4LibraryFunctionDescriptor Descriptor;

    public MTLShader(MTLGraphicsContext context, ShaderDesc desc) : base(context, desc)
    {
        Library = context.Device.MakeLibrary(DispatchData.Create(desc.CodeBytes), out NSError error);
        error.Success();

        Descriptor = new()
        {
            Name = desc.Name,
            Library = Library
        };
    }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
        Library.Label = name;
    }

    protected override void Destroy()
    {
        Descriptor.Dispose();
        Library.Dispose();
    }
}
