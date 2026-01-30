using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Metal;
using Zenith.NET.Vulkan;

foreach (Backend backend in Enum.GetValues<Backend>())
{
    try
    {
        using GraphicsContext context = backend switch
        {
            Backend.DirectX12 => GraphicsContext.CreateDirectX12(true),
            Backend.Metal => GraphicsContext.CreateMetal(true),
            Backend.Vulkan => GraphicsContext.CreateVulkan(true),
            _ => throw new NotSupportedException()
        };

        Console.WriteLine($"Backend {backend} is supported.");
        Console.WriteLine($"  Device Name: {context.Capabilities.DeviceName}");
        Console.WriteLine($"  Ray Tracing Supported: {context.Capabilities.RayTracingSupported}");
        Console.WriteLine($"  Mesh Shading Supported: {context.Capabilities.MeshShadingSupported}");
    }
    catch (Exception)
    {
        Console.WriteLine($"Backend {backend} is not supported.");
    }

    Console.WriteLine();
}