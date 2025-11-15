using Hexa.NET.ImGui;

namespace SponzaScene;

internal class MainView : IView
{
    public void Update(double delta)
    {
    }

    public void Render(double delta)
    {
        ImGui.ShowDemoWindow();
    }
}
