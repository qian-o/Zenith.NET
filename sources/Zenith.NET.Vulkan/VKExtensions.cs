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

    extension(Shader shader)
    {
        public VKShader Vulkan()
        {
            return (VKShader)shader;
        }
    }

    extension(Buffer buffer)
    {
        public VKBuffer Vulkan()
        {
            return (VKBuffer)buffer;
        }
    }

    extension(BufferView bufferView)
    {
        public VKBufferView Vulkan()
        {
            return (VKBufferView)bufferView;
        }
    }
}
