# Backend Selection

Zenith.NET supports three graphics backends. Each backend targets specific platforms and hardware.

## Runtime Selection

Select the backend based on the current platform:

```csharp
GraphicsContext context;

if (OperatingSystem.IsWindows())
{
    context = GraphicsContext.CreateDirectX12(useValidationLayer: true);
}
else if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS())
{
    context = GraphicsContext.CreateMetal(useValidationLayer: true);
}
else
{
    context = GraphicsContext.CreateVulkan(useValidationLayer: true);
}
```

After creation, check which backend was selected:

```csharp
Console.WriteLine($"Backend: {context.Backend}");
Console.WriteLine($"Device: {context.Capabilities.DeviceName}");
```

## Validation Layer

Pass `useValidationLayer: true` during development to enable diagnostic messages. Disable in production for optimal performance. See [Graphics Context — Validation Messages](../concepts/graphics-context.md#validation-messages) for details.

## Capability Checks

Some features are not available on all hardware. Always check before using optional capabilities. See [Graphics Context — Capabilities](../concepts/graphics-context.md#capabilities) for the full list.

## NuGet Packages

Each backend is a separate NuGet package. Reference only the backends you need:

| Package | Backend |
|---------|---------|
| `Zenith.NET.DirectX12` | DirectX 12 (Windows) |
| `Zenith.NET.Metal` | Metal 4 (Apple) |
| `Zenith.NET.Vulkan` | Vulkan 1.4 (Cross-platform) |

All backends depend on the core `Zenith.NET` package, which provides the shared API surface.
