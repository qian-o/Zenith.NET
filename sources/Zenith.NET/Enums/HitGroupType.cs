namespace Zenith.NET;

public enum HitGroupType
{
    /// <summary>
    /// Describes a group that uses a triangle list to calculate the number of ray hits.
    /// Hit groups that use triangles cannot contain an intersection shader.
    /// </summary>
    Triangles,

    /// <summary>
    /// Describes a group that uses procedural primitives inside bounding boxes to calculate the number of ray hits.
    /// Hit groups that use procedural primitives must contain an intersection shader.
    /// </summary>
    Procedural
}
