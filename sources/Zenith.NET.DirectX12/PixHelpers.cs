using System.Diagnostics;

namespace Zenith.NET.DirectX12;

/// <summary>
/// https://github.com/amerkoleci/Vortice.Windows/blob/main/src/Vortice.Direct3D12/PixHelpers.cs
/// </summary>
internal static unsafe class PixHelpers
{
    public const uint Version = 2;

    public const ulong Event = 0x002;

    public const ulong Marker = 0x008;

    public static uint CalculateEventSize(string label)
    {
        const uint startMarker = 3;
        const uint nullTerminator = 1;
        const uint endMarker = 1;

        return (uint)((startMarker + (label.Length / 4) + nullTerminator + endMarker) * 8);
    }

    public static void FormatEventToBuffer(ulong* buffer, ulong pixType, ulong color, string label)
    {
        ulong timestamp = (ulong)Stopwatch.GetTimestamp();

        buffer[0] = ((timestamp & 0x00000FFFFFFFFFFF) << 20) | ((pixType & 0x00000000000003FF) << 10);
        buffer[1] = color;
        buffer[2] = (8UL & 0x1F) << 55;

        int strIndex = 0;
        int bufferIndex = 3;
        ReadOnlySpan<char> str = label.AsSpan();

        while (true)
        {
            // char #1
            if (strIndex >= label.Length)
            {
                buffer[bufferIndex++] = 0;

                break;
            }

            uint c = str[strIndex++];
            ulong longValue = c;

            // char #2
            if (strIndex >= label.Length)
            {
                buffer[bufferIndex++] = longValue;

                break;
            }

            c = str[strIndex++];
            longValue |= (ulong)c << 16;

            // char #3
            if (strIndex >= label.Length)
            {
                buffer[bufferIndex++] = longValue;

                break;
            }

            c = str[strIndex++];
            longValue |= (ulong)c << 32;

            // char #4
            if (strIndex >= label.Length)
            {
                buffer[bufferIndex++] = longValue;

                break;
            }

            c = str[strIndex++];
            longValue |= (ulong)c << 48;

            // Write to the buffer.
            buffer[bufferIndex++] = longValue;
        }

        buffer[bufferIndex] = 0xFFF80;
    }
}
