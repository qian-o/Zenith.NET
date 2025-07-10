namespace Zenith.NET;

public abstract class BufferView(GraphicsContext context, BufferViewDesc desc) : GraphicsResource(context), IBindableResource
{
    private BufferViewDesc desc = desc;

    public ref readonly BufferViewDesc Desc => ref desc;
}
