namespace Zenith.NET.DirectX12;

internal class DXBufferView : BufferView
{
    public DXBufferView(DXGraphicsContext context, BufferViewDesc desc) : base(context, desc)
    {
    }

    public override ResourceHandle UniformHandle { get; }

    public override ResourceHandle StorageReadOnlyHandle { get; }

    public override ResourceHandle StorageReadWriteHandle { get; }

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
    }
}
