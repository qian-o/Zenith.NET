using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXValidationLayer : ValidationLayer
{
    private readonly PfnMessageFunc callback;
    private readonly uint callbackCookie;

    public DXValidationLayer(DXGraphicsContext context) : base(context)
    {
        context.InfoQueue1?.RegisterMessageCallback(callback = new(Callback), MessageCallbackFlags.FlagNone, null, ref callbackCookie).Success();
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        Context.InfoQueue1?.UnregisterMessageCallback(callbackCookie).Success();

        callback.Dispose();
    }

    private void Callback(MessageCategory category, DxMessageSeverity severity, MessageID messageID, byte* pDescription, void* context)
    {
        Report(MessageSource.GraphicsApi, severity switch
        {
            DxMessageSeverity.Error => MessageSeverity.Error,
            DxMessageSeverity.Warning => MessageSeverity.Warning,
            _ => MessageSeverity.Info
        }, ZenithMarshal.StringFromPointer((nint)pDescription, StringEncoding.UTF8));
    }
}
