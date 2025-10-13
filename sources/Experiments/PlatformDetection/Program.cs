using Zenith.NET;

foreach (Backend backend in Enum.GetValues<Backend>())
{
    try
    {
        using GraphicsContext? context = backend switch
        {
            Backend.DirectX12 => GraphicsContext.CreateDirectX12(true),
            Backend.Metal => GraphicsContext.CreateMetal(true),
            Backend.Vulkan => GraphicsContext.CreateVulkan(true),
            _ => null
        };

        if (context is null)
        {
            Console.WriteLine($"Backend {backend} is unknown.");
        }
        else
        {
            Console.WriteLine($"Backend {backend} is supported.");
            Console.WriteLine($"  Device Name: {context.Capabilities.DeviceName}");
            Console.WriteLine($"  API Version: {context.Capabilities.ApiVersion}");
            Console.WriteLine($"  Driver Version: {context.Capabilities.DriverVersion}");
            Console.WriteLine($"  Ray Tracing Supported: {context.Capabilities.RayTracingSupported}");
            Console.WriteLine($"  Mesh Shader Supported: {context.Capabilities.MeshShaderSupported}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Backend {backend} is not supported on this platform.");
        Console.WriteLine($"  Error: {ex.Message}");
    }
}