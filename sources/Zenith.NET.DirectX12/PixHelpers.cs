using System.Diagnostics;

namespace Zenith.NET.DirectX12;

// Ported from https://github.com/amerkoleci/Vortice.Windows/blob/main/src/Vortice.Direct3D12/PixHelpers.cs
internal static unsafe class PixHelpers
{
    public const uint Version = 2;

    public const ulong Event = 0x002;

    public const ulong Marker = 0x008;

    public static uint CalculateEventSize(string label)
    {
        const uint StartMarker = 3;
        const uint NullTerminator = 1;
        const uint EndMarker = 1;

        return (uint)((StartMarker + (label.Length / 4) + NullTerminator + EndMarker) * 8);
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
            if (strIndex >= label.Length)
            {
                buffer[bufferIndex++] = 0;

                break;
            }

            uint c = str[strIndex++];
            ulong longValue = c;

            if (strIndex >= label.Length)
            {
                buffer[bufferIndex++] = longValue;

                break;
            }

            c = str[strIndex++];
            longValue |= (ulong)c << 16;

            if (strIndex >= label.Length)
            {
                buffer[bufferIndex++] = longValue;

                break;
            }

            c = str[strIndex++];
            longValue |= (ulong)c << 32;

            if (strIndex >= label.Length)
            {
                buffer[bufferIndex++] = longValue;

                break;
            }

            c = str[strIndex++];
            longValue |= (ulong)c << 48;

            buffer[bufferIndex++] = longValue;
        }

        buffer[bufferIndex] = 0xFFF80;
    }
}
