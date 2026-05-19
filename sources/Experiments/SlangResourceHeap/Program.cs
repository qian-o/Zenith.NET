using Slangc.NET;
using System.Runtime.InteropServices;

internal sealed record ShaderEntry(string Name, string Stage);

internal sealed record ShaderTest(string Name, string File, ShaderEntry[] Entries);

internal sealed record TargetConfig(string Name, string OutputExtension, string[] Arguments);

internal sealed record CompileOptions(string Target, bool UseSpvDescriptorHeapExt, bool Clean, bool ShowHelp)
{
    private static readonly HashSet<string> ValidTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "all",
        "dxil",
        "spirv",
        "metal-source",
        "metal"
    };

    public static CompileOptions Parse(string[] arguments)
    {
        string target = "all";
        bool targetSpecified = false;
        bool useSpvDescriptorHeapExt = arguments.Length == 0;
        bool clean = arguments.Length == 0;
        bool showHelp = false;

        for (int index = 0; index < arguments.Length; index++)
        {
            string argument = arguments[index];

            switch (argument)
            {
                case "-h":
                case "--help":
                    showHelp = true;
                    break;

                case "--target":
                    if (index + 1 >= arguments.Length)
                    {
                        throw new ArgumentException("Missing value for --target.");
                    }

                    target = arguments[++index];
                    targetSpecified = true;
                    break;

                case "--spv-descriptor-heap-ext":
                    useSpvDescriptorHeapExt = true;
                    break;

                case "--clean":
                    clean = true;
                    break;

                default:
                    if (argument.StartsWith('-'))
                    {
                        throw new ArgumentException($"Unknown option '{argument}'.");
                    }

                    if (targetSpecified)
                    {
                        throw new ArgumentException($"Unexpected argument '{argument}'.");
                    }

                    target = argument;
                    targetSpecified = true;
                    break;
            }
        }

        if (!ValidTargets.Contains(target))
        {
            throw new ArgumentException($"Unknown target '{target}'. Expected one of: {string.Join(", ", ValidTargets)}.");
        }

        return new(target, useSpvDescriptorHeapExt, clean, showHelp);
    }
}

internal static class Program
{
    private static readonly ShaderTest[] TestCases =
    [
        new(
            "01_cbo_texture_sampler",
            "01_cbo_texture_sampler.slang",
            [
                new("vertexMain", "vertex"),
                new("fragmentMain", "fragment")
            ]),
        new(
            "02_cbo_buffer_uav",
            "02_cbo_buffer_uav.slang",
            [
                new("computeMain", "compute")
            ]),
        new(
            "03_nonuniform_material_texture",
            "03_nonuniform_material_texture.slang",
            [
                new("vertexMain", "vertex"),
                new("fragmentMain", "fragment")
            ]),
        new(
            "04_texture_handle_array",
            "04_texture_handle_array.slang",
            [
                new("vertexMain", "vertex"),
                new("fragmentMain", "fragment")
            ])
    ];

    private static int Main(string[] arguments)
    {
        try
        {
            CompileOptions options = CompileOptions.Parse(arguments);
            if (options.ShowHelp)
            {
                PrintUsage();
                return 0;
            }

            if (arguments.Length == 0)
            {
                Console.WriteLine("No arguments supplied; running all --clean --spv-descriptor-heap-ext.");
            }

            string projectRoot = FindProjectRoot();
            string shaderDirectory = Path.Combine(projectRoot, "Shaders");
            string outputRoot = Path.Combine(projectRoot, "out");

            if (options.Clean && Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }

            foreach (string target in ExpandTargets(options.Target))
            {
                CompileTarget(shaderDirectory, outputRoot, CreateTargetConfig(target, options.UseSpvDescriptorHeapExt));
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static void CompileTarget(string shaderDirectory, string outputRoot, TargetConfig targetConfig)
    {
        string outputDirectory = Path.Combine(outputRoot, targetConfig.Name);
        Directory.CreateDirectory(outputDirectory);

        Console.WriteLine($"[{targetConfig.Name}] args: {string.Join(' ', targetConfig.Arguments)}");

        foreach (ShaderTest testCase in TestCases)
        {
            string source = Path.Combine(shaderDirectory, testCase.File);

            foreach (ShaderEntry entry in testCase.Entries)
            {
                string output = Path.Combine(outputDirectory, $"{testCase.Name}.{entry.Name}{targetConfig.OutputExtension}");
                string[] compilerArguments =
                [
                    source,
                    "-entry", entry.Name,
                    "-stage", entry.Stage,
                    "-matrix-layout-row-major",
                    .. targetConfig.Arguments
                ];

                Console.WriteLine($"[{targetConfig.Name}] {testCase.Name}::{entry.Name}");

                try
                {
                    byte[] shaderBytes = SlangCompiler.Compile(compilerArguments);
                    File.WriteAllBytes(output, shaderBytes);
                }
                catch (Exception exception) when (IsMissingDxilCompiler(targetConfig, exception))
                {
                    throw new InvalidOperationException(
                        "DXIL compilation requires DXC/dxcompiler to be available to Slang. " +
                        "On macOS, run the SPIR-V or Metal-source targets for this experiment, or install DXC and make libdxcompiler loadable before running the dxil target.",
                        exception);
                }
            }
        }
    }

    private static string[] ExpandTargets(string target)
    {
        return target.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? CreateDefaultTargets()
            : [target];
    }

    private static string[] CreateDefaultTargets()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ["dxil", "spirv", "metal-source"];
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return ["spirv", "metal-source", "metal"];
        }

        return ["spirv", "metal-source"];
    }

    private static TargetConfig CreateTargetConfig(string target, bool useSpvDescriptorHeapExt)
    {
        return target.ToLowerInvariant() switch
        {
            "dxil" => new("dxil", ".dxil", ["-target", "dxil", "-profile", "sm_6_6"]),
            "spirv" => new("spirv", ".spv", CreateSpirvArguments(useSpvDescriptorHeapExt)),
            "metal-source" => new("metal-source", ".metal", ["-target", "metal"]),
            "metal" => new("metal", ".metallib", ["-target", "metallib", "-capability", "metallib_latest", "-Xmetal", "-std=metal4.0"]),
            _ => throw new ArgumentException($"Unknown target '{target}'.")
        };
    }

    private static string[] CreateSpirvArguments(bool useSpvDescriptorHeapExt)
    {
        List<string> arguments =
        [
            "-target", "spirv",
            "-capability", "spirv_latest",
            "-fvk-use-entrypoint-name",
            "-bindless-space-index", "100"
        ];

        if (useSpvDescriptorHeapExt)
        {
            arguments.AddRange(["-capability", "spvDescriptorHeapEXT"]);
        }

        return [.. arguments];
    }

    private static bool IsMissingDxilCompiler(TargetConfig targetConfig, Exception exception)
    {
        if (!targetConfig.Name.Equals("dxil", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string message = exception.ToString();
        return message.Contains("dxc", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("dxcompiler", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("pass-through compiler", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindProjectRoot()
    {
        foreach (string seed in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? directory = new(Path.GetFullPath(seed));
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "SlangResourceHeap.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate SlangResourceHeap.csproj from the current directory or app base directory.");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: dotnet run --project SlangResourceHeap.csproj -- [all|dxil|spirv|metal-source|metal] [--spv-descriptor-heap-ext] [--clean]");
        Console.WriteLine("No arguments runs: all --clean --spv-descriptor-heap-ext.");
        Console.WriteLine("Note: all includes dxil on Windows, metallib on macOS, and skips platform downstream targets elsewhere unless requested explicitly.");
    }
}