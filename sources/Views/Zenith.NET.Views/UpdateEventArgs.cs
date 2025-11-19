namespace Zenith.NET.Views;

public class UpdateEventArgs(double deltaTime, double totalTime) : EventArgs
{
    public double DeltaTime { get; } = deltaTime;

    public double TotalTime { get; } = totalTime;
}
