using Metal.NET;

namespace Zenith.NET.Metal;

internal static class NSAutorelease
{
    public static T Own<T>(Func<T> func) where T : NSObject, INativeObject<T>
    {
        using NSAutoreleasePool _ = new();

        return func().Retain();
    }

    public static T Own<T1, T>(Func<T1, T> func, T1 arg1) where T : NSObject, INativeObject<T>
    {
        using NSAutoreleasePool _ = new();

        return func(arg1).Retain();
    }
}
