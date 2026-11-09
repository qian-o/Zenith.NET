namespace Zenith.NET.Extensions.Upscaling.FSR;

internal partial class FSRRayReconstruction
{
    #region DirectX12
    private static readonly ShaderDesc DirectX12Prepare = new()
    {
        Name = "Prepare",
        CodeBytes = [],
        ThreadGroupSize = new()
        {
            X = 0,
            Y = 0,
            Z = 0
        }
    };

    private static readonly ShaderDesc DirectX12Compose = new()
    {
        Name = "Compose",
        CodeBytes = [],
        ThreadGroupSize = new()
        {
            X = 0,
            Y = 0,
            Z = 0
        }
    };
    #endregion
}
