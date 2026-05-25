using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Metal;
using Zenith.NET.Vulkan;

foreach (GraphicsApi graphicsApi in Enum.GetValues<GraphicsApi>())
{
    try
    {
        using GraphicsContext context = graphicsApi switch
        {
            GraphicsApi.DirectX12 => GraphicsContext.CreateDirectX12(true),
            GraphicsApi.Metal => GraphicsContext.CreateMetal(true),
            GraphicsApi.Vulkan => GraphicsContext.CreateVulkan(true),
            _ => throw new NotSupportedException()
        };

        Console.WriteLine($"GraphicsApi {graphicsApi} is supported.");
        Console.WriteLine($"  Device Name: {context.Capabilities.DeviceName}");
        Console.WriteLine($"  Ray Tracing Supported: {context.Capabilities.RayTracing}");
        Console.WriteLine($"  Mesh Shading Supported: {context.Capabilities.MeshShading}");
    }
    catch (Exception)
    {
        Console.WriteLine($"GraphicsApi {graphicsApi} is not supported.");
    }

    Console.WriteLine();
}