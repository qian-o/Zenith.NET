using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;
using Silk.NET.DXGI;

namespace Zenith.NET.DirectX12;

internal unsafe class DXSwapChainFrameBuffer(DXGraphicsContext context, DXSwapChain swapChain) : GraphicsResource(context)
{
    private DXTexture? depthStencilTarget;
    private DXTexture[] colorTargets = [];
    private DXFrameBuffer[] frameBuffers = [];

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public DXFrameBuffer this[uint index] => throw new NotImplementedException();

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
                MipLevels = 1,
                ArrayLayers = 1,
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
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Flags = TextureUsageFlags.RenderTarget
        };

        if (swapChain.Desc.Surface.Type is not SurfaceType.D3D11Interop)
        {
            using ZenithMarshal.Scope scope = new();

            colorTargets = new DXTexture[DXGraphicsContext.SwapChainBufferCount];
            frameBuffers = new DXFrameBuffer[DXGraphicsContext.SwapChainBufferCount];

            for (uint i = 0; i < DXGraphicsContext.SwapChainBufferCount; i++)
            {
                frameBuffers[i] = new(context, new()
                {
                    ColorAttachments = [new() { Target = colorTargets[i] = new(context, colorTargetDesc, swapChain.SwapChain3.GetBuffer<ID3D12Resource>(i)) }],
                    DepthStencilAttachment = depthStencilTarget is not null ? new() { Target = depthStencilTarget } : null
                });
            }
        }
        else if (swapChain.Desc.Surface.Type is SurfaceType.D3D11Interop)
        {
            colorTargets = new DXTexture[1];
            frameBuffers = new DXFrameBuffer[1];

            Context.Device.OpenSharedHandle((void*)handles[0], out ComPtr<ID3D12Resource> resource).Success();

            frameBuffers[0] = new(context, new()
            {
                ColorAttachments = [new() { Target = colorTargets[0] = new(context, colorTargetDesc, resource) }],
                DepthStencilAttachment = depthStencilTarget is not null ? new() { Target = depthStencilTarget } : null
            });
        }
    }

    public void DestroyFrameBuffers()
    {
        foreach (DXFrameBuffer frameBuffer in frameBuffers)
        {
            frameBuffer.Dispose();
        }
        frameBuffers = [];

        foreach (DXTexture texture in colorTargets)
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
