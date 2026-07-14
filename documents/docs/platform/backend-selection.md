# Graphics API Selection

Zenith.NET exposes a single public Graphics API abstraction (`GraphicsContext`) with three concrete `GraphicsApi` targets:

- `GraphicsApi.DirectX12`
- `GraphicsApi.Metal`
- `GraphicsApi.Vulkan`

## Packages and Namespaces

Reference the core package plus one or more Graphics API packages.

| Package | Namespace | Factory Extension | Graphics API |
|---|---|---|---|
| `Zenith.NET` | `Zenith.NET` | N/A | Shared abstractions |
| `Zenith.NET.DirectX12` | `Zenith.NET.DirectX12` | `GraphicsContext.CreateDirectX12(bool useValidationLayer)` | `GraphicsApi.DirectX12` |
| `Zenith.NET.Metal` | `Zenith.NET.Metal` | `GraphicsContext.CreateMetal(bool useValidationLayer)` | `GraphicsApi.Metal` |
| `Zenith.NET.Vulkan` | `Zenith.NET.Vulkan` | `GraphicsContext.CreateVulkan(bool useValidationLayer)` | `GraphicsApi.Vulkan` |

## Platform-Driven Selection

Use explicit platform rules first, then keep a fallback strategy for unsupported environments.

```csharp
using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Metal;
using Zenith.NET.Vulkan;

GraphicsContext context;

if (OperatingSystem.IsWindows())
{
    context = GraphicsContext.CreateDirectX12(useValidationLayer: true);
}
else if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst())
{
    context = GraphicsContext.CreateMetal(useValidationLayer: true);
}
else
{
    context = GraphicsContext.CreateVulkan(useValidationLayer: true);
}

Console.WriteLine($"Graphics API: {context.GraphicsApi}");
```

Notes:

- Prefer explicit branches when your app has known deployment targets.
- Vulkan requires API version 1.4 plus the extensions used by Zenith.NET's bindless ABI, including `VK_EXT_descriptor_heap` and `VK_KHR_shader_untyped_pointers`. Actual support is device and driver dependent.

## Enumeration and Runtime Detection

If you want to probe support dynamically, iterate `Enum.GetValues<GraphicsApi>()` and attempt context creation.

```csharp
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

        Console.WriteLine($"Graphics API {graphicsApi} is supported.");
        Console.WriteLine($"  Device Name: {context.Capabilities.DeviceName}");
        Console.WriteLine($"  Ray Tracing Supported: {context.Capabilities.RayTracingSupported}");
        Console.WriteLine($"  Mesh Shading Supported: {context.Capabilities.MeshShadingSupported}");
    }
    catch
    {
        Console.WriteLine($"Graphics API {graphicsApi} is not supported.");
    }
}
```

## Validation Messages

Create the context with `useValidationLayer: true` during development and subscribe to:

- `GraphicsContext.ValidationMessage`

The event args type is `ValidationMessageEventArgs` and currently exposes only:

- `Severity` (`MessageSeverity`)
- `Message` (`string`)
- `Timestamp` (`DateTimeOffset`)

See [Graphics Context](../concepts/graphics-context.md) and [Synchronization and Barriers](../concepts/synchronization.md) for how to combine validation with explicit command ordering.

## Capabilities Surface

The public `Capabilities` contract is intentionally small. It currently exposes only:

- `DeviceName`
- `RayTracingSupported`
- `MeshShadingSupported`

Guard optional features with capabilities checks before creating related pipelines or acceleration structures.

## Package Strategy

- Ship only the Graphics API packages your product needs.
- Prefer runtime fallback only when your deployment matrix is broad enough to justify extra package size.
- Keep platform assumptions narrow and source-backed. For example, DirectX 12 integration is Windows-focused, while Metal paths are Apple-focused.

For UI integration choices by framework and platform, see [UI Framework Integration](ui-frameworks.md).
