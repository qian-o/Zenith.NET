namespace FluidTank.Models;

internal struct SimulationSettings
{
    public float FlipRatio;

    public float VelocityDamping;

    public float WaveAmplitude;

    public float WaveFrequency;

    public bool WaveMakerEnabled;

    public int PressureIterations;
}
