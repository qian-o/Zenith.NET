using NGX.NET;

namespace Zenith.NET.Extensions.Reconstruction.DLSS;

internal unsafe class DLSSFeature(DLSSContext context) : DisposableObject
{
    public readonly DLSSContext Context = context;

    public readonly NGXParameter Parameters = context.AllocateParameters();

    public NGXHandle Handle;

    public void SetUI(ReadOnlySpan<byte> name, uint value)
    {
        fixed (byte* pointer = name)
        {
            Ngx.Parameter.SetUI(Parameters, pointer, value);
        }
    }

    protected override void Destroy()
    {
        Context.ReleaseFeature(Handle);
        Context.DestroyParameters(Parameters);
    }
}
