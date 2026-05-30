using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXValidationLayer : ValidationLayer
{
    private readonly PfnMessageFunc callback;
    private readonly uint callbackCookie;

    public ComPtr<ID3D12InfoQueue1> InfoQueue;

    public DXValidationLayer(DXGraphicsContext context) : base(context)
    {
        context.Device.QueryInterface(SilkMarshal.GuidPtrOf<ID3D12InfoQueue1>(), (void**)InfoQueue.GetAddressOf()).Success();

        InfoQueue.RegisterMessageCallback(callback = new(Callback), MessageCallbackFlags.FlagNone, default, ref callbackCookie).Success();
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
        InfoQueue.UnregisterMessageCallback(callbackCookie).Success();

        callback.Dispose();
    }

    private void Callback(MessageCategory category, DxMessageSeverity severity, MessageID messageID, byte* pDescription, void* context)
    {
        Report(severity switch
        {
            DxMessageSeverity.Error => MessageSeverity.Error,
            DxMessageSeverity.Warning => MessageSeverity.Warning,
            _ => MessageSeverity.Info
        }, ZenithMarshal.StringFromPointer((nint)pDescription, StringEncoding.UTF8));
    }
}
