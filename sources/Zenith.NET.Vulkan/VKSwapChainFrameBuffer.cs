using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKSwapChainFrameBuffer(VKGraphicsContext context, VKSwapChain swapChain) : GraphicsResource(context)
{
    private VKTexture? depthStencilTarget;
    private VKTexture[] colorTargets = [];
    private VKFrameBuffer[] frameBuffers = [];

    public VKFrameBuffer this[uint index] => frameBuffers[index];

    public void CreateFrameBuffers(uint width, uint height, nint[] handles)
    {
        if (swapChain.Desc.DepthStencilTargetFormat is not null)
        {
            depthStencilTarget = new(context, new()
            {
                Type = TextureType.Texture2D,
                Format = swapChain.Desc.DepthStencilTargetFormat.Value,
                Width = width,
                Height = height,
                Depth = 1,
                Layers = 1,
                MipLevels = 1,
                SampleCount = SampleCount.Count1,
                Flags = TextureUsageFlags.DepthStencil
            });
        }

        TextureDesc colorTargetDesc = new()
        {
            Type = TextureType.Texture2D,
            Format = swapChain.Desc.ColorTargetFormat,
            Width = width,
            Height = height,
            Depth = 1,
            Layers = 1,
            MipLevels = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.RenderTarget
        };

        if (swapChain.Desc.Surface.Type is not SurfaceType.D3D11Interop)
        {
            using ZenithMarshal.Scope scope = new();

            uint swapchainImageCount = 0;
            context.Swapchain?.GetSwapchainImages(context.Device, swapChain.Swapchain, &swapchainImageCount, null).Success();

            Image* swapchainImages = (Image*)ZenithMarshal.Allocate<Image>(scope, swapchainImageCount);
            context.Swapchain?.GetSwapchainImages(context.Device, swapChain.Swapchain, &swapchainImageCount, swapchainImages).Success();

            colorTargets = new VKTexture[swapchainImageCount];
            frameBuffers = new VKFrameBuffer[swapchainImageCount];

            for (uint i = 0; i < swapchainImageCount; i++)
            {
                frameBuffers[i] = new(context, new()
                {
                    ColorAttachments = [new() { Target = colorTargets[i] = new(context, colorTargetDesc, swapchainImages[i]) }],
                    DepthStencilAttachment = depthStencilTarget is not null ? new() { Target = depthStencilTarget } : null
                });
            }
        }
        else if (swapChain.Desc.Surface.Type is SurfaceType.D3D11Interop)
        {
            colorTargets = new VKTexture[1];
            frameBuffers = new VKFrameBuffer[1];

            frameBuffers[0] = new(context, new()
            {
                ColorAttachments = [new() { Target = colorTargets[0] = new(context, colorTargetDesc, ExternalMemoryHandleTypeFlags.D3D11TextureKmtBit, handles[0]) }],
                DepthStencilAttachment = depthStencilTarget is not null ? new() { Target = depthStencilTarget } : null
            });
        }
    }

    public void DestroyFrameBuffers()
    {
        foreach (VKFrameBuffer frameBuffer in frameBuffers)
        {
            frameBuffer.Dispose();
        }
        frameBuffers = [];

        foreach (VKTexture texture in colorTargets)
        {
            texture.Dispose();
        }
        colorTargets = [];

        depthStencilTarget?.Dispose();
        depthStencilTarget = null;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        DestroyFrameBuffers();
    }
}
