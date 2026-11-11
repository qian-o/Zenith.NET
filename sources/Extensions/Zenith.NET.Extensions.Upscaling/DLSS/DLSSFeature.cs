using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal unsafe class DLSSFeature(DLSSVendor vendor) : DisposableObject
{
    public readonly NGXParameter Parameters = vendor.AllocateParameters();

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
        if (!Handle.IsNull)
        {
            vendor.ReleaseFeature(Handle);
        }

        if (!Parameters.IsNull)
        {
            vendor.DestroyParameters(Parameters);
        }
    }
}
