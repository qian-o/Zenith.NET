namespace Zenith.NET;

/// <summary>
/// Represents a description interface for resource descriptors that can be validated.
/// Implement this interface to provide a validation method for resource configuration objects.
/// </summary>
public interface IDesc
{
    /// <summary>
    /// Validates the current descriptor instance.
    /// </summary>
    /// <returns><c>true</c> if the descriptor is valid; otherwise, <c>false</c>.</returns>
    bool Validate();
}
