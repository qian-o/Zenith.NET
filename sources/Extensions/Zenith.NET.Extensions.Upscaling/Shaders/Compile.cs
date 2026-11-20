#:project ../../../Zenith.NET.Compiler/Zenith.NET.Compiler.csproj

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Zenith.NET;

const string NamespaceName = "Zenith.NET.Extensions.Upscaling";
const string ClassName = "UpscalingBridge";
const string GeneratedFileName = "UpscalingBridge.g.cs";
const int BytesPerLine = 16;
const string LineEnding = "\r\n";

GraphicsApi[] graphicsApis =
[
    GraphicsApi.DirectX12,
    GraphicsApi.Metal,
    GraphicsApi.Vulkan
];

ShaderDefinition[] shaders =
[
    new("RayReconstruction.slang", "RayReconstruction", "PrepareCS", "1"),
    new("RayReconstruction.slang", "RayReconstruction", "ComposeCS", "0")
];

string shadersDirectory = GetShadersDirectory();
string generatedPath = Path.GetFullPath(Path.Combine(shadersDirectory, "..", GeneratedFileName));

string text = File.Exists(generatedPath) ? NormalizeGeneratedText(File.ReadAllText(generatedPath)) : CreateSkeleton(graphicsApis, shaders);

foreach (GraphicsApi graphicsApi in graphicsApis)
{
    foreach (ShaderDefinition shader in shaders)
    {
        string source = File.ReadAllText(Path.Combine(shadersDirectory, shader.SourceFileName));

        text = CompileShader(text, source, graphicsApi, shader);
    }
}

WriteGeneratedFile(generatedPath, text);

static string CompileShader(string generatedText, string source, GraphicsApi graphicsApi, ShaderDefinition shader)
{
    ShaderDesc shaderDesc;

    try
    {
        string shaderSource = shader.Stage is not null ? source.Replace("#if 0", $"#if {shader.Stage}", StringComparison.Ordinal) : source;
        shaderDesc = ZenithCompiler.CompileFromSource(graphicsApi, shaderSource, shader.EntryPoint);
    }
    catch (Exception)
    {
        Console.WriteLine($"skip {graphicsApi} {shader.NamePrefix}{shader.EntryPoint}");

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

    for (int i = 0; i < shaders.Length; i++)
    {
        if (i > 0)
        {
            builder.AppendLine();
        }

        ShaderDefinition shader = shaders[i];

        builder.AppendLine($"    public static ShaderDesc {shader.NamePrefix}{shader.EntryPoint}(GraphicsApi graphicsApi)");
        builder.AppendLine("    {");
        builder.AppendLine("        return graphicsApi switch");
        builder.AppendLine("        {");

        foreach (GraphicsApi graphicsApi in graphicsApis)
        {
            builder.AppendLine($"            GraphicsApi.{graphicsApi} => {GetShaderFieldName(graphicsApi, shader)},");
        }

        builder.AppendLine("            _ => default");
        builder.AppendLine("        };");
        builder.AppendLine("    }");
    }

    for (int i = 0; i < graphicsApis.Length; i++)
    {
        builder.AppendLine();

        GraphicsApi graphicsApi = graphicsApis[i];

        builder.AppendLine($"    #region {graphicsApi}");

        for (int j = 0; j < shaders.Length; j++)
        {
            if (j > 0)
            {
                builder.AppendLine();
            }

            string fieldName = GetShaderFieldName(graphicsApi, shaders[j]);
            ShaderDesc emptyShader = CreateEmptyShader(shaders[j].EntryPoint);

            builder.AppendLine(FormatShaderDesc(fieldName, emptyShader));
        }

        builder.AppendLine("    #endregion");
    }

    builder.Append('}');

    return NormalizeGeneratedText(builder.ToString());
}

static string GetShaderFieldName(GraphicsApi graphicsApi, ShaderDefinition shader)
{
    return $"{graphicsApi}{shader.NamePrefix}{shader.EntryPoint}";
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

file readonly struct ShaderDefinition(string sourceFileName, string namePrefix, string entryPoint, string? stage)
{
    public readonly string SourceFileName = sourceFileName;

    public readonly string NamePrefix = namePrefix;

    public readonly string EntryPoint = entryPoint;

    public readonly string? Stage = stage;
}
