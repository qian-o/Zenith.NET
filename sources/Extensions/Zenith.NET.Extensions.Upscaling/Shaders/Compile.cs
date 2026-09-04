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
    string[] regionNames = [.. GetRegionNames(pass, graphicsApis)];
    ShaderDesc emptyShader = new() { Name = EntryPoint, CodeBytes = [] };
    string text = PrepareGeneratedFile(generatedPath, pass.Name, regionNames, emptyShader);

    foreach (GraphicsApi graphicsApi in graphicsApis)
    {
        foreach (ShaderSource shader in pass.Shaders)
        {
            string regionName = $"{graphicsApi}{shader.ConstantSuffix}";
            string shaderPath = Path.Combine(shadersDirectory, shader.FileName);

            text = CompileShader(text, graphicsApi, shaderPath, shader.FileName, regionName);
        }
    }

    WriteGeneratedFile(generatedPath, text);
}

static string CompileShader(string text, GraphicsApi graphicsApi, string shaderPath, string shaderFileName, string regionName)
{
    try
    {
        ShaderDesc shaderDesc = ZenithCompiler.CompileFromFile(graphicsApi, shaderPath, EntryPoint);
        string compiledText = ReplaceRegion(text, regionName, FormatShaderDesc(regionName, shaderDesc));
        Console.WriteLine($"compiled {shaderFileName} {graphicsApi} ({shaderDesc.CodeBytes.Length} bytes, threads={shaderDesc.ThreadGroupSize.X}x{shaderDesc.ThreadGroupSize.Y}x{shaderDesc.ThreadGroupSize.Z})");
        return compiledText;
    }
    catch (Exception exception)
    {
        Console.WriteLine($"skip {shaderFileName} {graphicsApi}: {exception.Message}");
        return text;
    }
}

static string PrepareGeneratedFile(string path, string passName, string[] regionNames, ShaderDesc emptyShader)
{
    string text = ReadGeneratedFile(path);
    if (!text.Contains("#region ", StringComparison.Ordinal))
    {
        return CreateSkeleton(passName, regionNames, emptyShader);
    }

    return EnsureRegions(text, regionNames, emptyShader);
}

static IEnumerable<string> GetRegionNames(Pass pass, GraphicsApi[] graphicsApis)
{
    foreach (GraphicsApi graphicsApi in graphicsApis)
    {
        foreach (ShaderSource shader in pass.Shaders)
        {
            yield return $"{graphicsApi}{shader.ConstantSuffix}";
        }
    }
}

static string CreateSkeleton(string passName, string[] regionNames, ShaderDesc emptyShader)
{
    StringBuilder builder = new();
    builder.AppendLine("namespace Zenith.NET.Extensions.Upscaling.Passes;");
    builder.AppendLine();
    builder.AppendLine($"internal partial class {passName}");
    builder.AppendLine("{");

    for (int index = 0; index < regionNames.Length; index++)
    {
        if (index > 0)
        {
            builder.AppendLine();
        }

        builder.AppendLine(FormatRegion(regionNames[index], FormatShaderDesc(regionNames[index], emptyShader)));
    }

    builder.AppendLine("}");

    return NormalizeGeneratedText(builder.ToString());
}

static string EnsureRegions(string text, string[] regionNames, ShaderDesc emptyShader)
{
    for (int index = 0; index < regionNames.Length; index++)
    {
        if (!FindRegion(text, regionNames[index], out _, out _))
        {
            text = InsertRegion(text, regionNames[index], regionNames, index, emptyShader);
        }
    }

    return text;
}

static string InsertRegion(string text, string regionName, string[] regionNames, int regionIndex, ShaderDesc emptyShader)
{
    int insertionIndex = text.LastIndexOf("\n}", StringComparison.Ordinal) + 1;

    for (int index = regionIndex + 1; index < regionNames.Length; index++)
    {
        if (FindRegion(text, regionNames[index], out int start, out _))
        {
            insertionIndex = start;
            break;
        }
    }

    string prefix = text[..insertionIndex].TrimEnd('\n');
    string suffix = text[insertionIndex..].TrimStart('\n');
    string before = prefix.EndsWith('{') ? "\n" : "\n\n";
    string after = suffix.StartsWith('}') ? "\n" : "\n\n";
    string body = FormatShaderDesc(regionName, emptyShader);
    return string.Concat(prefix, before, FormatRegion(regionName, body), after, suffix);
}

static string ReplaceRegion(string text, string regionName, string body)
{
    if (!FindRegion(text, regionName, out int start, out int end))
    {
        throw new InvalidOperationException($"Region '{regionName}' was not found.");
    }

    return string.Concat(text.AsSpan(0, start), FormatRegion(regionName, body), text.AsSpan(end));
}

static bool FindRegion(string text, string regionName, out int start, out int end)
{
    string startMarker = $"    #region {regionName}\n";
    const string EndMarker = "    #endregion";

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

static string ReadGeneratedFile(string path)
{
    if (!File.Exists(path))
    {
        return string.Empty;
    }

    return NormalizeGeneratedText(File.ReadAllText(path));
}

static string NormalizeGeneratedText(string text)
{
    string normalizedText = text.ReplaceLineEndings("\n").TrimEnd('\n');
    normalizedText = normalizedText.Replace("#endregion\n    #region", "#endregion\n\n    #region", StringComparison.Ordinal);

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
