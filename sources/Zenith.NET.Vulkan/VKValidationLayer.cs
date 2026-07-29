using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKValidationLayer : ValidationLayer
{
    private readonly PfnDebugUtilsMessengerCallbackEXT callback;
    private readonly DebugUtilsMessengerEXT messenger;

    public VKValidationLayer(VKGraphicsContext context) : base(context)
    {
        DebugUtilsMessengerCreateInfoEXT createInfo = new()
        {
            SType = StructureType.DebugUtilsMessengerCreateInfoExt,
            MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt
                              | DebugUtilsMessageSeverityFlagsEXT.InfoBitExt
                              | DebugUtilsMessageSeverityFlagsEXT.WarningBitExt
                              | DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt,
            MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt
                          | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt
                          | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt,
            PfnUserCallback = callback = new(Callback)
        };

        context.DebugUtils?.CreateDebugUtilsMessenger(context.Instance, &createInfo, default, out messenger).Success();
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        Context.DebugUtils?.DestroyDebugUtilsMessenger(Context.Instance, messenger, default);

        callback.Dispose();
    }

    private uint Callback(DebugUtilsMessageSeverityFlagsEXT severity, DebugUtilsMessageTypeFlagsEXT types, DebugUtilsMessengerCallbackDataEXT* callbackData, void* userData)
    {
        Report(severity switch
        {
            DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt => MessageSeverity.Error,
            DebugUtilsMessageSeverityFlagsEXT.WarningBitExt => MessageSeverity.Warning,
            _ => MessageSeverity.Info
        }, ZenithMarshal.StringFromPointer((nint)callbackData->PMessage, StringEncoding.UTF8));

        return Vk.False;
    }
}
