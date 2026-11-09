using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal unsafe class DLSSFeature(DLSSVendor vendor) : DisposableObject
{
    public readonly DLSSVendor Vendor = vendor;

    public readonly NGXParameter Parameters = vendor.AllocateParameters();

    public NGXHandle Handle;

    public CommandQueue? Queue;

    public void SetUI(ReadOnlySpan<byte> name, uint value)
    {
        fixed (byte* pointer = name)
        {
            Ngx.Parameter.SetUI(Parameters, pointer, value);
        }
    }

    protected override void Destroy()
    {
        Queue?.Timeline.Signal().Wait();

        if (!Handle.IsNull)
        {
            Vendor.ReleaseFeature(Handle);
        }

        if (!Parameters.IsNull)
        {
            Vendor.DestroyParameters(Parameters);
        }
    }
}
