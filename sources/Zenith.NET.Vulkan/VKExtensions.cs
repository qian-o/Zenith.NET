using System.Diagnostics;
using Silk.NET.Vulkan;

namespace Zenith.NET;

internal static class VKExtensions
{
    extension(Result result)
    {
        public void Success()
        {
            if (result is not Result.Success)
            {
                Debug.WriteLine($"Vulkan call failed with error: {result}");
            }
        }
    }
}
