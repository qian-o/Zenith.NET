using Slangc.NET;

namespace Zenith.NET.Extensions.Slang;

public static class Extensions
{
    extension(GraphicsContext context)
    {
        public Shader CompileShader(string filePath, string entryPoint, ShaderStageFlags stage)
        {
            List<string> arguments =
            [
                filePath,
                "-entry", entryPoint,
                "-stage", stage.ToString().ToLowerInvariant(),
                "-matrix-layout-row-major",
                "-preserve-params"
            ];

            switch (context.Backend)
            {
                case Backend.DirectX12:
                    arguments.AddRange(["-target", "dxil"]);
                    break;

                case Backend.Metal:
                    arguments.AddRange(["-target", "metal"]);
                    break;

                case Backend.Vulkan:
                    arguments.AddRange(["-fvk-use-dx-layout", "-fvk-use-entrypoint-name", "-target", "spirv"]);
                    break;
            }

            return context.CreateShader(new() { ShaderBytes = SlangCompiler.Compile([.. arguments]), EntryPoint = entryPoint, Stage = stage });
        }

        public void ReflectShaderLayout()
        {
            throw new NotImplementedException();
        }
    }
}
