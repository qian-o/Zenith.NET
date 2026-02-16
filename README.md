<p align="center">
  <img src="documents/images/Zenith.NET.svg" alt="Zenith.NET Logo" width="128" height="128">
</p>

<h1 align="center">Zenith.NET</h1>

<p align="center">
  A modern, cross-platform graphics and compute library for .NET.<br/>
  One API for DirectX 12, Metal, and Vulkan.
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/Zenith.NET"><img src="https://img.shields.io/nuget/v/Zenith.NET.svg?style=flat-square" alt="NuGet"></a>
  <a href="https://github.com/qian-o/Zenith.NET/blob/master/LICENSE"><img src="https://img.shields.io/github/license/qian-o/Zenith.NET?style=flat-square" alt="License"></a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Status-Work%20in%20Progress-orange?style=for-the-badge" alt="Status: Work in Progress">
</p>

---

## 📖 Overview

Zenith.NET is a GPU abstraction layer that unifies DirectX 12, Metal, and Vulkan under a single .NET API. It enables developers to build high-performance rendering and compute applications without writing backend-specific code. The library supports modern GPU features including ray tracing and mesh shading, and integrates seamlessly with popular .NET UI frameworks.

Visit the [documentation site](https://qian-o.github.io/Zenith.NET/) for tutorials and API reference.

## ✨ Features

- 🎯 **Unified API** — Write once, run on DirectX 12, Metal, and Vulkan
- 🎨 **Graphics** — Vertex and pixel shaders
- ⚡ **Compute** — General-purpose GPU computing
- 💡 **Ray Tracing** — Hardware-accelerated BLAS/TLAS with RayQuery in any shader stage
- 🔷 **Mesh Shading** — GPU-driven geometry with mesh and amplification shaders
- 🖼️ **UI Integrations** — Avalonia, MAUI, WinForms, WinUI, WPF, and Uno Platform

---

## 🌍 Platform Support

|           | DirectX 12 | Metal 4 | Vulkan 1.4 |
| :-------: | :--------: | :-----: | :--------: |
| Windows   | ✅ |  | ✅ |
| Linux     |  |  | ✅ |
| Apple     |  | 🚧 | ✅ |
| Android   |  |  | ✅ |

> 🚧 Metal backend is under development.

---

## 📦 NuGet Packages

### Core

[![Zenith.NET](https://img.shields.io/nuget/v/Zenith.NET.svg?label=Zenith.NET&style=flat-square)](https://www.nuget.org/packages/Zenith.NET)

### Backends

[![DirectX12](https://img.shields.io/nuget/v/Zenith.NET.DirectX12.svg?label=DirectX12&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.DirectX12)
[![Metal](https://img.shields.io/nuget/v/Zenith.NET.Metal.svg?label=Metal&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.Metal)
[![Vulkan](https://img.shields.io/nuget/v/Zenith.NET.Vulkan.svg?label=Vulkan&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.Vulkan)

### Extensions

[![ImageSharp](https://img.shields.io/nuget/v/Zenith.NET.Extensions.ImageSharp.svg?label=ImageSharp&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.Extensions.ImageSharp)
[![ImGui](https://img.shields.io/nuget/v/Zenith.NET.Extensions.ImGui.svg?label=ImGui&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.Extensions.ImGui)
[![Skia](https://img.shields.io/nuget/v/Zenith.NET.Extensions.Skia.svg?label=Skia&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.Extensions.Skia)
[![Slang](https://img.shields.io/nuget/v/Zenith.NET.Extensions.Slang.svg?label=Slang&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.Extensions.Slang)

### Views

[![Views](https://img.shields.io/nuget/v/Zenith.NET.Views.svg?label=Views&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.Views)
[![Avalonia](https://img.shields.io/nuget/v/Zenith.NET.Views.Avalonia.svg?label=Avalonia&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.Views.Avalonia)
[![MAUI](https://img.shields.io/nuget/v/Zenith.NET.Views.Maui.svg?label=MAUI&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.Views.Maui)
[![WinForms](https://img.shields.io/nuget/v/Zenith.NET.Views.WinForms.svg?label=WinForms&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.Views.WinForms)
[![WinUI](https://img.shields.io/nuget/v/Zenith.NET.Views.WinUI.svg?label=WinUI&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.Views.WinUI)
[![WPF](https://img.shields.io/nuget/v/Zenith.NET.Views.WPF.svg?label=WPF&style=flat-square)](https://www.nuget.org/packages/Zenith.NET.Views.WPF)
