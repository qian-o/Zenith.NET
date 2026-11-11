using System.Runtime.InteropServices;
using FSR.NET;

namespace Zenith.NET.Extensions.Upscaling.FSR;

internal unsafe class FSRVendor : Vendor
{
    private readonly FSRVkDeviceContext deviceContext;

    public FSRVendor(GraphicsContext context) : base(context)
    {
        if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture is not Architecture.X64)
        {
            Capabilities = new(false, false, false);

            return;
        }

        if (context.GraphicsApi is GraphicsApi.Vulkan)
        {
            deviceContext = new()
            {
                VkDevice = context.GetNativeObject(NativeObjectType.VulkanDevice),
                VkPhysicalDevice = context.GetNativeObject(NativeObjectType.VulkanPhysicalDevice),
                VkDeviceProcAddr = (delegate* unmanaged[Stdcall]<nint, byte*, delegate* unmanaged[Stdcall]<void>>)context.GetNativeObject(NativeObjectType.VulkanGetDeviceProcAddr)
            };
        }

        bool superResolutionSupported = IsAvailable(FSRCreateContextDescUpscale.Type);
        bool rayReconstructionSupported = context.GraphicsApi is GraphicsApi.DirectX12 && superResolutionSupported && IsAvailable(FSRCreateContextDescDenoiser.Type);
        bool frameGenerationSupported = IsAvailable(FSRCreateContextDescFrameGeneration.Type);

        Capabilities = new(superResolutionSupported, rayReconstructionSupported, frameGenerationSupported);
    }

    public override UpscalingCapabilities Capabilities { get; }

    public override (uint Width, uint Height) GetSuperResolutionRenderSize(uint outputWidth, uint outputHeight, UpscalingQuality quality)
    {
        return GetRenderSize(outputWidth, outputHeight, quality);
    }

    public override (uint Width, uint Height) GetRayReconstructionRenderSize(uint outputWidth, uint outputHeight, UpscalingQuality quality)
    {
        return GetRenderSize(outputWidth, outputHeight, quality);
    }

    public override SuperResolution CreateSuperResolution(SuperResolutionDesc desc)
    {
        return new FSRSuperResolution(this, desc);
    }

    public override RayReconstruction CreateRayReconstruction(RayReconstructionDesc desc)
    {
        return new FSRRayReconstruction(this, desc);
    }

    public override FrameGeneration CreateFrameGeneration(FrameGenerationDesc desc)
    {
        return new FSRFrameGeneration(this, desc);
    }

    public FSRContext CreateContext(FSRApiHeader* desc)
    {
        FSRCreateBackendDX12Desc directX12 = new() { Device = Context.GetNativeObject(NativeObjectType.D3D12Device) };
        FSRCreateBackendVKDesc vulkan = new() { DeviceContext = deviceContext };
        FSRApiHeader* tail = desc;
        while (tail->Next is not null)
        {
            tail = tail->Next;
        }

        tail->Next = Context.GraphicsApi is GraphicsApi.DirectX12 ? &directX12.Header : &vulkan.Header;

        FSRContext context = default;
        FSRApiReturnCodes result = Context.GraphicsApi is GraphicsApi.DirectX12 ? Fsr.D3D12.CreateContext(&context, desc, null) : Fsr.Vulkan.CreateContext(&context, desc, null);

        tail->Next = null;
        result.Success();

        return context;
    }

    public void DestroyContext(FSRContext context)
    {
        if (context.IsNull)
        {
            return;
        }

        FSRApiReturnCodes result = Context.GraphicsApi is GraphicsApi.DirectX12 ? Fsr.D3D12.DestroyContext(&context, null) : Fsr.Vulkan.DestroyContext(&context, null);
        result.Success();
    }

    public void Configure(FSRContext context, FSRApiHeader* desc)
    {
        FSRApiReturnCodes result = Context.GraphicsApi is GraphicsApi.DirectX12 ? Fsr.D3D12.Configure(&context, desc) : Fsr.Vulkan.Configure(&context, desc);
        result.Success();
    }

    public void Dispatch(FSRContext context, FSRApiHeader* desc)
    {
        FSRApiReturnCodes result = Context.GraphicsApi is GraphicsApi.DirectX12 ? Fsr.D3D12.Dispatch(&context, desc) : Fsr.Vulkan.Dispatch(&context, desc);
        result.Success();
    }

    protected override void Destroy()
    {
    }

    private bool IsAvailable(ulong type)
    {
        ulong count = 0;
        FSRVkDeviceContext vulkan = deviceContext;
        FSRQueryDescGetVersions query = new()
        {
            CreateDescType = type,
            Device = Context.GraphicsApi is GraphicsApi.DirectX12 ? (void*)Context.GetNativeObject(NativeObjectType.D3D12Device) : &vulkan,
            OutputCount = &count
        };
        FSRApiReturnCodes result = Context.GraphicsApi is GraphicsApi.DirectX12 ? Fsr.D3D12.Query(null, &query.Header) : Fsr.Vulkan.Query(null, &query.Header);
        result.Success();

        return result.IsSuccess() && count > 0;
    }

    private (uint Width, uint Height) GetRenderSize(uint outputWidth, uint outputHeight, UpscalingQuality quality)
    {
        uint width = 0;
        uint height = 0;
        FSRQueryDescUpscaleGetRenderResolutionFromQualityMode query = new()
        {
            DisplayWidth = outputWidth,
            DisplayHeight = outputHeight,
            QualityMode = (uint)FSRFormats.FSR(quality),
            RenderWidth = &width,
            RenderHeight = &height
        };
        FSRApiReturnCodes result = Context.GraphicsApi is GraphicsApi.DirectX12 ? Fsr.D3D12.Query(null, &query.Header) : Fsr.Vulkan.Query(null, &query.Header);
        result.Success();

        return result.IsSuccess() ? (width, height) : default;
    }
}
