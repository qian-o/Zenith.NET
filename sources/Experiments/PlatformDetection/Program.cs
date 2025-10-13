using Zenith.NET;

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
        Console.WriteLine($"  API Version: {context.Capabilities.ApiVersion}");
        Console.WriteLine($"  Driver Version: {context.Capabilities.DriverVersion}");
        Console.WriteLine($"  Ray Tracing Supported: {context.Capabilities.RayTracingSupported}");
        Console.WriteLine($"  Mesh Shader Supported: {context.Capabilities.MeshShaderSupported}");
    }
    catch (Exception)
    {
        Console.WriteLine($"Backend {backend} is not supported.");
    }

    Console.WriteLine();
}