using NGX.NET;

namespace Zenith.NET.Extensions.Reconstruction.DLSS;

internal unsafe class DLSSFeature(DLSSBackend backend) : DisposableObject
{
    public readonly DLSSBackend Backend = backend;

    public readonly NGXParameter Parameters = backend.AllocateParameters();

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
        Backend.ReleaseFeature(Handle);
        Backend.DestroyParameters(Parameters);
    }
}
