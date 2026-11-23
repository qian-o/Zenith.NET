using Hexa.NET.ImGui;

namespace CornellBox.Helpers;

internal static class ImGuiHelper
{
    public static void Overlay(Action action)
    {
        ImGui.SetNextWindowPos(new(10.0f, 10.0f), ImGuiCond.Always, new(0.0f, 0.0f));
        ImGui.SetNextWindowBgAlpha(0.35f);

        if (ImGui.Begin("Overlay", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoInputs))
        {
            action();
        }

        ImGui.End();
    }

    public static void Settings(Action action)
    {
        ImGui.SetNextWindowPos(new(ImGui.GetIO().DisplaySize.X - 10.0f, 10.0f), ImGuiCond.Always, new(1.0f, 0.0f));
        ImGui.SetNextWindowCollapsed(true, ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Settings", ImGuiWindowFlags.AlwaysAutoResize))
        {
            action();
        }

        ImGui.End();
    }
}
