#:project ../../../Zenith.NET.Compiler/Zenith.NET.Compiler.csproj

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Zenith.NET;

const string NamespaceName = "Zenith.NET.Extensions.Upscaling";
const string ClassName = "FrameGenerationSwapChain";
const string SourceFileName = "FrameGenerationSwapChain.slang";
const string GeneratedFileName = "FrameGenerationSwapChain.g.cs";
const int BytesPerLine = 16;
const string LineEnding = "\n";

GraphicsApi[] graphicsApis =
[
    GraphicsApi.DirectX12,
    GraphicsApi.Metal,
    GraphicsApi.Vulkan
];

string[] entryPoints =
[
    "CaptureInputs",
    "FullscreenVS",
    "CaptureColorFS"
];

string shadersDirectory = GetShadersDirectory();
string sourcePath = Path.Combine(shadersDirectory, SourceFileName);
string generatedPath = Path.GetFullPath(Path.Combine(shadersDirectory, "..", GeneratedFileName));

string text = File.Exists(generatedPath) ? NormalizeGeneratedText(File.ReadAllText(generatedPath)) : CreateSkeleton(graphicsApis, entryPoints);

foreach (GraphicsApi graphicsApi in graphicsApis)
{
    foreach (string entryPoint in entryPoints)
    {
        text = CompileShader(text, sourcePath, graphicsApi, entryPoint);
    }
}

WriteGeneratedFile(generatedPath, text);

static string CompileShader(string generatedText, string sourcePath, GraphicsApi graphicsApi, string entryPoint)
{
    ShaderDesc shaderDesc;

    try
    {
        shaderDesc = ZenithCompiler.CompileFromFile(graphicsApi, sourcePath, entryPoint);
    }
    catch (Exception)
    {
        Console.WriteLine($"skip {graphicsApi} {entryPoint}");

        return generatedText;
    }

    string fieldName = GetShaderFieldName(graphicsApi, entryPoint);

    return ReplaceShader(generatedText, fieldName, FormatShaderDesc(fieldName, shaderDesc));
}

static string CreateSkeleton(GraphicsApi[] graphicsApis, string[] entryPoints)
{
    StringBuilder builder = new();

    builder.AppendLine($"namespace {NamespaceName};");
    builder.AppendLine();
    builder.AppendLine($"public partial class {ClassName}");
    builder.AppendLine("{");

    for (int graphicsApiIndex = 0; graphicsApiIndex < graphicsApis.Length; graphicsApiIndex++)
    {
        if (graphicsApiIndex > 0)
        {
            builder.AppendLine();
        }

        GraphicsApi graphicsApi = graphicsApis[graphicsApiIndex];

        builder.AppendLine($"    #region {graphicsApi}");

        for (int shaderIndex = 0; shaderIndex < entryPoints.Length; shaderIndex++)
        {
            if (shaderIndex > 0)
            {
                builder.AppendLine();
            }

            string entryPoint = entryPoints[shaderIndex];
            string fieldName = GetShaderFieldName(graphicsApi, entryPoint);
            ShaderDesc emptyShader = CreateEmptyShader(entryPoint);

            builder.AppendLine(FormatShaderDesc(fieldName, emptyShader));
        }

        builder.AppendLine("    #endregion");
    }

    builder.Append('}');

    return NormalizeGeneratedText(builder.ToString());
}

static string GetShaderFieldName(GraphicsApi graphicsApi, string entryPoint)
{
    return $"{graphicsApi}{entryPoint}";
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
