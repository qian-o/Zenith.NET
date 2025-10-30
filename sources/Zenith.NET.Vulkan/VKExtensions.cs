namespace Zenith.NET;

internal static class VKExtensions
{
    extension(VkResult result)
    {
        public bool IsSuccess()
        {
            return result is VkResult.Success;
        }
    }
}
