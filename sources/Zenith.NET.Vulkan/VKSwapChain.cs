using System.Diagnostics;
using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKSwapChain : SwapChain
{
    private readonly VKFence fence;
    private readonly VKSwapChainFrameBuffer swapChainFrameBuffer;

    public SurfaceKHR Surface;

    public SwapchainKHR Swapchain;

    public uint Index;

    public VKSwapChain(VKGraphicsContext context, SwapChainDesc desc) : base(context, desc)
    {
        fence = new(context);
        swapChainFrameBuffer = new(context, this);

        CreateSurface();
        CreateSwapChain();
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public override FrameBuffer FrameBuffer => swapChainFrameBuffer[Index];

    public override void Present()
    {
        throw new NotImplementedException();
    }

    protected override void ResizeImpl()
    {
        CreateSwapChain();
    }

    protected override void RefreshImpl()
    {
        CreateSurface();
        CreateSwapChain();
    }

    protected override void SetResourceName(string name)
    {
        throw new NotImplementedException();
    }

    protected override void Destroy()
    {
        throw new NotImplementedException();
    }

    private void CreateSurface()
    {
        DestroySurface();

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

                    Context.Win32Surface?.CreateWin32Surface(Context.Instance, &createInfo, null, (SurfaceKHR*)Unsafe.AsPointer(ref Surface)).Success();
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

                    Context.WaylandSurface?.CreateWaylandSurface(Context.Instance, &createInfo, null, (SurfaceKHR*)Unsafe.AsPointer(ref Surface)).Success();
                }
                break;

            case SurfaceType.Xlib:
                {
                    XlibSurfaceCreateInfoKHR createInfo = new()
                    {
                        SType = StructureType.XlibSurfaceCreateInfoKhr,
                        Dpy = (nint*)Desc.Surface.Handles[0],
                        Window = Desc.Surface.Handles[1]
                    };

                    Context.XlibSurface?.CreateXlibSurface(Context.Instance, &createInfo, null, (SurfaceKHR*)Unsafe.AsPointer(ref Surface)).Success();
                }
                break;

            case SurfaceType.Android:
                {
                    AndroidSurfaceCreateInfoKHR createInfo = new()
                    {
                        SType = StructureType.AndroidSurfaceCreateInfoKhr,
                        Window = (nint*)Desc.Surface.Handles[0]
                    };

                    Context.AndroidSurface?.CreateAndroidSurface(Context.Instance, &createInfo, null, (SurfaceKHR*)Unsafe.AsPointer(ref Surface)).Success();
                }
                break;

            case SurfaceType.Apple:
                {
                    MetalSurfaceCreateInfoEXT createInfo = new()
                    {
                        SType = StructureType.MetalSurfaceCreateInfoExt,
                        PLayer = (nint*)Desc.Surface.Handles[0]
                    };

                    Context.MetalSurface?.CreateMetalSurface(Context.Instance, &createInfo, null, (SurfaceKHR*)Unsafe.AsPointer(ref Surface)).Success();
                }
                break;
        }
    }

    private void DestroySurface()
    {
        if (Surface.Handle is not 0)
        {
            Context.Surface?.DestroySurface(Context.Instance, Surface, null);

            Surface = default;
        }
    }

    private void CreateSwapChain()
    {
        DestroySwapChain();

        if (Desc.Surface.Type is not SurfaceType.D3D11Interop)
        {
            using ZenithMarshal.Scope scope = new();

            uint* queueFamilyIndices = (uint*)ZenithMarshal.Allocate<uint>(scope, (uint)Context.QueueFamilyIndices.Length);
            Context.QueueFamilyIndices.CopyTo(new Span<uint>(queueFamilyIndices, Context.QueueFamilyIndices.Length));

            SurfaceCapabilitiesKHR capabilities = default;
            Context.Surface?.GetPhysicalDeviceSurfaceCapabilities(Context.PhysicalDevice, Surface, &capabilities).Success();

            uint surfaceFormatCount = 0;
            Context.Surface?.GetPhysicalDeviceSurfaceFormats(Context.PhysicalDevice, Surface, &surfaceFormatCount, null).Success();

            SurfaceFormatKHR* surfaceFormats = (SurfaceFormatKHR*)ZenithMarshal.Allocate<SurfaceFormatKHR>(scope, surfaceFormatCount);
            Context.Surface?.GetPhysicalDeviceSurfaceFormats(Context.PhysicalDevice, Surface, &surfaceFormatCount, surfaceFormats).Success();

            uint presentModeCount = 0;
            Context.Surface?.GetPhysicalDeviceSurfacePresentModes(Context.PhysicalDevice, Surface, &presentModeCount, null).Success();

            PresentModeKHR* presentModes = (PresentModeKHR*)ZenithMarshal.Allocate<PresentModeKHR>(scope, presentModeCount);
            Context.Surface?.GetPhysicalDeviceSurfacePresentModes(Context.PhysicalDevice, Surface, &presentModeCount, presentModes).Success();

            uint minImageCount = capabilities.MinImageCount + 1;
            if (capabilities.MaxImageCount > 0 && minImageCount > capabilities.MaxImageCount)
            {
                minImageCount = capabilities.MaxImageCount;
            }

            SurfaceFormatKHR surfaceFormat = surfaceFormats[0];
            foreach (SurfaceFormatKHR item in new ReadOnlySpan<SurfaceFormatKHR>(surfaceFormats, (int)surfaceFormatCount))
            {
                if (item.Format == VKFormats.Vulkan(Desc.ColorTargetFormat))
                {
                    surfaceFormat = item;

                    if (item.ColorSpace is ColorSpaceKHR.SpaceSrgbNonlinearKhr)
                    {
                        break;
                    }
                }
            }

            PresentModeKHR presentMode = presentModes[0];
            foreach (PresentModeKHR item in new ReadOnlySpan<PresentModeKHR>(presentModes, (int)presentModeCount))
            {
                if (Desc.VerticalSync && item is PresentModeKHR.FifoRelaxedKhr)
                {
                    presentMode = PresentModeKHR.FifoRelaxedKhr;

                    break;
                }
                else if (item is PresentModeKHR.MailboxKhr)
                {
                    presentMode = PresentModeKHR.MailboxKhr;

                    break;
                }
                else if (item is PresentModeKHR.ImmediateKhr)
                {
                    presentMode = PresentModeKHR.ImmediateKhr;

                    break;
                }
            }

            SwapchainCreateInfoKHR createInfo = new()
            {
                SType = StructureType.SwapchainCreateInfoKhr,
                Surface = Surface,
                MinImageCount = minImageCount,
                ImageFormat = surfaceFormat.Format,
                ImageColorSpace = surfaceFormat.ColorSpace,
                ImageExtent = new()
                {
                    Width = uint.Clamp(capabilities.MinImageExtent.Width, Desc.Surface.Width, capabilities.MaxImageExtent.Width),
                    Height = uint.Clamp(capabilities.MinImageExtent.Height, Desc.Surface.Height, capabilities.MaxImageExtent.Height)
                },
                ImageArrayLayers = 1,
                ImageUsage = ImageUsageFlags.ColorAttachmentBit,
                ImageSharingMode = Context.QueueFamilyIndices.Length is 1 ? SharingMode.Exclusive : SharingMode.Concurrent,
                QueueFamilyIndexCount = (uint)Context.QueueFamilyIndices.Length,
                PQueueFamilyIndices = queueFamilyIndices,
                CompositeAlpha = capabilities.SupportedCompositeAlpha.HasFlag(CompositeAlphaFlagsKHR.OpaqueBitKhr) ? CompositeAlphaFlagsKHR.OpaqueBitKhr : CompositeAlphaFlagsKHR.InheritBitKhr,
                PresentMode = presentMode,
                Clipped = true
            };

            Context.Swapchain?.CreateSwapchain(Context.Device, &createInfo, null, (SwapchainKHR*)Unsafe.AsPointer(ref Swapchain)).Success();
        }
    }

    private void DestroySwapChain()
    {
        if (Swapchain.Handle is not 0)
        {
            Context.Swapchain?.DestroySwapchain(Context.Device, Swapchain, null);

            Swapchain = default;
        }
    }
}
