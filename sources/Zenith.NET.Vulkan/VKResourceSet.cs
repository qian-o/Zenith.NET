namespace Zenith.NET;

internal unsafe class VKResourceSet : ResourceSet
{
    public VKResourceSet(VKGraphicsContext context, ResourceSetDesc desc) : base(context, desc)
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
