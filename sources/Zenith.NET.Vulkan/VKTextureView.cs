namespace Zenith.NET;

internal unsafe class VKTextureView : TextureView
{
    public VKTextureView(GraphicsContext context, TextureViewDesc desc) : base(context, desc)
    {
    }

    protected override void SetResourceName(string name)
    {
        throw new NotImplementedException();
    }

    protected override void Destroy()
    {
        throw new NotImplementedException();
    }
}
