#:project ../../../Zenith.NET/Zenith.NET.csproj

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Zenith.NET;

const string NamespaceName = "Zenith.NET.Extensions.ImGui";
const string ClassName = "ImGuiRenderer";
const string SourceFileName = "ImGui.slang";
const string GeneratedFileName = "ImGuiRenderer.g.cs";
const int BytesPerLine = 16;
const string LineEnding = "\r\n";

GraphicsApi[] graphicsApis =
[
    GraphicsApi.DirectX12,
    GraphicsApi.Metal,
    GraphicsApi.Vulkan
];

string shadersDirectory = GetShadersDirectory();
string sourcePath = Path.Combine(shadersDirectory, SourceFileName);
string generatedPath = Path.GetFullPath(Path.Combine(shadersDirectory, "..", GeneratedFileName));
string source = File.ReadAllText(sourcePath);

ShaderDefinition[] shaders =
[
    new("Legacy", "VSMain", "0"),
    new("Legacy", "FSMain", "0"),
    new("Linear", "VSMain", "1"),
    new("Linear", "FSMain", "1")
];

string text = CreateSkeleton(graphicsApis, shaders);

foreach (GraphicsApi graphicsApi in graphicsApis)
{
    foreach (ShaderDefinition shader in shaders)
    {
        string constantName = GetConstantName(graphicsApi, shader);
        text = CompileShader(text, source, graphicsApi, shader, constantName);
    }
}

WriteGeneratedFile(generatedPath, text);

static string CompileShader(string text, string source, GraphicsApi graphicsApi, ShaderDefinition shader, string constantName)
{
    string variantSource = source.Replace("#if 0", $"#if {shader.ColorSpace}", StringComparison.Ordinal);

    try
    {
        ShaderDesc shaderDesc = ZenithCompiler.CompileFromSource(graphicsApi, variantSource, shader.EntryPoint);
        string compiledText = ReplaceShader(text, constantName, FormatShaderDesc(constantName, shaderDesc));
        Console.WriteLine($"compiled {shader.ModeName}{shader.EntryPoint} {graphicsApi} ({shaderDesc.CodeBytes.Length} bytes)");
        return compiledText;
    }
    catch (Exception exception)
    {
        Console.WriteLine($"skip {shader.ModeName}{shader.EntryPoint} {graphicsApi}: {exception.Message}");
        return text;
    }
}

static string CreateSkeleton(GraphicsApi[] graphicsApis, ShaderDefinition[] shaders)
{
    StringBuilder builder = new();
    builder.AppendLine($"namespace {NamespaceName};");
    builder.AppendLine();
    builder.AppendLine($"internal partial class {ClassName}");
    builder.AppendLine("{");

    for (int graphicsApiIndex = 0; graphicsApiIndex < graphicsApis.Length; graphicsApiIndex++)
    {
        if (graphicsApiIndex > 0)
        {
            builder.AppendLine();
        }

        GraphicsApi graphicsApi = graphicsApis[graphicsApiIndex];
        StringBuilder regionBuilder = new();
        for (int shaderIndex = 0; shaderIndex < shaders.Length; shaderIndex++)
        {
            if (shaderIndex > 0)
            {
                regionBuilder.AppendLine();
                regionBuilder.AppendLine();
            }

            string constantName = GetConstantName(graphicsApi, shaders[shaderIndex]);
            ShaderDesc emptyShader = CreateEmptyShader(shaders[shaderIndex].EntryPoint);
            regionBuilder.Append(FormatShaderDesc(constantName, emptyShader).TrimEnd('\n'));
        }

        builder.AppendLine(FormatRegion(graphicsApi.ToString(), regionBuilder.ToString()));
    }

    builder.AppendLine("}");
    return NormalizeGeneratedText(builder.ToString());
}

static string GetConstantName(GraphicsApi graphicsApi, ShaderDefinition shader)
{
    return $"{graphicsApi}{shader.ModeName}{shader.EntryPoint}";
}

static string ReplaceShader(string text, string constantName, string body)
{
    if (!FindShader(text, constantName, out int start, out int end))
    {
        throw new InvalidOperationException($"Shader '{constantName}' was not found.");
    }

    return string.Concat(text.AsSpan(0, start), body.TrimEnd('\n'), text.AsSpan(end));
}

static bool FindShader(string text, string constantName, out int start, out int end)
{
    string startMarker = $"    private static readonly ShaderDesc {constantName} = new()\n";
    const string EndMarker = "    };";

    start = text.IndexOf(startMarker, StringComparison.Ordinal);
    end = -1;
    if (start < 0)
    {
        return false;
    }

    int finishStart = text.IndexOf($"\n{EndMarker}", start + startMarker.Length, StringComparison.Ordinal);
    if (finishStart < 0)
    {
        return false;
    }

    end = finishStart + 1 + EndMarker.Length;
    return true;
}

static string FormatRegion(string regionName, string body)
{
    body = body.ReplaceLineEndings("\n").TrimEnd('\n');
    return $"    #region {regionName}\n{body}\n    #endregion";
}

static string NormalizeGeneratedText(string text)
{
    string normalizedText = text.ReplaceLineEndings("\n").TrimEnd('\n');

    int closingBraceIndex = normalizedText.LastIndexOf("\n}", StringComparison.Ordinal);
    if (closingBraceIndex < 0)
    {
        return normalizedText;
    }

    return string.Concat(normalizedText[..closingBraceIndex].TrimEnd('\n'), normalizedText[closingBraceIndex..]);
}

static void WriteGeneratedFile(string path, string text)
{
    string normalizedText = NormalizeGeneratedText(text).Replace("\n", LineEnding, StringComparison.Ordinal);
    File.WriteAllText(path, $"{normalizedText}{LineEnding}", new UTF8Encoding(true));
    Console.WriteLine($"wrote {path}");
}

static string FormatShaderDesc(string constantName, ShaderDesc shaderDesc)
{
    StringBuilder builder = new();
    builder.AppendLine($"    private static readonly ShaderDesc {constantName} = new()");
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
    return builder.ToString();
}

static void AppendCodeBytes(StringBuilder builder, byte[] codeBytes)
{
    if (codeBytes.Length == 0)
    {
        builder.AppendLine("        CodeBytes = [],");
        return;
    }

    builder.AppendLine("        CodeBytes =");
    builder.AppendLine("        [");

    for (int index = 0; index < codeBytes.Length; index += BytesPerLine)
    {
        int count = Math.Min(BytesPerLine, codeBytes.Length - index);

        builder.Append("            ");
        for (int offset = 0; offset < count; offset++)
        {
            if (offset > 0)
            {
                builder.Append(", ");
            }

            builder.Append("0x");
            builder.Append(codeBytes[index + offset].ToString("X2", CultureInfo.InvariantCulture));
        }

        if (index + count < codeBytes.Length)
        {
            builder.Append(',');
        }

        builder.AppendLine();
    }

    builder.AppendLine("        ],");
}

static ShaderDesc CreateEmptyShader(string entryPoint)
{
    return new() { Name = entryPoint, CodeBytes = [] };
}

static string GetShadersDirectory([CallerFilePath] string filePath = "")
{
    return Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();
}

file readonly struct ShaderDefinition(string modeName, string entryPoint, string colorSpace)
{
    public readonly string ModeName = modeName;

    public readonly string EntryPoint = entryPoint;

    public readonly string ColorSpace = colorSpace;
}
