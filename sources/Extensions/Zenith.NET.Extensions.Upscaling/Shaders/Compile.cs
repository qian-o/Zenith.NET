#:project ../../../Zenith.NET/Zenith.NET.csproj

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Zenith.NET;

const string EntryPoint = "Main";
const int BytesPerLine = 16;
const string LineEnding = "\r\n";

GraphicsApi[] graphicsApis =
[
    GraphicsApi.DirectX12,
    GraphicsApi.Metal,
    GraphicsApi.Vulkan
];

string shadersDirectory = GetShadersDirectory();
string passesDirectory = Path.GetFullPath(Path.Combine(shadersDirectory, "..", "Passes"));

Pass[] passes =
[
    new("Sgsr1Pass", [new("Main", "Sgsr1.slang")]),
    new("Sgsr2ConvertPass", [new("SpeedMain", "Sgsr2ConvertSpeed.slang"), new("QualityMain", "Sgsr2ConvertQuality.slang")]),
    new("Sgsr2ActivatePass", [new("Main", "Sgsr2Activate.slang")]),
    new("Sgsr2UpscalePass", [new("SpeedMain", "Sgsr2UpscaleSpeed.slang"), new("QualityMain", "Sgsr2UpscaleQuality.slang")])
];

foreach (Pass pass in passes)
{
    CompilePass(pass, graphicsApis, shadersDirectory, passesDirectory);
}

static void CompilePass(Pass pass, GraphicsApi[] graphicsApis, string shadersDirectory, string passesDirectory)
{
    string generatedPath = Path.Combine(passesDirectory, $"{pass.Name}.g.cs");
    string text = File.Exists(generatedPath) ? NormalizeGeneratedText(File.ReadAllText(generatedPath)) : CreateSkeleton(pass.Name, graphicsApis, pass.Shaders);

    foreach (GraphicsApi graphicsApi in graphicsApis)
    {
        foreach (ShaderSource shader in pass.Shaders)
        {
            text = CompileShader(text, graphicsApi, shadersDirectory, shader);
        }
    }

    WriteGeneratedFile(generatedPath, text);
}

static string CompileShader(string generatedText, GraphicsApi graphicsApi, string shadersDirectory, ShaderSource shader)
{
    ShaderDesc shaderDesc;

    try
    {
        shaderDesc = ZenithCompiler.CompileFromFile(graphicsApi, Path.Combine(shadersDirectory, shader.FileName), EntryPoint);
    }
    catch (Exception)
    {
        Console.WriteLine($"skip {graphicsApi} {shader.FileName}");

        return generatedText;
    }

    string fieldName = GetShaderFieldName(graphicsApi, shader);

    return ReplaceShader(generatedText, fieldName, FormatShaderDesc(fieldName, shaderDesc));
}

static string CreateSkeleton(string passName, GraphicsApi[] graphicsApis, ShaderSource[] shaders)
{
    StringBuilder builder = new();

    builder.AppendLine("namespace Zenith.NET.Extensions.Upscaling.Passes;");
    builder.AppendLine();
    builder.AppendLine($"internal partial class {passName}");
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
            ShaderDesc emptyShader = CreateEmptyShader(EntryPoint);

            builder.AppendLine(FormatShaderDesc(fieldName, emptyShader));
        }

        builder.AppendLine("    #endregion");
    }

    builder.Append('}');

    return NormalizeGeneratedText(builder.ToString());
}

static string GetShaderFieldName(GraphicsApi graphicsApi, ShaderSource shader)
{
    return $"{graphicsApi}{shader.FieldNameSuffix}";
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

static ShaderDesc CreateEmptyShader(string entryPoint)
{
    return new()
    {
        Name = entryPoint,
        CodeBytes = []
    };
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

static string GetShadersDirectory([CallerFilePath] string filePath = "")
{
    return Path.GetDirectoryName(filePath)!;
}

file readonly struct Pass(string name, ShaderSource[] shaders)
{
    public readonly string Name = name;

    public readonly ShaderSource[] Shaders = shaders;
}

file readonly struct ShaderSource(string fieldNameSuffix, string fileName)
{
    public readonly string FieldNameSuffix = fieldNameSuffix;

    public readonly string FileName = fileName;
}
