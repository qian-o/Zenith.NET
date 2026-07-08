using System.Diagnostics;
using Silk.NET.Vulkan;

namespace Zenith.NET.Vulkan;

internal unsafe class VKSwapChain : SwapChain
{
    private readonly VKFence fence;

    public SurfaceKHR Surface;

    public SwapchainKHR Swapchain;

    private VKTexture[] textures = [];
    private uint index;

    public VKSwapChain(VKGraphicsContext context, SwapChainDesc desc) : base(context, desc)
    {
        fence = new(context);

        CreateSwapChain();
        CreateTextures();
        AcquireNextImage();
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public override Texture Drawable => textures[index];

    public override nint GetNativeObject(NativeObjectType type)
    {
        return 0;
    }

    public override void Present()
    {
        fixed (SwapchainKHR* swapchains = &Swapchain)
        {
            fixed (uint* imageIndices = &index)
            {
                PresentInfoKHR presentInfo = new()
                {
                    SType = StructureType.PresentInfoKhr,
                    SwapchainCount = 1,
                    PSwapchains = swapchains,
                    PImageIndices = imageIndices
                };

                Context.Swapchain?.QueuePresent(Context.GraphicsQueue.Vulkan().Queue, &presentInfo).Success();
            }
        }

        AcquireNextImage();
    }

    protected override void ResizeImpl()
    {
        DestroyTextures();
        DestroySwapChain();

        CreateSwapChain();
        CreateTextures();

        AcquireNextImage();
    }

    protected override void RefreshImpl()
    {
        DestroyTextures();
        DestroySwapChain();

        CreateSwapChain();
        CreateTextures();

        AcquireNextImage();
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
        DestroyTextures();
        DestroySwapChain();

        fence.Dispose();
    }

    private void CreateSwapChain()
    {
        using ZenithMarshal.Scope scope = new();

        switch (Desc.Surface.Type)
        {
            case SurfaceType.Win32:
                {
                    Win32SurfaceCreateInfoKHR surfaceCreateInfo = new()
                    {
                        SType = StructureType.Win32SurfaceCreateInfoKhr,
                        Hinstance = Process.GetCurrentProcess().Handle,
                        Hwnd = Desc.Surface.Handles[0]
                    };

                    Context.Win32Surface?.CreateWin32Surface(Context.Instance, &surfaceCreateInfo, default, out Surface).Success();
                }
                break;

            case SurfaceType.Wayland:
                {
                    WaylandSurfaceCreateInfoKHR surfaceCreateInfo = new()
                    {
                        SType = StructureType.WaylandSurfaceCreateInfoKhr,
                        Display = (nint*)Desc.Surface.Handles[0],
                        Surface = (nint*)Desc.Surface.Handles[1]
                    };

                    Context.WaylandSurface?.CreateWaylandSurface(Context.Instance, &surfaceCreateInfo, default, out Surface).Success();
                }
                break;

            case SurfaceType.Xlib:
                {
                    XlibSurfaceCreateInfoKHR surfaceCreateInfo = new()
                    {
                        SType = StructureType.XlibSurfaceCreateInfoKhr,
                        Dpy = (nint*)Desc.Surface.Handles[0],
                        Window = Desc.Surface.Handles[1]
                    };

                    Context.XlibSurface?.CreateXlibSurface(Context.Instance, &surfaceCreateInfo, default, out Surface).Success();
                }
                break;

            case SurfaceType.Android:
                {
                    AndroidSurfaceCreateInfoKHR surfaceCreateInfo = new()
                    {
                        SType = StructureType.AndroidSurfaceCreateInfoKhr,
                        Window = (nint*)Desc.Surface.Handles[0]
                    };

                    Context.AndroidSurface?.CreateAndroidSurface(Context.Instance, &surfaceCreateInfo, default, out Surface).Success();
                }
                break;

            case SurfaceType.Apple:
                {
                    MetalSurfaceCreateInfoEXT surfaceCreateInfo = new()
                    {
                        SType = StructureType.MetalSurfaceCreateInfoExt,
                        PLayer = (nint*)Desc.Surface.Handles[0]
                    };

                    Context.MetalSurface?.CreateMetalSurface(Context.Instance, &surfaceCreateInfo, default, out Surface).Success();
                }
                break;
        }

        SurfaceCapabilitiesKHR capabilities = default;
        Context.Surface?.GetPhysicalDeviceSurfaceCapabilities(Context.PhysicalDevice, Surface, &capabilities).Success();

        uint surfaceFormatCount = 0;
        Context.Surface?.GetPhysicalDeviceSurfaceFormats(Context.PhysicalDevice, Surface, &surfaceFormatCount, default).Success();

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

        SurfaceFormatKHR surfaceFormat = default;
        foreach (SurfaceFormatKHR item in new ReadOnlySpan<SurfaceFormatKHR>(surfaceFormats, (int)surfaceFormatCount))
        {
            if (item.Format == VKFormats.Vulkan(Desc.Format).Format)
            {
                surfaceFormat = item;

                if (item.ColorSpace is ColorSpaceKHR.SpaceSrgbNonlinearKhr)
                {
                    break;
                }
            }
        }

        SurfaceTransformFlagsKHR preTransform = SurfaceTransformFlagsKHR.InheritBitKhr;
        if (capabilities.SupportedTransforms.HasFlag(SurfaceTransformFlagsKHR.IdentityBitKhr))
        {
            preTransform = SurfaceTransformFlagsKHR.IdentityBitKhr;
        }

        CompositeAlphaFlagsKHR compositeAlpha = CompositeAlphaFlagsKHR.InheritBitKhr;
        if (capabilities.SupportedCompositeAlpha.HasFlag(CompositeAlphaFlagsKHR.OpaqueBitKhr))
        {
            compositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr;
        }

        PresentModeKHR presentMode = PresentModeKHR.FifoKhr;
        foreach (PresentModeKHR item in new ReadOnlySpan<PresentModeKHR>(presentModes, (int)presentModeCount))
        {
            if (item is PresentModeKHR.MailboxKhr)
            {
                presentMode = PresentModeKHR.MailboxKhr;

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
            ImageUsage = ImageUsageFlags.TransferDstBit | ImageUsageFlags.ColorAttachmentBit,
            ImageSharingMode = Context.QueueFamilies.SharingMode,
            QueueFamilyIndexCount = Context.QueueFamilies.IndexCount,
            PQueueFamilyIndices = Context.QueueFamilies.Indices,
            PreTransform = preTransform,
            CompositeAlpha = compositeAlpha,
            PresentMode = presentMode,
            Clipped = true
        };

        Context.Swapchain?.CreateSwapchain(Context.Device, &createInfo, default, out Swapchain).Success();
    }

    private void DestroySwapChain()
    {
        Context.Swapchain?.DestroySwapchain(Context.Device, Swapchain, default);
        Context.Surface?.DestroySurface(Context.Instance, Surface, default);

        index = 0;
    }

    private void CreateTextures()
    {
        using ZenithMarshal.Scope scope = new();

        uint swapchainImageCount = 0;
        Context.Swapchain?.GetSwapchainImages(Context.Device, Swapchain, &swapchainImageCount, default).Success();

        Image* swapchainImages = (Image*)ZenithMarshal.Allocate<Image>(scope, swapchainImageCount);
        Context.Swapchain?.GetSwapchainImages(Context.Device, Swapchain, &swapchainImageCount, swapchainImages).Success();

        TextureDesc desc = new()
        {
            Type = TextureType.Texture2D,
            Format = Desc.Format,
            Width = Desc.Surface.Width,
            Height = Desc.Surface.Height,
            Depth = 1,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = SampleCount.Count1,
            Usages = TextureUsages.ColorAttachment | TextureUsages.TransferDst
        };

        textures = new VKTexture[swapchainImageCount];
        for (uint i = 0; i < swapchainImageCount; i++)
        {
            textures[i] = new(Context, desc, swapchainImages[i], new(default, 0, false));
        }
    }

    private void DestroyTextures()
    {
        for (int i = 0; i < textures.Length; i++)
        {
            textures[i].Dispose();
        }
    }

    private void AcquireNextImage()
    {
        Context.Swapchain?.AcquireNextImage(Context.Device, Swapchain, ulong.MaxValue, default, fence.Fence, ref index).Success();

        fence.Wait();
    }
}
