using Slangc.NET;

namespace Zenith.NET;

public static class ZenithCompiler
{
    public static byte[] CompileFromFile(GraphicsApi graphicsApi, string file, string entry, string[]? searchPaths = null)
    {
        return SlangCompiler.Compile(SlangCompilerArguments(graphicsApi, file, entry, searchPaths));
    }

    public static byte[] CompileFromSource(GraphicsApi graphicsApi, string source, string entry, string[]? searchPaths = null)
    {
        return SlangCompiler.Compile(source, SlangCompilerArguments(graphicsApi, null, entry, searchPaths));
    }

    private static string[] SlangCompilerArguments(GraphicsApi graphicsApi, string? file, string entry, string[]? searchPaths)
    {
        List<string> arguments;

        if (file is null)
        {
            arguments =
            [
                "-entry", entry,
                "-matrix-layout-row-major"
            ];
        }
        else
        {
            arguments =
            [
                file,
                "-entry", entry,
                "-matrix-layout-row-major"
            ];
        }

        if (searchPaths is not null)
        {
            foreach (string searchPath in searchPaths)
            {
                arguments.AddRange(["-I", searchPath]);
            }
        }

        arguments.Add("-target");

        switch (graphicsApi)
        {
            case GraphicsApi.DirectX12:
                arguments.AddRange(["dxil", "-profile", "sm_6_6"]);
                break;

            case GraphicsApi.Metal:
                arguments.AddRange(["metallib", "-capability", "metallib_latest"]);
                break;

            case GraphicsApi.Vulkan:
                arguments.AddRange(["spirv", "-capability", "spirv_latest", "-capability", "spvDescriptorHeapEXT", "-fvk-use-entrypoint-name"]);
                break;
        }

        return [.. arguments];
    }
}
