using System.Runtime.InteropServices;

namespace CornellBox.Helpers;

internal static partial class CocoaHelper
{
    private const string LibObjC = "/usr/lib/libobjc.A.dylib";

    [LibraryImport(LibObjC, EntryPoint = "objc_getClass")]
    private static partial nint GetClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [LibraryImport(LibObjC, EntryPoint = "sel_registerName")]
    private static partial nint Selector([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector);

    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector, [MarshalAs(UnmanagedType.I1)] bool arg);

    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static partial nint Send(nint receiver, nint selector, nint arg);

    public static nint CreateLayer(nint cocoa)
    {
        nint layer = Send(GetClass("CAMetalLayer"), Selector("layer"));
        Send(layer, Selector("retain"));

        nint view = Send(cocoa, Selector("contentView"));
        Send(view, Selector("setWantsLayer:"), true);
        Send(view, Selector("setLayer:"), layer);

        return layer;
    }
}
