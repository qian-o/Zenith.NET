#:project ../../../Zenith.NET.Compiler/Zenith.NET.Compiler.csproj

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Zenith.NET;

const string NamespaceName = "Zenith.NET.Extensions.Upscaling.FSR";
const string ClassName = "FSRRayReconstruction";
const string SourceFileName = "FSRRayReconstruction.slang";
const string GeneratedFileName = "FSRRayReconstruction.g.cs";
const int BytesPerLine = 16;
const string LineEnding = "\r\n";

string shadersDirectory = GetShadersDirectory();
string sourcePath = Path.Combine(shadersDirectory, SourceFileName);
string generatedPath = Path.GetFullPath(Path.Combine(shadersDirectory, "..", "FSR", GeneratedFileName));
string source = File.ReadAllText(sourcePath);

string[] entryPoints =
[
    "Prepare",
    "Compose"
];

string text = File.Exists(generatedPath) ? NormalizeGeneratedText(File.ReadAllText(generatedPath)) : CreateSkeleton(entryPoints);

foreach (string entryPoint in entryPoints)
{
    text = CompileShader(text, source, entryPoint);
}

WriteGeneratedFile(generatedPath, text);

static string CompileShader(string generatedText, string source, string entryPoint)
{
    ShaderDesc shaderDesc;

    try
    {
        shaderDesc = ZenithCompiler.CompileFromSource(GraphicsApi.DirectX12, source, entryPoint);
    }
    catch (Exception)
    {
        Console.WriteLine($"skip DirectX12 {entryPoint}");

        return generatedText;
    }

    string fieldName = GetShaderFieldName(entryPoint);

    return ReplaceShader(generatedText, fieldName, FormatShaderDesc(fieldName, shaderDesc));
}

static string CreateSkeleton(string[] entryPoints)
{
    StringBuilder builder = new();

    builder.AppendLine($"namespace {NamespaceName};");
    builder.AppendLine();
    builder.AppendLine($"internal partial class {ClassName}");
    builder.AppendLine("{");
    builder.AppendLine("    #region DirectX12");

    for (int i = 0; i < entryPoints.Length; i++)
    {
        if (i > 0)
        {
            builder.AppendLine();
        }

        string fieldName = GetShaderFieldName(entryPoints[i]);
        ShaderDesc emptyShader = CreateEmptyShader(entryPoints[i]);

        builder.AppendLine(FormatShaderDesc(fieldName, emptyShader));
    }

    builder.AppendLine("    #endregion");
    builder.Append('}');

    return NormalizeGeneratedText(builder.ToString());
}

static string GetShaderFieldName(string entryPoint)
{
    return $"DirectX12{entryPoint}";
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
