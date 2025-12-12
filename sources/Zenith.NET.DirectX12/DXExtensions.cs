using System.Diagnostics;

namespace Zenith.NET.DirectX12;

internal static class DXExtensions
{
    extension(int result)
    {
        public void Success()
        {
            if (result is not 0)
            {
                Debug.WriteLine($"DirectX call failed with error code: {result}");
            }
        }

        public bool IsSuccess()
        {
            return result is 0;
        }
    }
}
