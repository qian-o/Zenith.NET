using System.Diagnostics;
using NGX.NET;

namespace Zenith.NET.Extensions.Upscaling.DLSS;

internal static class Extensions
{
    extension(NGXResult result)
    {
        internal void Success()
        {
            if (!result.IsSuccess())
            {
                Debug.WriteLine($"NGX call failed with error: {result}");
            }
        }

        internal bool IsSuccess()
        {
            return !Ngx.NVSDK_NGX_FAILED(result);
        }
    }

    extension(int result)
    {
        internal void Success()
        {
            if (result is not 0)
            {
                Debug.WriteLine($"Vulkan call failed with error code: {result}");
            }
        }
    }
}
