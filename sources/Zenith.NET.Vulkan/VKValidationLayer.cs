using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKValidationLayer : ValidationLayer
{
    private readonly PfnDebugUtilsMessengerCallbackEXT pfnUserCallback;
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
            PfnUserCallback = pfnUserCallback = new(UserCallback)
        };

        context.DebugUtils?.CreateDebugUtilsMessenger(context.Instance, &createInfo, null, out messenger).Success();
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        Context.DebugUtils?.DestroyDebugUtilsMessenger(Context.Instance, messenger, null);

        pfnUserCallback.Dispose();
    }

    private uint UserCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity,
                              DebugUtilsMessageTypeFlagsEXT messageTypes,
                              DebugUtilsMessengerCallbackDataEXT* pCallbackData,
                              void* pUserData)
    {
        Report(MessageSource.GraphicsAPI, messageSeverity switch
        {
            DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt => MessageSeverity.Error,
            DebugUtilsMessageSeverityFlagsEXT.WarningBitExt => MessageSeverity.Warning,
            _ => MessageSeverity.Info
        }, ZenithMarshal.StringFromPointer((nint)pCallbackData->PMessage, StringEncoding.UTF8));

        return Vk.False;
    }
}
