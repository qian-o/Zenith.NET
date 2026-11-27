#:project sources/Zenith.NET.Compiler/Zenith.NET.Compiler.csproj

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Zenith.NET;

const int BytesPerLine = 16;

string rootDirectory = GetRootDirectory();
string sourcesDirectory = Path.Combine(rootDirectory, "sources");

foreach (string projectFile in Directory.EnumerateFiles(sourcesDirectory, "*.csproj", SearchOption.AllDirectories).OrderBy(static path => path, StringComparer.Ordinal))
{
    string projectDirectory = Path.GetDirectoryName(projectFile)!;
    string projectName = Path.GetFileNameWithoutExtension(projectFile);
    string shadersDirectory = Path.Combine(projectDirectory, "Shaders");
    string configPath = Path.Combine(shadersDirectory, "shaders.json");

    if (!File.Exists(configPath))
    {
        continue;
    }

    if (args.Length > 0 && !MatchesFilter(args, projectName, configPath))
    {
        continue;
    }

    string rootNamespace = GetRootNamespace(projectFile, projectName);

    foreach (TargetDefinition target in LoadTargets(configPath, projectDirectory, rootNamespace))
    {
        ProcessTarget(projectName, projectDirectory, shadersDirectory, target);
    }
}

static string GetRootDirectory([CallerFilePath] string filePath = "")
{
    return Path.GetDirectoryName(filePath)!;
}

static bool MatchesFilter(string[] filters, string projectName, string configPath)
{
    foreach (string filter in filters)
    {
        if (projectName.Contains(filter, StringComparison.OrdinalIgnoreCase) || configPath.Contains(filter, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

static string GetRootNamespace(string projectFile, string projectName)
{
    XDocument document = XDocument.Load(projectFile);
    string? rootNamespace = document.Root?.Elements("PropertyGroup").Elements("RootNamespace").FirstOrDefault()?.Value.Trim();

    return string.IsNullOrEmpty(rootNamespace) ? projectName : rootNamespace;
}

static TargetDefinition[] LoadTargets(string configPath, string projectDirectory, string rootNamespace)
{
    TargetConfig[] configs = JsonSerializer.Deserialize(File.ReadAllText(configPath), ShaderConfigJsonContext.Default.TargetConfigArray)
        ?? throw new InvalidOperationException($"Failed to deserialize '{configPath}'.");

    List<TargetDefinition> targets = [];
    HashSet<string> outputPaths = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    foreach (TargetConfig config in configs)
    {
        TargetDefinition target = ResolveTarget(config, configPath, projectDirectory, rootNamespace);
        if (!outputPaths.Add(target.OutputPath))
        {
            throw new InvalidOperationException($"Duplicate output path '{target.OutputPath}' in '{configPath}'.");
        }

        targets.Add(target);
    }

    return [.. targets];
}

static TargetDefinition ResolveTarget(TargetConfig config, string configPath, string projectDirectory, string rootNamespace)
{
    if (string.IsNullOrWhiteSpace(config.Output))
    {
        throw new InvalidOperationException($"Target in '{configPath}' is missing 'output'.");
    }

    string outputPath = Path.GetFullPath(Path.Combine(projectDirectory, config.Output));
    string className = string.IsNullOrWhiteSpace(config.ClassName) ? InferClassName(outputPath) : config.ClassName;
    string @namespace = string.IsNullOrWhiteSpace(config.Namespace) ? InferNamespace(projectDirectory, outputPath, rootNamespace) : config.Namespace;
    string accessibility = string.IsNullOrWhiteSpace(config.Accessibility) ? "internal" : config.Accessibility;
    GraphicsApi[] defaultApis = config.Apis is { Length: > 0 } ? config.Apis : Enum.GetValues<GraphicsApi>();

    if (config.Shaders is null or { Length: 0 })
    {
        throw new InvalidOperationException($"Target '{className}' is missing 'shaders'.");
    }

    List<ShaderDefinition> shaders = [];
    HashSet<string> shaderNames = new(StringComparer.Ordinal);

    foreach (ShaderConfig shaderConfig in config.Shaders)
    {
        string file = shaderConfig.File ?? config.File ?? throw new InvalidOperationException($"Shader in target '{className}' is missing 'file'.");
        if (string.IsNullOrWhiteSpace(file))
        {
            throw new InvalidOperationException($"Shader in target '{className}' has an empty 'file'.");
        }

        if (string.IsNullOrWhiteSpace(shaderConfig.EntryPoint))
        {
            throw new InvalidOperationException($"Shader in target '{className}' is missing 'entryPoint'.");
        }

        string name = string.IsNullOrWhiteSpace(shaderConfig.Name) ? shaderConfig.EntryPoint : shaderConfig.Name;
        if (!shaderNames.Add(name))
        {
            throw new InvalidOperationException($"Duplicate shader name '{name}' in target '{className}'.");
        }

        GraphicsApi[] shaderApis = [.. (shaderConfig.Apis ?? defaultApis).Distinct()];
        if (shaderApis.Length is 0)
        {
            throw new InvalidOperationException($"Shader '{name}' in target '{className}' has no target APIs.");
        }

        Dictionary<string, string>? preprocessorMacros = MergePreprocessorMacros(config.PreprocessorMacros, shaderConfig.PreprocessorMacros);

        shaders.Add(new(name, file, shaderConfig.EntryPoint, shaderApis, preprocessorMacros));
    }

    return new(@namespace, className, accessibility, outputPath, [.. shaders]);
}

static string InferClassName(string outputPath)
{
    string fileName = Path.GetFileName(outputPath);
    int dotIndex = fileName.IndexOf('.', StringComparison.Ordinal);
    string className = dotIndex > 0 ? fileName[..dotIndex] : Path.GetFileNameWithoutExtension(outputPath);

    if (string.IsNullOrWhiteSpace(className))
    {
        throw new InvalidOperationException($"Cannot infer class name from output '{outputPath}'.");
    }

    return className;
}

static string InferNamespace(string projectDirectory, string outputPath, string rootNamespace)
{
    string outputDirectory = Path.GetDirectoryName(outputPath)!;
    string relativeDirectory = Path.GetRelativePath(projectDirectory, outputDirectory);

    if (relativeDirectory is "." || relativeDirectory.StartsWith("..", StringComparison.Ordinal))
    {
        return rootNamespace;
    }

    string[] segments = relativeDirectory.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

    return segments.Length is 0 ? rootNamespace : $"{rootNamespace}.{string.Join('.', segments)}";
}

static Dictionary<string, string>? MergePreprocessorMacros(Dictionary<string, string>? baseMacros, Dictionary<string, string>? overrideMacros)
{
    if (baseMacros is null && overrideMacros is null)
    {
        return null;
    }

    Dictionary<string, string> merged = new(StringComparer.Ordinal);

    if (baseMacros is not null)
    {
        foreach ((string key, string value) in baseMacros)
        {
            merged[key] = value;
        }
    }

    if (overrideMacros is not null)
    {
        foreach ((string key, string value) in overrideMacros)
        {
            merged[key] = value;
        }
    }

    return merged.Count is 0 ? null : merged;
}

static void ProcessTarget(string projectName, string projectDirectory, string shadersDirectory, TargetDefinition target)
{
    Dictionary<string, ShaderDesc> existing = ParseExistingShaders(target.OutputPath);
    Dictionary<string, ShaderDesc> compiled = new(StringComparer.Ordinal);

    foreach (ShaderDefinition shader in target.Shaders)
    {
        string filePath = Path.GetFullPath(Path.Combine(shadersDirectory, shader.File));

        foreach (GraphicsApi graphicsApi in shader.Apis)
        {
            string fieldName = $"{graphicsApi}{shader.Name}";

            try
            {
                compiled[fieldName] = ZenithCompiler.CompileFromFile(graphicsApi, filePath, shader.EntryPoint, shader.PreprocessorMacros, [shadersDirectory]);

                Console.WriteLine($"[{projectName}] compiled {graphicsApi} {shader.Name}");
            }
            catch (Exception)
            {
                if (existing.TryGetValue(fieldName, out ShaderDesc preserved))
                {
                    compiled[fieldName] = preserved;

                    Console.WriteLine($"[{projectName}] skipped {graphicsApi} {shader.Name} (preserved)");
                }
                else
                {
                    compiled[fieldName] = new()
                    {
                        Bytecode = [],
                        EntryPoint = shader.EntryPoint
                    };

                    Console.WriteLine($"[{projectName}] skipped {graphicsApi} {shader.Name} (empty)");
                }
            }
        }
    }

    string generated = GenerateCode(target, compiled);

    Directory.CreateDirectory(Path.GetDirectoryName(target.OutputPath)!);
    File.WriteAllText(target.OutputPath, generated, new UTF8Encoding(true));

    string relativeOutput = Path.GetRelativePath(projectDirectory, target.OutputPath).Replace('\\', '/');
    Console.WriteLine($"[{projectName}] wrote {relativeOutput}");
}

static Dictionary<string, ShaderDesc> ParseExistingShaders(string outputPath)
{
    Dictionary<string, ShaderDesc> result = new(StringComparer.Ordinal);

    if (!File.Exists(outputPath))
    {
        return result;
    }

    string text = File.ReadAllText(outputPath).ReplaceLineEndings("\n");

    foreach (Match match in ShaderRegex.Field().Matches(text))
    {
        string field = match.Groups["field"].Value;
        string entryPoint = match.Groups["entryPoint"].Value;
        string bytesSection = match.Groups["bytes"].Value;
        uint x = match.Groups["x"].Success ? uint.Parse(match.Groups["x"].Value, CultureInfo.InvariantCulture) : 0;
        uint y = match.Groups["y"].Success ? uint.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture) : 0;
        uint z = match.Groups["z"].Success ? uint.Parse(match.Groups["z"].Value, CultureInfo.InvariantCulture) : 0;

        List<byte> bytes = [];
        foreach (Match hexMatch in ShaderRegex.HexByte().Matches(bytesSection))
        {
            bytes.Add(byte.Parse(hexMatch.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }

        result[field] = new()
        {
            Bytecode = [.. bytes],
            EntryPoint = entryPoint,
            ThreadGroup = new()
            {
                X = x,
                Y = y,
                Z = z
            }
        };
    }

    return result;
}

static string GenerateCode(TargetDefinition target, Dictionary<string, ShaderDesc> compiled)
{
    StringBuilder builder = new();

    builder.AppendLine($"namespace {target.Namespace};");
    builder.AppendLine();
    builder.AppendLine($"{target.Accessibility} partial class {target.ClassName}");
    builder.AppendLine("{");

    for (int i = 0; i < target.Shaders.Length; i++)
    {
        if (i > 0)
        {
            builder.AppendLine();
        }

        ShaderDefinition shader = target.Shaders[i];

        builder.AppendLine($"    public static ShaderDesc {shader.Name}(GraphicsApi graphicsApi)");
        builder.AppendLine("    {");
        builder.AppendLine("        return graphicsApi switch");
        builder.AppendLine("        {");

        foreach (GraphicsApi graphicsApi in shader.Apis)
        {
            builder.AppendLine($"            GraphicsApi.{graphicsApi} => {graphicsApi}{shader.Name},");
        }

        builder.AppendLine("            _ => default");
        builder.AppendLine("        };");
        builder.AppendLine("    }");
    }

    foreach (GraphicsApi graphicsApi in Enum.GetValues<GraphicsApi>())
    {
        ShaderDefinition[] apiShaders = [.. target.Shaders.Where(shader => shader.Apis.Contains(graphicsApi))];

        if (apiShaders.Length is 0)
        {
            continue;
        }

        builder.AppendLine();
        builder.AppendLine($"    #region {graphicsApi}");

        for (int i = 0; i < apiShaders.Length; i++)
        {
            if (i > 0)
            {
                builder.AppendLine();
            }

            string fieldName = $"{graphicsApi}{apiShaders[i].Name}";
            AppendShaderDesc(builder, fieldName, compiled[fieldName]);
        }

        builder.AppendLine("    #endregion");
    }

    builder.AppendLine("}");

    return builder.ToString().ReplaceLineEndings("\n");
}

static void AppendShaderDesc(StringBuilder builder, string fieldName, ShaderDesc shaderDesc)
{
    bool hasThreadGroup = shaderDesc.ThreadGroup.X is not 0 || shaderDesc.ThreadGroup.Y is not 0 || shaderDesc.ThreadGroup.Z is not 0;

    builder.AppendLine($"    private static readonly ShaderDesc {fieldName} = new()");
    builder.AppendLine("    {");

    AppendBytecode(builder, shaderDesc.Bytecode);

    if (hasThreadGroup)
    {
        builder.AppendLine($"        EntryPoint = \"{shaderDesc.EntryPoint}\",");
        builder.AppendLine("        ThreadGroup = new()");
        builder.AppendLine("        {");
        builder.AppendLine($"            X = {shaderDesc.ThreadGroup.X},");
        builder.AppendLine($"            Y = {shaderDesc.ThreadGroup.Y},");
        builder.AppendLine($"            Z = {shaderDesc.ThreadGroup.Z}");
        builder.AppendLine("        }");
    }
    else
    {
        builder.AppendLine($"        EntryPoint = \"{shaderDesc.EntryPoint}\"");
    }

    builder.AppendLine("    };");
}

static void AppendBytecode(StringBuilder builder, byte[] bytecode)
{
    if (bytecode.Length is 0)
    {
        builder.AppendLine("        Bytecode = [],");

        return;
    }

    builder.AppendLine("        Bytecode =");
    builder.AppendLine("        [");

    for (int i = 0; i < bytecode.Length; i += BytesPerLine)
    {
        int count = Math.Min(BytesPerLine, bytecode.Length - i);

        builder.Append("            ");

        for (int j = 0; j < count; j++)
        {
            if (j > 0)
            {
                builder.Append(", ");
            }

            builder.Append("0x");
            builder.Append(bytecode[i + j].ToString("X2", CultureInfo.InvariantCulture));
        }

        if (i + count < bytecode.Length)
        {
            builder.Append(',');
        }

        builder.AppendLine();
    }

    builder.AppendLine("        ],");
}

internal class TargetConfig
{
    public string? Namespace { get; init; }

    public string? ClassName { get; init; }

    public string? Accessibility { get; init; }

    public required string Output { get; init; }

    public string? File { get; init; }

    public GraphicsApi[]? Apis { get; init; }

    public Dictionary<string, string>? PreprocessorMacros { get; init; }

    public required ShaderConfig[] Shaders { get; init; }
}

internal class ShaderConfig
{
    public string? Name { get; init; }

    public string? File { get; init; }

    public required string EntryPoint { get; init; }

    public GraphicsApi[]? Apis { get; init; }

    public Dictionary<string, string>? PreprocessorMacros { get; init; }
}

file readonly struct TargetDefinition(string @namespace,
                                      string className,
                                      string accessibility,
                                      string outputPath,
                                      ShaderDefinition[] shaders)
{
    public readonly string Namespace = @namespace;

    public readonly string ClassName = className;

    public readonly string Accessibility = accessibility;

    public readonly string OutputPath = outputPath;

    public readonly ShaderDefinition[] Shaders = shaders;
}

file readonly struct ShaderDefinition(string name,
                                      string file,
                                      string entryPoint,
                                      GraphicsApi[] apis,
                                      Dictionary<string, string>? preprocessorMacros)
{
    public readonly string Name = name;

    public readonly string File = file;

    public readonly string EntryPoint = entryPoint;

    public readonly GraphicsApi[] Apis = apis;

    public readonly Dictionary<string, string>? PreprocessorMacros = preprocessorMacros;
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true,
                             ReadCommentHandling = JsonCommentHandling.Skip,
                             AllowTrailingCommas = true,
                             UseStringEnumConverter = true)]
[JsonSerializable(typeof(TargetConfig[]))]
internal partial class ShaderConfigJsonContext : JsonSerializerContext;

internal static partial class ShaderRegex
{
    [GeneratedRegex(@"private static readonly ShaderDesc (?<field>\w+) = new\(\)\s*\{\s*Bytecode =(?<bytes>.*?),\s*EntryPoint = ""(?<entryPoint>[^""]*)""(?:\s*,\s*ThreadGroup = new\(\)\s*\{\s*X = (?<x>\d+),\s*Y = (?<y>\d+),\s*Z = (?<z>\d+)\s*\})?\s*\};", RegexOptions.Singleline)]
    public static partial Regex Field();

    [GeneratedRegex(@"0x([0-9A-Fa-f]{2})")]
    public static partial Regex HexByte();
}
