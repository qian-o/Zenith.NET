namespace Zenith.NET.Views.WPF;

public class UpdateEventArgs(double deltaTime, double totalTime) : EventArgs
{
    public double DeltaTime { get; } = deltaTime;

    public double TotalTime { get; } = totalTime;
}
