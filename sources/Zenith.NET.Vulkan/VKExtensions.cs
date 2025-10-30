using System.Diagnostics;

namespace Zenith.NET;

internal static class VKExtensions
{
    extension(VkResult result)
    {
        public void Success()
        {
            if (result is not VkResult.Success)
            {
                Debug.WriteLine($"Vulkan call failed with error: {result}");
            }
        }
    }
}
