using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Zenith.NET.Vulkan;

internal unsafe class VKCapabilities : Capabilities
{
    public VKCapabilities(VKGraphicsContext context)
    {
        using ZenithMarshal.Scope scope = new();

        PhysicalDeviceProperties properties;
        context.Vk.GetPhysicalDeviceProperties(context.PhysicalDevice, &properties);

        uint extensionCount = 0;
        context.Vk.EnumerateDeviceExtensionProperties(context.PhysicalDevice, (byte*)null, &extensionCount, (ExtensionProperties*)null).Success();

        ExtensionProperties* extensions = (ExtensionProperties*)ZenithMarshal.Allocate<ExtensionProperties>(scope, extensionCount);
        context.Vk.EnumerateDeviceExtensionProperties(context.PhysicalDevice, (byte*)null, &extensionCount, extensions).Success();

        string[] supportedExtensions = [.. new ReadOnlySpan<ExtensionProperties>(extensions, (int)extensionCount).ToArray().Select(static item => ZenithMarshal.StringFromPointer((nint)item.ExtensionName, StringEncoding.UTF8))];

        DeviceName = ZenithMarshal.StringFromPointer((nint)properties.DeviceName, StringEncoding.UTF8);
        ApiVersion = (Version32)properties.ApiVersion;
        RayTracingSupported = supportedExtensions.Contains(KhrRayQuery.ExtensionName) || supportedExtensions.Contains(KhrRayTracingPipeline.ExtensionName);
        MeshShaderSupported = supportedExtensions.Contains(ExtMeshShader.ExtensionName);
    }

    public override string DeviceName { get; }

    public override Version ApiVersion { get; }

    public override bool RayTracingSupported { get; }

    public override bool MeshShaderSupported { get; }
}
