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
                "-matrix-layout-row-major"
            ];

            if (searchPaths is not null)
            {
                foreach (string path in searchPaths)
                {
                    arguments.AddRange(["-I", path]);
                }
            }

            arguments.Add("-target");

            switch (context.Backend)
            {
                case Backend.DirectX12:
                    arguments.AddRange(["dxil", "-profile", "sm_6_6"]);
                    break;

                case Backend.Metal:
                    arguments.AddRange(["metallib", "-capability", "metallib_latest"]);
                    break;

                case Backend.Vulkan:
                    arguments.AddRange(["spirv", "-capability", "spirv_latest", "-fvk-use-entrypoint-name"]);
                    break;
            }

            return context.CreateShader(new() { ShaderBytes = SlangCompiler.Compile([.. arguments]), EntryPoint = entryPoint, Stage = stage });
        }

        public Shader LoadShaderFromSource(string source, string entryPoint, ShaderStageFlags stage, string[]? searchPaths = null)
        {
            string file = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.slang");

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
    }
}
