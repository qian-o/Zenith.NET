using System.Text.Unicode;
using CornellBox.Models;
using Hexa.NET.ImGui;
using Zenith.NET.Extensions.ImGui;
using Zenith.NET.Extensions.Upscaling;

namespace CornellBox;

internal class UserInterface
{
    private const string UpscalingModes = "Off\0Native\0Quality\0Balanced\0Performance\0Ultra Performance\0";
    private const string RayReconstructionModes = "Off\0Quality\0Balanced\0Performance\0Ultra Performance\0";

    private readonly string graphicsApiText = $"GraphicsApi: {App.Context.GraphicsApi}";

    public RenderSettings RenderSettings { get; private set; } = new()
    {
        UpscalingMode = App.Context.UpscalingCapabilities.SuperResolutionSupported ? UpscalingMode.Quality : null,
        IsRayReconstructionEnabled = App.Context.UpscalingCapabilities.RayReconstructionSupported
    };

    public bool IsFrameGenerationEnabled { get; private set; }

    public void Update(double deltaSeconds)
    {
        App.Context.BeginImGuiFrame(deltaSeconds, App.LogicalWidth, App.LogicalHeight, App.DpiScale);

        DrawOverlay();
        DrawSettings();
    }

    private void DrawOverlay()
    {
        ImGui.SetNextWindowPos(new(10, 10), ImGuiCond.Always, new(0, 0));
        ImGui.SetNextWindowBgAlpha(0.35f);

        if (ImGui.Begin("Overlay", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoDecoration))
        {
            ImGui.TextUnformatted(App.Context.Capabilities.DeviceName);
            ImGui.TextUnformatted(graphicsApiText);

            Span<byte> text = stackalloc byte[64];
            Utf8.TryWrite(text, $"FPS: {ImGui.GetIO().Framerate:F1}\0", out int bytesWritten);
            ImGui.TextUnformatted(text[..bytesWritten]);
        }

        ImGui.End();
    }

    private void DrawSettings()
    {
        ImGui.SetNextWindowPos(new(ImGui.GetIO().DisplaySize.X - 10, 10), ImGuiCond.Always, new(1, 0));
        ImGui.SetNextWindowCollapsed(true, ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Settings", ImGuiWindowFlags.AlwaysAutoResize))
        {
            RenderSettings settings = RenderSettings;

            ImGui.Checkbox("Pause", ref settings.IsPaused);
            ImGui.Separator();

            DrawUpscalingMode(ref settings);
            DrawRayReconstruction(ref settings);
            DrawFrameGeneration();

            RenderSettings = settings;
        }

        ImGui.End();
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

    private static void DrawUpscalingMode(ref RenderSettings settings)
    {
        ImGui.BeginDisabled(!App.Context.UpscalingCapabilities.SuperResolutionSupported);

        int modeOffset = settings.IsRayReconstructionEnabled ? 0 : 1;
        int modeIndex = settings.UpscalingMode is UpscalingMode mode ? (int)mode + modeOffset : 0;
        string modes = settings.IsRayReconstructionEnabled ? RayReconstructionModes : UpscalingModes;
        if (ImGui.Combo("Upscaling Mode", ref modeIndex, modes))
        {
            settings.UpscalingMode = modeIndex is 0 ? null : (UpscalingMode)(modeIndex - modeOffset);
        }

        ImGui.EndDisabled();
    }

    private static void DrawRayReconstruction(ref RenderSettings settings)
    {
        ImGui.BeginDisabled(!App.Context.UpscalingCapabilities.RayReconstructionSupported || settings.UpscalingMode is null or UpscalingMode.Native);
        ImGui.Checkbox("Ray Reconstruction", ref settings.IsRayReconstructionEnabled);
        ImGui.EndDisabled();
    }
}
