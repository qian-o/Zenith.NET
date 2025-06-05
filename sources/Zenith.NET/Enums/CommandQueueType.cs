namespace Zenith.NET;

/// <summary>
/// Specifies the type of command queue for submitting GPU work.
/// </summary>
public enum CommandQueueType
{
    /// <summary>
    /// Direct queue for general rendering and compute commands.
    /// </summary>
    Direct,

    /// <summary>
    /// Queue for compute-only commands.
    /// </summary>
    Compute,

    /// <summary>
    /// Queue for copy/transfer commands.
    /// </summary>
    Copy
}
