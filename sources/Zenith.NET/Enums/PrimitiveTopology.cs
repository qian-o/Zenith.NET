namespace Zenith.NET;

public enum PrimitiveTopology
{
    /// <summary>
    /// Interprets the vertex data as a list of points.
    /// </summary>
    PointList,

    /// <summary>
    /// Interprets the vertex data as a list of lines.
    /// </summary>
    LineList,

    /// <summary>
    /// Interprets the vertex data as a line strip.
    /// </summary>
    LineStrip,

    /// <summary>
    /// Interprets the vertex data as a list of triangles.
    /// </summary>
    TriangleList,

    /// <summary>
    /// Interprets the vertex data as a triangle strip.
    /// </summary>
    TriangleStrip,

    /// <summary>
    /// Interprets the vertex data as a list of lines with adjacency data.
    /// </summary>
    LineListWithAdjacency,

    /// <summary>
    /// Interprets the vertex data as a line strip with adjacency data.
    /// </summary>
    LineStripWithAdjacency,

    /// <summary>
    /// Interprets the vertex data as a list of triangles with adjacency data.
    /// </summary>
    TriangleListWithAdjacency,

    /// <summary>
    /// Interprets the vertex data as a triangle strip with adjacency data.
    /// </summary>
    TriangleStripWithAdjacency,

    /// <summary>
    /// Interprets the vertex data as a patch list.
    /// </summary>
    PatchList = 99
}
