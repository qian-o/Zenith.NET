namespace Zenith.NET;

/// <summary>
/// Specifies how input data is classified for the input assembler stage.
/// </summary>
public enum InputClassification
{
    /// <summary>
    /// Input data is per-vertex data.
    /// </summary>
    PerVertexData,

    /// <summary>
    /// Input data is per-instance data.
    /// </summary>
    PerInstanceData
}
