using Slangc.NET;

namespace Zenith.NET;

public static class ZenithCompiler
{
    public static ShaderDesc CompileFromFile(GraphicsApi graphicsApi, string file, string name, string[]? searchPaths = null)
    {
        return new()
        {
            Name = name,
            CodeBytes = SlangCompiler.CompileWithReflection([file, .. Arguments(graphicsApi, name, searchPaths)], out SlangReflection reflection),
            ThreadGroupSize = ThreadGroupSize(reflection, name)
        };
    }

    public static ShaderDesc CompileFromSource(GraphicsApi graphicsApi, string source, string name, string[]? searchPaths = null)
    {
        return new()
        {
            Name = name,
            CodeBytes = SlangCompiler.CompileWithReflection(source, Arguments(graphicsApi, name, searchPaths), out SlangReflection reflection),
            ThreadGroupSize = ThreadGroupSize(reflection, name)
        };
    }

    private static string[] Arguments(GraphicsApi graphicsApi, string name, string[]? searchPaths)
    {
        List<string> arguments =
        [
            "-entry", name,
            "-matrix-layout-row-major"
        ];

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
                arguments.AddRange(["metallib", "-capability", "metallib_latest", "-Xmetal", "-std=metal4.0"]);
                break;

            case GraphicsApi.Vulkan:
                arguments.AddRange(["spirv", "-capability", "spirv_latest", "-capability", "spvDescriptorHeapEXT", "-capability", "spvRayQueryKHR", "-fvk-use-entrypoint-name"]);
                break;
        }

        return [.. arguments];
    }

    private static ThreadGroupSize ThreadGroupSize(SlangReflection reflection, string name)
    {
        if (reflection.EntryPoints.FirstOrDefault(p => p.Name == name) is SlangEntryPoint entryPoint && entryPoint.ThreadGroupSize.Length is 3)
        {
            return new()
            {
                X = entryPoint.ThreadGroupSize[0],
                Y = entryPoint.ThreadGroupSize[1],
                Z = entryPoint.ThreadGroupSize[2]
            };
        }

        return new();
    }
}
