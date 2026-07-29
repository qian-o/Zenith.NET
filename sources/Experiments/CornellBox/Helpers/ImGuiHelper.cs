using Hexa.NET.ImGui;

namespace CornellBox.Helpers;

internal static class ImGuiHelper
{
    public static void Overlay(Action action)
    {
        ImGui.SetNextWindowPos(new(10, 10), ImGuiCond.Always, new(0, 0));
        ImGui.SetNextWindowBgAlpha(0.35f);

        if (ImGui.Begin("Overlay", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoDecoration))
        {
            action();
        }

        ImGui.End();
    }

    public static void Settings(Action action)
    {
        ImGui.SetNextWindowPos(new(ImGui.GetIO().DisplaySize.X - 10, 10), ImGuiCond.Always, new(1, 0));
        ImGui.SetNextWindowCollapsed(true, ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Settings", ImGuiWindowFlags.AlwaysAutoResize))
        {
            action();
        }

        ImGui.End();
    }
}
