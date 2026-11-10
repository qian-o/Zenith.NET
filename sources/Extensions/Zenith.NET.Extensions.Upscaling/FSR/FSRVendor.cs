using System.Runtime.InteropServices;
using FSR.NET;

namespace Zenith.NET.Extensions.Upscaling.FSR;

internal unsafe class FSRVendor : Vendor
{
    private readonly FSRVkDeviceContext deviceContext;

    public FSRVendor(GraphicsContext context)
    {
        Context = context;

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

    public GraphicsContext Context { get; }

    public override UpscalingCapabilities Capabilities { get; }

    public override UpscalingInputSize GetSuperResolutionInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        return GetInputSize(outputWidth, outputHeight, mode);
    }

    public override UpscalingInputSize GetRayReconstructionInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        return GetInputSize(outputWidth, outputHeight, mode);
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

    public void CreateContext(ref FSRContext context, FSRApiHeader* desc)
    {
        FSRCreateBackendDX12Desc directX12 = new() { Device = Context.GetNativeObject(NativeObjectType.D3D12Device) };
        FSRCreateBackendVKDesc vulkan = new() { DeviceContext = deviceContext };
        FSRApiHeader* tail = desc;
        while (tail->Next is not null)
        {
            tail = tail->Next;
        }

        tail->Next = Context.GraphicsApi is GraphicsApi.DirectX12 ? &directX12.Header : &vulkan.Header;

        FSRApiReturnCodes result;
        fixed (FSRContext* pointer = &context)
        {
            result = Context.GraphicsApi is GraphicsApi.DirectX12 ? Fsr.D3D12.CreateContext(pointer, desc, null) : Fsr.Vulkan.CreateContext(pointer, desc, null);
        }

        tail->Next = null;
        result.Success();
    }

    public void DestroyContext(ref FSRContext context)
    {
        if (context.IsNull)
        {
            return;
        }

        FSRApiReturnCodes result;
        fixed (FSRContext* pointer = &context)
        {
            result = Context.GraphicsApi is GraphicsApi.DirectX12 ? Fsr.D3D12.DestroyContext(pointer, null) : Fsr.Vulkan.DestroyContext(pointer, null);
        }

        context = default;
        result.Success();
    }

    public void Configure(ref FSRContext context, FSRApiHeader* desc)
    {
        FSRApiReturnCodes result;
        fixed (FSRContext* pointer = &context)
        {
            result = Context.GraphicsApi is GraphicsApi.DirectX12 ? Fsr.D3D12.Configure(pointer, desc) : Fsr.Vulkan.Configure(pointer, desc);
        }

        result.Success();
    }

    public void Dispatch(ref FSRContext context, FSRApiHeader* desc)
    {
        FSRApiReturnCodes result;
        fixed (FSRContext* pointer = &context)
        {
            result = Context.GraphicsApi is GraphicsApi.DirectX12 ? Fsr.D3D12.Dispatch(pointer, desc) : Fsr.Vulkan.Dispatch(pointer, desc);
        }

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

    private UpscalingInputSize GetInputSize(uint outputWidth, uint outputHeight, UpscalingMode mode)
    {
        uint renderWidth = 0;
        uint renderHeight = 0;
        FSRQueryDescUpscaleGetRenderResolutionFromQualityMode query = new()
        {
            DisplayWidth = outputWidth,
            DisplayHeight = outputHeight,
            QualityMode = (uint)FSRFormats.FSR(mode),
            RenderWidth = &renderWidth,
            RenderHeight = &renderHeight
        };
        FSRApiReturnCodes result = Context.GraphicsApi is GraphicsApi.DirectX12 ? Fsr.D3D12.Query(null, &query.Header) : Fsr.Vulkan.Query(null, &query.Header);
        result.Success();

        return result.IsSuccess() ? new(renderWidth, renderHeight) : default;
    }
}
