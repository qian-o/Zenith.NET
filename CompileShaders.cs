#:project sources/Zenith.NET.Compiler/Zenith.NET.Compiler.csproj

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Zenith.NET;

const int BytesPerLine = 16;

string rootDirectory = GetRootDirectory();
string sourcesDirectory = Path.Combine(rootDirectory, "sources");

string[] projectFiles = [.. Directory.EnumerateFiles(sourcesDirectory, "*.csproj", SearchOption.AllDirectories).OrderBy(static path => path, StringComparer.Ordinal)];

foreach (string projectFile in projectFiles)
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

    TargetDefinition[] targets = ParseConfig(configPath);

    foreach (TargetDefinition target in targets)
    {
        ProcessTarget(projectName, shadersDirectory, target);
    }
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

static void ProcessTarget(string projectName, string shadersDirectory, TargetDefinition target)
{
    string outputPath = Path.GetFullPath(Path.Combine(shadersDirectory, target.Output));
    Dictionary<string, ShaderDesc> existing = ParseExistingShaders(outputPath);
    Dictionary<string, ShaderDesc> compiled = new(StringComparer.Ordinal);
    List<GraphicsApi> usedApis = [];

    foreach (ShaderDefinition shader in target.Shaders)
    {
        string sourcePath = Path.GetFullPath(Path.Combine(shadersDirectory, shader.Source));

        foreach (GraphicsApi graphicsApi in shader.Apis)
        {
            if (!usedApis.Contains(graphicsApi))
            {
                usedApis.Add(graphicsApi);
            }

            string fieldName = $"{graphicsApi}{shader.Name}";

            try
            {
                compiled[fieldName] = ZenithCompiler.CompileFromFile(graphicsApi, sourcePath, shader.EntryPoint, shader.PreprocessorMacros, [shadersDirectory]);

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
                        Name = shader.EntryPoint,
                        CodeBytes = []
                    };

                    Console.WriteLine($"[{projectName}] skipped {graphicsApi} {shader.Name} (empty)");
                }
            }
        }
    }

    List<GraphicsApi> orderedApis = [.. target.Apis.Where(usedApis.Contains)];
    foreach (GraphicsApi graphicsApi in usedApis)
    {
        if (!orderedApis.Contains(graphicsApi))
        {
            orderedApis.Add(graphicsApi);
        }
    }

    string generated = GenerateCode(target, orderedApis, compiled);

    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    File.WriteAllText(outputPath, generated, new UTF8Encoding(true));

    Console.WriteLine($"[{projectName}] wrote {Path.GetFileName(outputPath)}");
}

static TargetDefinition[] ParseConfig(string configPath)
{
    JsonDocumentOptions options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(configPath), options);
    JsonElement root = document.RootElement;

    if (root.ValueKind is JsonValueKind.Array)
    {
        List<TargetDefinition> targets = [];
        foreach (JsonElement element in root.EnumerateArray())
        {
            targets.Add(ParseTarget(element));
        }

        return [.. targets];
    }

    if (root.ValueKind is JsonValueKind.Object)
    {
        return [ParseTarget(root)];
    }

    throw new InvalidOperationException($"Invalid shaders.json root in '{configPath}'.");
}

static TargetDefinition ParseTarget(JsonElement element)
{
    string @namespace = GetRequiredString(element, "namespace");
    string className = GetRequiredString(element, "className");
    string accessibility = GetOptionalString(element, "accessibility") ?? "internal";
    string output = GetOptionalString(element, "output") ?? $"../{className}.g.cs";
    GraphicsApi[] apis = ParseApis(element) ?? Enum.GetValues<GraphicsApi>();

    if (!TryGetPropertyIgnoreCase(element, "shaders", out JsonElement shadersElement) || shadersElement.ValueKind is not JsonValueKind.Array)
    {
        throw new InvalidOperationException($"Target '{className}' is missing 'shaders' array.");
    }

    List<ShaderDefinition> shaders = [];
    HashSet<string> shaderNames = new(StringComparer.Ordinal);
    foreach (JsonElement shaderElement in shadersElement.EnumerateArray())
    {
        string source = GetRequiredString(shaderElement, "source");
        string entryPoint = GetRequiredString(shaderElement, "entryPoint");
        string name = GetOptionalString(shaderElement, "name") ?? entryPoint;
        if (!shaderNames.Add(name))
        {
            throw new InvalidOperationException($"Duplicate shader name '{name}' in target '{className}'.");
        }

        GraphicsApi[] shaderApis = ParseApis(shaderElement) ?? apis;
        Dictionary<string, string>? preprocessorMacros = ParsePreprocessorMacros(shaderElement);

        shaders.Add(new(name, source, entryPoint, shaderApis, preprocessorMacros));
    }

    return new(@namespace, className, accessibility, output, apis, [.. shaders]);
}

static GraphicsApi[]? ParseApis(JsonElement element)
{
    if (!TryGetPropertyIgnoreCase(element, "apis", out JsonElement apisElement))
    {
        return null;
    }

    if (apisElement.ValueKind is not JsonValueKind.Array)
    {
        throw new InvalidOperationException("'apis' must be an array.");
    }

    List<GraphicsApi> apis = [];
    foreach (JsonElement apiElement in apisElement.EnumerateArray())
    {
        string value = apiElement.GetString() ?? throw new InvalidOperationException("API name cannot be null.");
        apis.Add(Enum.Parse<GraphicsApi>(value, ignoreCase: true));
    }

    return [.. apis];
}

static Dictionary<string, string>? ParsePreprocessorMacros(JsonElement element)
{
    if (!TryGetPropertyIgnoreCase(element, "preprocessorMacros", out JsonElement macrosElement))
    {
        return null;
    }

    Dictionary<string, string> preprocessorMacros = new(StringComparer.Ordinal);

    switch (macrosElement.ValueKind)
    {
        case JsonValueKind.Object:
            foreach (JsonProperty property in macrosElement.EnumerateObject())
            {
                string value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => property.Value.ToString(),
                    JsonValueKind.Null => string.Empty,
                    _ => throw new InvalidOperationException($"Unsupported preprocessor macro value for '{property.Name}'.")
                };

                if (!preprocessorMacros.TryAdd(property.Name, value))
                {
                    throw new InvalidOperationException($"Duplicate preprocessor macro '{property.Name}'.");
                }
            }
            break;

        case JsonValueKind.Array:
            foreach (JsonElement item in macrosElement.EnumerateArray())
            {
                string key = item.GetString() ?? throw new InvalidOperationException("Preprocessor macro name cannot be null.");
                if (!preprocessorMacros.TryAdd(key, "1"))
                {
                    throw new InvalidOperationException($"Duplicate preprocessor macro '{key}'.");
                }
            }
            break;

        default:
            throw new InvalidOperationException("'preprocessorMacros' must be an object or array.");
    }

    return preprocessorMacros.Count is 0 ? null : preprocessorMacros;
}

static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
{
    foreach (JsonProperty property in element.EnumerateObject())
    {
        if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            value = property.Value;

            return true;
        }
    }

    value = default;

    return false;
}

static string GetRequiredString(JsonElement element, string name)
{
    return GetOptionalString(element, name) ?? throw new InvalidOperationException($"Missing required property '{name}'.");
}

static string? GetOptionalString(JsonElement element, string name)
{
    if (TryGetPropertyIgnoreCase(element, name, out JsonElement value) && value.ValueKind is not JsonValueKind.Null)
    {
        return value.GetString();
    }

    return null;
}

static Dictionary<string, ShaderDesc> ParseExistingShaders(string outputPath)
{
    Dictionary<string, ShaderDesc> result = new(StringComparer.Ordinal);

    if (!File.Exists(outputPath))
    {
        return result;
    }

    string text = File.ReadAllText(outputPath).ReplaceLineEndings("\n");

    Regex fieldRegex = new(@"private static readonly ShaderDesc (?<field>\w+) = new\(\)\s*\{\s*Name = ""(?<name>[^""]*)"",\s*CodeBytes =(?<bytes>.*?),\s*ThreadGroupSize = new\(\)\s*\{\s*X = (?<x>\d+),\s*Y = (?<y>\d+),\s*Z = (?<z>\d+)\s*\}\s*\};", RegexOptions.Singleline);

    foreach (Match match in fieldRegex.Matches(text))
    {
        string field = match.Groups["field"].Value;
        string name = match.Groups["name"].Value;
        string bytesSection = match.Groups["bytes"].Value;
        uint x = uint.Parse(match.Groups["x"].Value, CultureInfo.InvariantCulture);
        uint y = uint.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture);
        uint z = uint.Parse(match.Groups["z"].Value, CultureInfo.InvariantCulture);

        List<byte> bytes = [];
        foreach (Match hexMatch in Regex.Matches(bytesSection, @"0x([0-9A-Fa-f]{2})"))
        {
            bytes.Add(byte.Parse(hexMatch.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }

        result[field] = new()
        {
            Name = name,
            CodeBytes = [.. bytes],
            ThreadGroupSize = new()
            {
                X = x,
                Y = y,
                Z = z
            }
        };
    }

    return result;
}

static string GenerateCode(TargetDefinition target, List<GraphicsApi> orderedApis, Dictionary<string, ShaderDesc> compiled)
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

    foreach (GraphicsApi graphicsApi in orderedApis)
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
    builder.AppendLine($"    private static readonly ShaderDesc {fieldName} = new()");
    builder.AppendLine("    {");
    builder.AppendLine($"        Name = \"{shaderDesc.Name}\",");

    AppendCodeBytes(builder, shaderDesc.CodeBytes);

    builder.AppendLine("        ThreadGroupSize = new()");
    builder.AppendLine("        {");
    builder.AppendLine($"            X = {shaderDesc.ThreadGroupSize.X},");
    builder.AppendLine($"            Y = {shaderDesc.ThreadGroupSize.Y},");
    builder.AppendLine($"            Z = {shaderDesc.ThreadGroupSize.Z}");
    builder.AppendLine("        }");
    builder.AppendLine("    };");
}

static void AppendCodeBytes(StringBuilder builder, byte[] codeBytes)
{
    if (codeBytes.Length is 0)
    {
        builder.AppendLine("        CodeBytes = [],");

        return;
    }

    builder.AppendLine("        CodeBytes =");
    builder.AppendLine("        [");

    for (int i = 0; i < codeBytes.Length; i += BytesPerLine)
    {
        int count = Math.Min(BytesPerLine, codeBytes.Length - i);

        builder.Append("            ");

        for (int j = 0; j < count; j++)
        {
            if (j > 0)
            {
                builder.Append(", ");
            }

            builder.Append("0x");
            builder.Append(codeBytes[i + j].ToString("X2", CultureInfo.InvariantCulture));
        }

        if (i + count < codeBytes.Length)
        {
            builder.Append(',');
        }

        builder.AppendLine();
    }

    builder.AppendLine("        ],");
}

static string GetRootDirectory([CallerFilePath] string filePath = "")
{
    return Path.GetDirectoryName(filePath)!;
}

file readonly struct TargetDefinition(string @namespace,
                                      string className,
                                      string accessibility,
                                      string output,
                                      GraphicsApi[] apis,
                                      ShaderDefinition[] shaders)
{
    public readonly string Namespace = @namespace;

    public readonly string ClassName = className;

    public readonly string Accessibility = accessibility;

    public readonly string Output = output;

    public readonly GraphicsApi[] Apis = apis;

    public readonly ShaderDefinition[] Shaders = shaders;
}

file readonly struct ShaderDefinition(string name,
                                      string source,
                                      string entryPoint,
                                      GraphicsApi[] apis,
                                      Dictionary<string, string>? preprocessorMacros)
{
    public readonly string Name = name;

    public readonly string Source = source;

    public readonly string EntryPoint = entryPoint;

    public readonly GraphicsApi[] Apis = apis;

    public readonly Dictionary<string, string>? PreprocessorMacros = preprocessorMacros;
}
