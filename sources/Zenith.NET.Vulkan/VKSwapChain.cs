using System.Diagnostics;
using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKSwapChain : SwapChain
{
    private readonly VKTexture[] textures = [];

    public SurfaceKHR Surface;

    public SwapchainKHR Swapchain;

    private uint index;

    public VKSwapChain(VKGraphicsContext context, SwapChainDesc desc) : base(context, desc)
    {
        CreateSwapChain();
        CreateTextures();
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public override Texture Drawable => textures[index];

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    protected override void PresentImpl()
    {
    }

    protected override void ResizeImpl()
    {
        DestroyTextures();
        DestroySwapChain();

        CreateSwapChain();
        CreateTextures();
    }

    protected override void RefreshImpl()
    {
        DestroyTextures();
        DestroySwapChain();

        CreateSwapChain();
        CreateTextures();
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        DestroyTextures();
        DestroySwapChain();
    }

    private void CreateSwapChain()
    {
        switch (Desc.Surface.Type)
        {
            case SurfaceType.Win32:
                {
                    Win32SurfaceCreateInfoKHR createInfo = new()
                    {
                        SType = StructureType.Win32SurfaceCreateInfoKhr,
                        Hinstance = Process.GetCurrentProcess().Handle,
                        Hwnd = Desc.Surface.Handles[0]
                    };

                    Context.Win32Surface?.CreateWin32Surface(Context.Instance, &createInfo, default, out Surface).Success();
                }
                break;

            case SurfaceType.Wayland:
                {
                    WaylandSurfaceCreateInfoKHR createInfo = new()
                    {
                        SType = StructureType.WaylandSurfaceCreateInfoKhr,
                        Display = (nint*)Desc.Surface.Handles[0],
                        Surface = (nint*)Desc.Surface.Handles[1]
                    };

                    Context.WaylandSurface?.CreateWaylandSurface(Context.Instance, &createInfo, default, out Surface).Success();
                }
                break;

            case SurfaceType.Xlib:
                break;

            case SurfaceType.Android:
                break;

            case SurfaceType.Apple:
                break;
        }
    }

    private void DestroySwapChain()
    {
    }

    private void CreateTextures()
    {
    }

    private void DestroyTextures()
    {
    }
}
