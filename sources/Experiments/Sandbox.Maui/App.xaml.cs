using System.Diagnostics;
using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Metal;
using Zenith.NET.Vulkan;

namespace Sandbox.Maui;

public partial class App : Application
{
    static App()
    {
        {
            Context = GraphicsContext.CreateVulkan(true);
        }

        Context.ValidationMessage += static (sender, args) =>
        {
            Debug.WriteLine($"[{args.Source} - {args.Severity}] {args.Message}");
            Console.WriteLine($"[{args.Source} - {args.Severity}] {args.Message}");
        };
    }

    public App()
    {
        InitializeComponent();
    }

    public static GraphicsContext Context { get; }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}