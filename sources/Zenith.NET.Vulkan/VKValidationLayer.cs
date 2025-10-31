namespace Zenith.NET;

internal unsafe class VKValidationLayer : ValidationLayer
{
    public VKValidationLayer(GraphicsContext context) : base(context)
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
