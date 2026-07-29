namespace Zenith.NET;

public struct QueryHeapDesc
{
    public QueryType Type;

    public uint Count;

    public static QueryHeapDesc Occlusion(uint count)
    {
        return new()
        {
            Type = QueryType.Occlusion,
            Count = count
        };
    }

    public static QueryHeapDesc BinaryOcclusion(uint count)
    {
        return new()
        {
            Type = QueryType.BinaryOcclusion,
            Count = count
        };
    }

    public static QueryHeapDesc Timestamp(uint count)
    {
        return new()
        {
            Type = QueryType.Timestamp,
            Count = count
        };
    }
}
