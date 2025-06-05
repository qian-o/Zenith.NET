namespace Zenith.NET;

/// <summary>
/// Specifies the graphics backend API in use.
/// </summary>
public enum Backend
{
    /// <summary>
    /// Microsoft DirectX 12 backend.
    /// </summary>
    DirectX12,

    /// <summary>
    /// Apple Metal backend.
    /// </summary>
    Metal,

    /// <summary>
    /// Khronos Vulkan backend.
    /// </summary>
    Vulkan
}
