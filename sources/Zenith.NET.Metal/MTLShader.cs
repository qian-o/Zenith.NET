using Metal.NET;

namespace Zenith.NET.Metal;

internal class MTLShader : Shader
{
    public MTLLibrary Library;

    public MTL4LibraryFunctionDescriptor Descriptor;

    public MTLShader(MTLGraphicsContext context, ShaderDesc desc) : base(context, desc)
    {
        using DispatchData data = DispatchData.Create(desc.CodeBytes);

        Library = context.Device.MakeLibrary(data, out NSError error);
        error.Success();

        using NSString @string = new(desc.Name);

        Descriptor = new()
        {
            Name = @string,
            Library = Library
        };
    }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
        using NSString @string = new(name);

        Library.Label = @string;
    }

    protected override void Destroy()
    {
        Descriptor.Dispose();
        Library.Dispose();
    }
}
