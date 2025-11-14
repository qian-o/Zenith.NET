using Slangc.NET;

namespace Zenith.NET.Extensions.Slang;

public static class Extensions
{
    extension(GraphicsContext context)
    {
        public Shader LoadShaderFromFile(string file, string entryPoint, ShaderStageFlags stage, string[]? searchPaths = null)
        {
            List<string> arguments =
            [
                file,
                "-entry", entryPoint,
                "-stage", stage.ToString().ToLowerInvariant(),
                "-matrix-layout-row-major",
                "-preserve-params"
            ];

            if (searchPaths is not null)
            {
                foreach (string path in searchPaths)
                {
                    arguments.AddRange(["-I", path]);
                }
            }

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

        public Shader LoadShaderFromSource(string source, string entryPoint, ShaderStageFlags stage, string[]? searchPaths = null)
        {
            string file = Path.GetTempFileName();

            File.WriteAllText(file, source);

            try
            {
                return context.LoadShaderFromFile(file, entryPoint, stage, searchPaths);
            }
            finally
            {
                File.Delete(file);
            }
        }

        public void ReflectShaderLayout()
        {
            throw new NotImplementedException();
        }
    }
}
