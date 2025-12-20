using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal class DXResourceSet : ResourceSet
{
    public DXResourceSet(GraphicsContext context, ResourceSetDesc desc) : base(context, desc)
    {
        uint resourceStartIndex = 0;
        List<DXTextureView> srvTextureViews = [];
        List<DXTextureView> uavTextureViews = [];

        for (int i = 0; i < desc.Layout.Desc.Bindings.Length; i++)
        {
            ResourceBinding binding = desc.Layout.Desc.Bindings[i];

            for (uint j = 0; j < binding.Count; j++)
            {
                IBindableResource resource = desc.Resources[(int)(resourceStartIndex + j)];

                if (binding.Type is ResourceType.Texture or ResourceType.TextureReadWrite)
                {
                    List<DXTextureView> views = binding.Type is ResourceType.Texture ? srvTextureViews : uavTextureViews;

                    if (resource is Texture texture)
                    {
                        views.Add(texture.DirectX12().View);
                    }
                    else if (resource is TextureView textureView)
                    {
                        views.Add(textureView.DirectX12());
                    }
                }
            }

            resourceStartIndex += binding.Count;
        }

        SrvTextureViews = [.. srvTextureViews];
        UavTextureViews = [.. uavTextureViews];
    }

    public DXTextureView[] SrvTextureViews { get; }

    public DXTextureView[] UavTextureViews { get; }

    public void TransitionStates(CommandBuffer commandBuffer)
    {
        foreach (DXTextureView textureView in SrvTextureViews)
        {
            textureView.TransitionStates(commandBuffer, ResourceStates.AllShaderResource);
        }

        foreach (DXTextureView textureView in UavTextureViews)
        {
            textureView.TransitionStates(commandBuffer, ResourceStates.UnorderedAccess);
        }
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
    }
}
