using System.Text.Unicode;
using CornellBox.Helpers;
using CornellBox.Models;
using Hexa.NET.ImGui;
using Zenith.NET.Extensions.ImGui;
using Zenith.NET.Extensions.Upscaling;

namespace CornellBox;

internal class UserInterface
{
    private const string UpscalingQualities = "Off\0Native\0Quality\0Balanced\0Performance\0Ultra Performance\0";

    private readonly string graphicsApiText = $"GraphicsApi: {App.Context.GraphicsApi}";

    public RenderSettings RenderSettings { get; private set; } = new()
    {
        UpscalingQuality = App.Context.UpscalingCapabilities.SuperResolutionSupported ? UpscalingQuality.Quality : null,
        IsRayReconstructionEnabled = App.Context.UpscalingCapabilities.RayReconstructionSupported
    };

    public bool IsFrameGenerationEnabled { get; private set; } = App.Context.UpscalingCapabilities.FrameGenerationSupported;

    public void Update(double deltaSeconds)
    {
        App.Context.BeginImGuiFrame(deltaSeconds, App.LogicalWidth, App.LogicalHeight, App.DpiScale);

        ImGuiHelper.Overlay(() =>
        {
            ImGui.TextUnformatted(App.Context.Capabilities.DeviceName);
            ImGui.TextUnformatted(graphicsApiText);

            Span<byte> text = stackalloc byte[64];
            float framerate = ImGui.GetIO().Framerate * (IsFrameGenerationEnabled ? 2.0f : 1.0f);
            Utf8.TryWrite(text, $"FPS: {framerate:F1}\0", out int bytesWritten);
            ImGui.TextUnformatted(text[..bytesWritten]);
        });

        ImGuiHelper.Settings(() =>
        {
            RenderSettings settings = RenderSettings;

            ImGui.Checkbox("Pause", ref settings.IsPaused);
            ImGui.Separator();

            DrawUpscalingQuality(ref settings);
            DrawRayReconstruction(ref settings);
            DrawFrameGeneration();

            RenderSettings = settings;
        });
    }

    private void DrawFrameGeneration()
    {
        ImGui.BeginDisabled(!App.Context.UpscalingCapabilities.FrameGenerationSupported);

        bool isFrameGenerationEnabled = IsFrameGenerationEnabled;
        if (ImGui.Checkbox("Frame Generation", ref isFrameGenerationEnabled))
        {
            IsFrameGenerationEnabled = isFrameGenerationEnabled;
        }

        ImGui.EndDisabled();
    }

    private static void DrawUpscalingQuality(ref RenderSettings settings)
    {
        ImGui.BeginDisabled(!App.Context.UpscalingCapabilities.SuperResolutionSupported);

        int qualityIndex = settings.UpscalingQuality is UpscalingQuality quality ? (int)quality + 1 : 0;
        if (ImGui.Combo("Upscaling Quality", ref qualityIndex, UpscalingQualities))
        {
            settings.UpscalingQuality = qualityIndex is 0 ? null : (UpscalingQuality)(qualityIndex - 1);

            if (settings.UpscalingQuality is null)
            {
                settings.IsRayReconstructionEnabled = false;
            }
        }

        ImGui.EndDisabled();
    }

    private static void DrawRayReconstruction(ref RenderSettings settings)
    {
        ImGui.BeginDisabled(!App.Context.UpscalingCapabilities.RayReconstructionSupported || settings.UpscalingQuality is null);
        ImGui.Checkbox("Ray Reconstruction", ref settings.IsRayReconstructionEnabled);
        ImGui.EndDisabled();
    }
}
