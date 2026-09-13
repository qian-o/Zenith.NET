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

string text = File.Exists(generatedPath) ? NormalizeGeneratedText(File.ReadAllText(generatedPath)) : CreateSkeleton(graphicsApis, shaders);

foreach (GraphicsApi graphicsApi in graphicsApis)
{
    foreach (ShaderDefinition shader in shaders)
    {
        text = CompileShader(text, source, graphicsApi, shader);
    }
}

WriteGeneratedFile(generatedPath, text);

static string CompileShader(string generatedText, string source, GraphicsApi graphicsApi, ShaderDefinition shader)
{
    ShaderDesc shaderDesc;

    try
    {
        shaderDesc = ZenithCompiler.CompileFromSource(graphicsApi, source.Replace("#if 0", $"#if {shader.ColorSpace}", StringComparison.Ordinal), shader.EntryPoint);
    }
    catch (Exception)
    {
        Console.WriteLine($"skip {graphicsApi} {shader.ModeName}{shader.EntryPoint}");

        return generatedText;
    }

    string fieldName = GetShaderFieldName(graphicsApi, shader);

    return ReplaceShader(generatedText, fieldName, FormatShaderDesc(fieldName, shaderDesc));
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

        builder.AppendLine($"    #region {graphicsApi}");

        for (int shaderIndex = 0; shaderIndex < shaders.Length; shaderIndex++)
        {
            if (shaderIndex > 0)
            {
                builder.AppendLine();
            }

            string fieldName = GetShaderFieldName(graphicsApi, shaders[shaderIndex]);
            ShaderDesc emptyShader = CreateEmptyShader(shaders[shaderIndex].EntryPoint);

            builder.AppendLine(FormatShaderDesc(fieldName, emptyShader));
        }

        builder.AppendLine("    #endregion");
    }

    builder.Append('}');

    return NormalizeGeneratedText(builder.ToString());
}

static string GetShaderFieldName(GraphicsApi graphicsApi, ShaderDefinition shader)
{
    return $"{graphicsApi}{shader.ModeName}{shader.EntryPoint}";
}

static string ReplaceShader(string generatedText, string fieldName, string shaderText)
{
    string startMarker = $"    private static readonly ShaderDesc {fieldName} = new()\n";
    const string EndMarker = "    };";

    int start = generatedText.IndexOf(startMarker, StringComparison.Ordinal);
    int finishStart = start < 0 ? -1 : generatedText.IndexOf($"\n{EndMarker}", start + startMarker.Length, StringComparison.Ordinal);

    if (start < 0 || finishStart < 0)
    {
        throw new InvalidOperationException($"Shader field '{fieldName}' was not found.");
    }

    int end = finishStart + 1 + EndMarker.Length;

    return string.Concat(generatedText.AsSpan(0, start), shaderText, generatedText.AsSpan(end));
}

static string NormalizeGeneratedText(string text)
{
    return text.ReplaceLineEndings("\n").TrimEnd('\n');
}

static void WriteGeneratedFile(string path, string text)
{
    File.WriteAllText(path, NormalizeGeneratedText(text).Replace("\n", LineEnding, StringComparison.Ordinal) + LineEnding, new UTF8Encoding(true));

    Console.WriteLine($"wrote {Path.GetFileName(path)}");
}

static string FormatShaderDesc(string fieldName, ShaderDesc shaderDesc)
{
    StringBuilder builder = new();

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

    return NormalizeGeneratedText(builder.ToString());
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
    return new()
    {
        Name = entryPoint,
        CodeBytes = []
    };
}

static string GetShadersDirectory([CallerFilePath] string filePath = "")
{
    return Path.GetDirectoryName(filePath)!;
}

file readonly struct ShaderDefinition(string modeName, string entryPoint, string colorSpace)
{
    public readonly string ModeName = modeName;

    public readonly string EntryPoint = entryPoint;

    public readonly string ColorSpace = colorSpace;
}
