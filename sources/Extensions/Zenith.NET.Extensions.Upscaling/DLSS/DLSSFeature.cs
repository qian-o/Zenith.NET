using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal unsafe class DLSSFeature(DLSSProvider provider) : DisposableObject
{
    public readonly DLSSProvider Provider = provider;

    public readonly NGXParameter Parameters = provider.AllocateParameters();

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
        Provider.ReleaseFeature(Handle);
        Provider.DestroyParameters(Parameters);
    }
}
