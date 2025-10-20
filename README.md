# Zenith.NET

A cross-platform, easy-to-use graphics and compute library for the .NET platform. It provides a unified programming interface for GPUs, simplifying the development of graphics rendering and general-purpose compute tasks.

## Graphics Backends

Zenith.NET supports multiple graphics APIs covering mainstream rendering technologies, making it easy to choose the right backend based on your needs.

| API       | Supported |
| :-------: | :-------: |
| DirectX12 | planned   |
| Metal     | planned   |
| Vulkan    | planned   |

## View Backends

Zenith.NET supports multiple .NET UI frameworks, and its rendering capabilities can be seamlessly integrated into different types of applications.

| Framework   | CPU Pixel Copy | Native GPU Rendering | DirectX12 | Metal     | Vulkan    |
| :---------: | :------------: | :------------------: | :-------: | :-------: | :-------: |
| Avalonia    | supported      |                      | supported | supported | supported |
| MAUI        |                | supported            | supported | supported | supported |
| WinForms    |                | supported            | supported |           | supported |
| WinUI       |                | supported            | supported |           | supported |
| WinUI (Uno) | supported      |                      | supported | supported | supported |
| WPF         |                | supported            | supported |           | supported |

- CPU Pixel Copy: Generate/receive a pixel buffer on the CPU, then copy the pixel data to a UI control for display.
- Native GPU Rendering: Render directly within the target platform's GPU context without intermediate pixel buffer copies.