using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKValidationLayer : ValidationLayer
{
    private readonly DebugUtilsMessengerEXT messenger;

    public VKValidationLayer(GraphicsContext context) : base(context)
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
            PfnUserCallback = new(UserCallback)
        };

        Context.DebugUtils?.CreateDebugUtilsMessenger(Context.Instance, &createInfo, null, (DebugUtilsMessengerEXT*)Unsafe.AsPointer(ref messenger)).Success();
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        Context.DebugUtils?.DestroyDebugUtilsMessenger(Context.Instance, messenger, null);
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
            _ => MessageSeverity.Message
        }, ZenithMarshal.StringFromPointer((nint)pCallbackData->PMessage, StringEncoding.UTF8));

        return Vk.False;
    }
}
