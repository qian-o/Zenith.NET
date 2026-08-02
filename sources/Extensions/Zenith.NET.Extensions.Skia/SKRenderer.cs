using System.Runtime.InteropServices;
using SkiaSharp;

namespace Zenith.NET.Extensions.Skia;

internal unsafe class SKRenderer : DisposableObject
{
    private readonly Lock @lock = new();
    private readonly nint commandQueue;

    private uint referenceCount;

    public SKRenderer(GraphicsContext context)
    {
        Context = context;

        switch (context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                {
                    using GRD3DBackendContext backendContext = new()
                    {
                        Adapter = context.GetNativeObject(NativeObjectType.D3D12Adapter),
                        Device = context.GetNativeObject(NativeObjectType.D3D12Device),
                        Queue = context.GraphicsQueue.GetNativeObject(NativeObjectType.D3D12CommandQueue)
                    };

                    GRContext = GRContext.CreateDirect3D(backendContext);
                }
                break;

            case GraphicsApi.Metal:
                {
                    nint device = context.GetNativeObject(NativeObjectType.MTLDevice);

                    using GRMtlBackendContext backendContext = new()
                    {
                        DeviceHandle = device,
                        QueueHandle = commandQueue = SKObjectiveC.SendMessage(device, "newCommandQueue")
                    };

                    GRContext = GRContext.CreateMetal(backendContext);
                }
                break;

            case GraphicsApi.Vulkan:
                {
                    delegate* unmanaged<nint, byte*, nint> getInstanceProcAddr = (delegate* unmanaged<nint, byte*, nint>)context.GetNativeObject(NativeObjectType.VulkanGetInstanceProcAddr);
                    delegate* unmanaged<nint, byte*, nint> getDeviceProcAddr = (delegate* unmanaged<nint, byte*, nint>)context.GetNativeObject(NativeObjectType.VulkanGetDeviceProcAddr);

                    nint instance = context.GetNativeObject(NativeObjectType.VulkanInstance);
                    nint physicalDevice = context.GetNativeObject(NativeObjectType.VulkanPhysicalDevice);

                    using GRVkExtensions extensions = GRVkExtensions.Create(GetProcedureAddress, instance, physicalDevice, null, null);

                    using GRVkBackendContext backendContext = new()
                    {
                        VkInstance = instance,
                        VkPhysicalDevice = physicalDevice,
                        VkDevice = context.GetNativeObject(NativeObjectType.VulkanDevice),
                        VkQueue = context.GraphicsQueue.GetNativeObject(NativeObjectType.VulkanQueue),
                        GraphicsQueueIndex = (uint)context.GraphicsQueue.GetNativeObject(NativeObjectType.VulkanQueueFamilyIndex),
                        MaxAPIVersion = (1u << 22) | (4u << 12),
                        Extensions = extensions,
                        GetProcedureAddress = GetProcedureAddress
                    };

                    GRContext = GRContext.CreateVulkan(backendContext);

                    nint GetProcedureAddress(string name, nint instance, nint device)
                    {
                        using ZenithMarshal.Scope scope = new();

                        byte* pointer = (byte*)ZenithMarshal.StringToPointer(scope, name, StringEncoding.UTF8);

                        return device is 0 ? getInstanceProcAddr(instance, pointer) : getDeviceProcAddr(device, pointer);
                    }
                }
                break;

            default:
                GRContext = default!;
                break;
        }
    }

    public GraphicsContext Context { get; }

    public GRContext GRContext { get; }

    public void AddReference()
    {
        referenceCount++;
    }

    public bool RemoveReference()
    {
        return --referenceCount is 0;
    }

    public void Render(SKSurface surface, Action<SKCanvas> render)
    {
        using Lock.Scope _ = @lock.EnterScope();

        render(surface.Canvas);

        GRContext.Flush(true, true);
    }

    protected override void Destroy()
    {
        GRContext.Dispose();

        if (Context.GraphicsApi is GraphicsApi.Metal)
        {
            SKObjectiveC.Release(commandQueue);
        }
    }

    public GRBackendTexture CreateBackendTexture(Texture texture)
    {
        switch (Context.GraphicsApi)
        {
            case GraphicsApi.DirectX12:
                return new((int)texture.Desc.Width, (int)texture.Desc.Height, new GRD3DTextureResourceInfo
                {
                    Resource = texture.GetNativeObject(NativeObjectType.D3D12Resource),
                    Format = SKFormats.DirectX12(texture.Desc.Format),
                    SampleCount = 1,
                    LevelCount = 1
                });

            case GraphicsApi.Metal:
                return new((int)texture.Desc.Width, (int)texture.Desc.Height, false, new GRMtlTextureInfo() { TextureHandle = texture.GetNativeObject(NativeObjectType.MTLTexture) });

            case GraphicsApi.Vulkan:
                uint graphicsQueueFamily = (uint)Context.GraphicsQueue.GetNativeObject(NativeObjectType.VulkanQueueFamilyIndex);
                uint computeQueueFamily = (uint)Context.ComputeQueue.GetNativeObject(NativeObjectType.VulkanQueueFamilyIndex);
                uint transferQueueFamily = (uint)Context.TransferQueue.GetNativeObject(NativeObjectType.VulkanQueueFamilyIndex);
                bool concurrent = graphicsQueueFamily != computeQueueFamily || graphicsQueueFamily != transferQueueFamily;

                return new((int)texture.Desc.Width, (int)texture.Desc.Height, new GRVkImageInfo()
                {
                    Image = (ulong)texture.GetNativeObject(NativeObjectType.VulkanImage),
                    Alloc = new()
                    {
                        Memory = (ulong)texture.GetNativeObject(NativeObjectType.VulkanDeviceMemory),
                        Offset = (ulong)texture.GetNativeObject(NativeObjectType.VulkanDeviceMemoryOffset),
                        Size = Context.GetSizeAndAlignment(texture.Desc).SizeInBytes
                    },
                    Format = SKFormats.Vulkan(texture.Desc.Format),
                    ImageUsageFlags = SKFormats.Vulkan(texture.Desc.Usages),
                    SampleCount = 1,
                    LevelCount = 1,
                    CurrentQueueFamily = concurrent ? uint.MaxValue : graphicsQueueFamily,
                    SharingMode = concurrent ? 1u : 0u
                });

            default:
                return default!;
        }
    }
}

internal static partial class SKObjectiveC
{
    private const string LibObjC = "/usr/lib/libobjc.A.dylib";

    [LibraryImport(LibObjC, EntryPoint = "objc_msgSend")]
    private static partial nint SendMessage(nint receiver, nint selector);

    [LibraryImport(LibObjC, EntryPoint = "sel_registerName")]
    private static partial nint RegisterName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [LibraryImport(LibObjC, EntryPoint = "objc_release")]
    public static partial void Release(nint value);

    public static nint SendMessage(nint receiver, string selector)
    {
        return SendMessage(receiver, RegisterName(selector));
    }
}