using System.Diagnostics;

namespace Zenith.NET.DirectX12;

public static class Extensions
{
    extension(GraphicsContext)
    {
        public static GraphicsContext CreateDirectX12(bool useValidationLayer)
        {
            return new DXGraphicsContext(useValidationLayer);
        }
    }

    extension(int result)
    {
        internal void Success()
        {
            if (result is not 0)
            {
                Debug.WriteLine($"DirectX call failed with error code: {result}");
            }
        }

        internal bool IsSuccess()
        {
            return result is 0;
        }
    }

    extension(Buffer buffer)
    {
        internal DXBuffer DirectX12()
        {
            return (DXBuffer)buffer;
        }
    }

    extension(BufferView bufferView)
    {
        internal DXBufferView DirectX12()
        {
            return (DXBufferView)bufferView;
        }
    }

    extension(Texture texture)
    {
        internal DXTexture DirectX12()
        {
            return (DXTexture)texture;
        }
    }

    extension(TextureView textureView)
    {
        internal DXTextureView DirectX12()
        {
            return (DXTextureView)textureView;
        }
    }

    extension(Sampler sampler)
    {
        internal DXSampler DirectX12()
        {
            return (DXSampler)sampler;
        }
    }

    extension(Shader shader)
    {
        internal DXShader DirectX12()
        {
            return (DXShader)shader;
        }
    }
}
