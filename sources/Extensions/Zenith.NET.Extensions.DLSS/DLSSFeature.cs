using NGX.NET;

namespace Zenith.NET.Extensions.DLSS;

public abstract class DLSSFeature : DisposableObject
{
    internal readonly DLSSBackend Backend;

    internal readonly NGXParameter Parameters;

    internal NGXHandle Handle;

    internal DLSSFeature(DLSSBackend backend)
    {
        Backend = backend;
        Parameters = backend.AllocateParameters();
    }

    protected override void Destroy()
    {
        using Lock.Scope _ = DLSSBackend.Lock.EnterScope();

        Backend.ReleaseFeature(Handle);
        Backend.DestroyParameters(Parameters);
    }
}
