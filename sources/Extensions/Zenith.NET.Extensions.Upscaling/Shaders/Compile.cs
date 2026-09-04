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
    string text = CreateSkeleton(pass.Name, graphicsApis, pass.Shaders);

    foreach (GraphicsApi graphicsApi in graphicsApis)
    {
        foreach (ShaderSource shader in pass.Shaders)
        {
            string constantName = GetConstantName(graphicsApi, shader);
            string shaderPath = Path.Combine(shadersDirectory, shader.FileName);

            text = CompileShader(text, graphicsApi, shaderPath, shader.FileName, constantName);
        }
    }

    WriteGeneratedFile(generatedPath, text);
}

static string CompileShader(string text, GraphicsApi graphicsApi, string shaderPath, string shaderFileName, string constantName)
{
    try
    {
        ShaderDesc shaderDesc = ZenithCompiler.CompileFromFile(graphicsApi, shaderPath, EntryPoint);
        string compiledText = ReplaceShader(text, constantName, FormatShaderDesc(constantName, shaderDesc));
        Console.WriteLine($"compiled {shaderFileName} {graphicsApi} ({shaderDesc.CodeBytes.Length} bytes, threads={shaderDesc.ThreadGroupSize.X}x{shaderDesc.ThreadGroupSize.Y}x{shaderDesc.ThreadGroupSize.Z})");
        return compiledText;
    }
    catch (Exception exception)
    {
        Console.WriteLine($"skip {shaderFileName} {graphicsApi}: {exception.Message}");
        return text;
    }
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
        StringBuilder regionBuilder = new();
        for (int shaderIndex = 0; shaderIndex < shaders.Length; shaderIndex++)
        {
            if (shaderIndex > 0)
            {
                regionBuilder.AppendLine();
                regionBuilder.AppendLine();
            }

            string constantName = GetConstantName(graphicsApi, shaders[shaderIndex]);
            ShaderDesc emptyShader = CreateEmptyShader(EntryPoint);
            regionBuilder.Append(FormatShaderDesc(constantName, emptyShader).TrimEnd('\n'));
        }

        builder.AppendLine(FormatRegion(graphicsApi.ToString(), regionBuilder.ToString()));
    }

    builder.AppendLine("}");

    return NormalizeGeneratedText(builder.ToString());
}

static string GetConstantName(GraphicsApi graphicsApi, ShaderSource shader)
{
    return $"{graphicsApi}{shader.ConstantSuffix}";
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

static ShaderDesc CreateEmptyShader(string entryPoint)
{
    return new() { Name = entryPoint, CodeBytes = [] };
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

static string GetShadersDirectory([CallerFilePath] string filePath = "")
{
    return Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();
}

file readonly struct Pass(string name, ShaderSource[] shaders)
{
    public readonly string Name = name;

    public readonly ShaderSource[] Shaders = shaders;
}

file readonly struct ShaderSource(string constantSuffix, string fileName)
{
    public readonly string ConstantSuffix = constantSuffix;

    public readonly string FileName = fileName;
}
