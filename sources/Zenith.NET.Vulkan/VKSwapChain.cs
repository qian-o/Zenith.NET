using System.Diagnostics;
using Silk.NET.Vulkan;

namespace Zenith.NET;

internal unsafe class VKSwapChain : SwapChain
{
    private readonly VKFence fence;
    private readonly VKSwapChainFrameBuffer swapChainFrameBuffer;

    public SurfaceKHR Surface;

    public SwapchainKHR Swapchain;

    public uint ImageIndex;

    public VKSwapChain(VKGraphicsContext context, SwapChainDesc desc) : base(context, desc)
    {
        fence = new(context);
        swapChainFrameBuffer = new(context, this);

        CreateSurface();
        CreateSwapChain();
    }

    public new VKGraphicsContext Context => (VKGraphicsContext)base.Context;

    public override FrameBuffer FrameBuffer => swapChainFrameBuffer[ImageIndex];

    public override void Present()
    {
        if (Swapchain.Handle is not 0)
        {
            fixed (SwapchainKHR* pSwapchains = &Swapchain)
            {
                fixed (uint* pImageIndices = &ImageIndex)
                {
                    PresentInfoKHR presentInfo = new()
                    {
                        SType = StructureType.PresentInfoKhr,
                        SwapchainCount = 1,
                        PSwapchains = pSwapchains,
                        PImageIndices = pImageIndices
                    };

                    Result result = Context.Swapchain?.QueuePresent(Context.GraphicsQueue, &presentInfo) ?? Result.ErrorInitializationFailed;

                    if (result is Result.ErrorOutOfDateKhr or Result.SuboptimalKhr)
                    {
                        CreateSwapChain();

                        return;
                    }

                    result.Success();

                    AcquireNextImage();
                }
            }
        }
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
        if (Swapchain.Handle is not 0)
        {
            using ZenithMarshal.Scope scope = new();

            DebugUtilsObjectNameInfoEXT nameInfo = new()
            {
                SType = StructureType.DebugUtilsObjectNameInfoExt,
                ObjectType = ObjectType.SwapchainKhr,
                ObjectHandle = Swapchain.Handle,
                PObjectName = (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8)
            };

            Context.DebugUtils?.SetDebugUtilsObjectName(Context.Device, &nameInfo).Success();
        }
    }

    protected override void Destroy()
    {
        DestroySwapChain();
        DestroySurface();

        swapChainFrameBuffer.Dispose();
        fence.Dispose();
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

                    Context.Win32Surface?.CreateWin32Surface(Context.Instance, &createInfo, null, out Surface).Success();
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

                    Context.WaylandSurface?.CreateWaylandSurface(Context.Instance, &createInfo, null, out Surface).Success();
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

                    Context.XlibSurface?.CreateXlibSurface(Context.Instance, &createInfo, null, out Surface).Success();
                }
                break;

            case SurfaceType.Android:
                {
                    AndroidSurfaceCreateInfoKHR createInfo = new()
                    {
                        SType = StructureType.AndroidSurfaceCreateInfoKhr,
                        Window = (nint*)Desc.Surface.Handles[0]
                    };

                    Context.AndroidSurface?.CreateAndroidSurface(Context.Instance, &createInfo, null, out Surface).Success();
                }
                break;

            case SurfaceType.Apple:
                {
                    MetalSurfaceCreateInfoEXT createInfo = new()
                    {
                        SType = StructureType.MetalSurfaceCreateInfoExt,
                        PLayer = (nint*)Desc.Surface.Handles[0]
                    };

                    Context.MetalSurface?.CreateMetalSurface(Context.Instance, &createInfo, null, out Surface).Success();
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

            (SharingMode sharingMode, uint queueFamilyIndexCount, nint pQueueFamilyIndices) = Context.GetSharingModeInfo(scope);

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

            SurfaceFormatKHR surfaceFormat = default;
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

            Extent2D imageExtent = new()
            {
                Width = uint.Clamp(capabilities.MinImageExtent.Width, Desc.Surface.Width, capabilities.MaxImageExtent.Width),
                Height = uint.Clamp(capabilities.MinImageExtent.Height, Desc.Surface.Height, capabilities.MaxImageExtent.Height)
            };

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

            PresentModeKHR presentMode = default;
            foreach (PresentModeKHR item in new ReadOnlySpan<PresentModeKHR>(presentModes, (int)presentModeCount))
            {
                if (item is PresentModeKHR.MailboxKhr)
                {
                    presentMode = PresentModeKHR.MailboxKhr;

                    break;
                }
                else if (item is PresentModeKHR.ImmediateKhr)
                {
                    presentMode = PresentModeKHR.ImmediateKhr;
                }
            }

            SwapchainCreateInfoKHR createInfo = new()
            {
                SType = StructureType.SwapchainCreateInfoKhr,
                Surface = Surface,
                MinImageCount = minImageCount,
                ImageFormat = surfaceFormat.Format,
                ImageColorSpace = surfaceFormat.ColorSpace,
                ImageExtent = imageExtent,
                ImageArrayLayers = 1,
                ImageUsage = ImageUsageFlags.ColorAttachmentBit,
                ImageSharingMode = sharingMode,
                QueueFamilyIndexCount = queueFamilyIndexCount,
                PQueueFamilyIndices = (uint*)pQueueFamilyIndices,
                PreTransform = preTransform,
                CompositeAlpha = compositeAlpha,
                PresentMode = presentMode,
                Clipped = true
            };

            Context.Swapchain?.CreateSwapchain(Context.Device, &createInfo, null, out Swapchain).Success();

            swapChainFrameBuffer.CreateFrameBuffers(createInfo.ImageExtent.Width, createInfo.ImageExtent.Height, []);

            AcquireNextImage();
        }
        else
        {
            swapChainFrameBuffer.CreateFrameBuffers(Desc.Surface.Width, Desc.Surface.Height, Desc.Surface.Handles);
        }
    }

    private void DestroySwapChain()
    {
        swapChainFrameBuffer.DestroyFrameBuffers();

        if (Swapchain.Handle is not 0)
        {
            Context.Swapchain?.DestroySwapchain(Context.Device, Swapchain, null);

            Swapchain = default;
        }

        ImageIndex = 0;
    }

    private void AcquireNextImage()
    {
        fixed (uint* pImageIndex = &ImageIndex)
        {
            Result result = Context.Swapchain?.AcquireNextImage(Context.Device, Swapchain, ulong.MaxValue, default, fence.Fence, pImageIndex) ?? Result.ErrorInitializationFailed;

            if (result is Result.ErrorOutOfDateKhr or Result.SuboptimalKhr)
            {
                CreateSwapChain();

                return;
            }

            result.Success();

            fence.Wait();
        }
    }
}
