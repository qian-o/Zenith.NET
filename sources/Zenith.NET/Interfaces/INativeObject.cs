namespace Zenith.NET;

public interface INativeObject
{
    nint GetNativeObject(NativeObjectType type);
}
