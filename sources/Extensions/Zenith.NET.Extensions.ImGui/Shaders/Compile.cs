﻿#:property TargetFramework=net10.0
#:project ../../../Zenith.NET/Zenith.NET.csproj

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Zenith.NET;

const string NamespaceName = "Zenith.NET.Extensions.ImGui";
const string ClassName = "ImGuiRenderer";
const string SourceFileName = "ImGui.slang";
const string GeneratedFileName = "ImGuiRenderer.g.cs";

string shadersDirectory = GetShadersDirectory();
string sourcePath = Path.Combine(shadersDirectory, SourceFileName);
string generatedPath = Path.GetFullPath(Path.Combine(shadersDirectory, "..", GeneratedFileName));
string source = File.ReadAllText(sourcePath);

ShaderDefinition[] shaders =
[
    new("LegacyVertex", "VSMain", "0"),
    new("LegacyFragment", "FSMain", "0"),
    new("LinearVertex", "VSMain", "1"),
    new("LinearFragment", "FSMain", "1")
];

GraphicsApi[] graphicsApis = Enum.GetValues<GraphicsApi>();
string[] regionNames = [.. RegionNames(graphicsApis, shaders)];
string text = ReadGeneratedFile(generatedPath);

if (text.Contains("#region ", StringComparison.Ordinal) is false)
{
    text = CreateSkeleton(graphicsApis, shaders);
}
else
{
    text = EnsureRegions(text, graphicsApis, shaders, regionNames);
}

foreach (GraphicsApi graphicsApi in graphicsApis)
{
    foreach (ShaderDefinition shader in shaders)
    {
        string regionName = $"{graphicsApi}{shader.Suffix}";
        string variantSource = source.Replace("#if 0", $"#if {shader.ColorSpace}", StringComparison.Ordinal);

        try
        {
            ShaderDesc shaderDesc = ZenithCompiler.CompileFromSource(graphicsApi, variantSource, shader.EntryPoint);
            text = ReplaceRegion(text, regionName, FormatShaderDesc(regionName, shaderDesc));
            Console.WriteLine($"compiled {shader.Suffix} {graphicsApi} ({shaderDesc.CodeBytes.Length} bytes)");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"skip {shader.Suffix} {graphicsApi}: {exception.Message}");
        }
    }
}

WriteGeneratedFile(generatedPath, text);

static IEnumerable<string> RegionNames(GraphicsApi[] graphicsApis, ShaderDefinition[] shaders)
{
    foreach (GraphicsApi graphicsApi in graphicsApis)
    {
        foreach (ShaderDefinition shader in shaders)
        {
            yield return $"{graphicsApi}{shader.Suffix}";
        }
    }
}

static string CreateSkeleton(GraphicsApi[] graphicsApis, ShaderDefinition[] shaders)
{
    StringBuilder builder = new();
    builder.AppendLine($"namespace {NamespaceName};");
    builder.AppendLine();
    builder.AppendLine($"internal partial class {ClassName}");
    builder.AppendLine("{");

    bool firstRegion = true;
    foreach (GraphicsApi graphicsApi in graphicsApis)
    {
        foreach (ShaderDefinition shader in shaders)
        {
            if (firstRegion is false)
            {
                builder.AppendLine();
            }

            string regionName = $"{graphicsApi}{shader.Suffix}";
            ShaderDesc emptyShader = new()
            {
                Name = shader.EntryPoint,
                CodeBytes = []
            };
            builder.AppendLine(FormatRegion(regionName, FormatShaderDesc(regionName, emptyShader)));
            firstRegion = false;
        }
    }

    builder.AppendLine();
    builder.AppendLine("}");
    return NormalizeGeneratedText(builder.ToString());
}

static string EnsureRegions(string text, GraphicsApi[] graphicsApis, ShaderDefinition[] shaders, string[] regionNames)
{
    int regionIndex = 0;
    foreach (GraphicsApi graphicsApi in graphicsApis)
    {
        foreach (ShaderDefinition shader in shaders)
        {
            string regionName = $"{graphicsApi}{shader.Suffix}";
            if (FindRegion(text, regionName, out _, out _) is false)
            {
                text = InsertRegion(text, regionName, shader.EntryPoint, regionNames, regionIndex);
            }

            regionIndex++;
        }
    }

    return text;
}

static string InsertRegion(string text, string regionName, string entryPoint, string[] regionNames, int regionIndex)
{
    int insertionIndex = text.LastIndexOf("\n}", StringComparison.Ordinal) + 1;

    for (int index = regionIndex + 1; index < regionNames.Length; index++)
    {
        if (FindRegion(text, regionNames[index], out int start, out _) is true)
        {
            insertionIndex = start;
            break;
        }
    }

    string prefix = text[..insertionIndex].TrimEnd('\n');
    string suffix = text[insertionIndex..].TrimStart('\n');
    string before = prefix.EndsWith('{') ? "\n" : "\n\n";
    string after = suffix.StartsWith('}') ? "\n" : "\n\n";
    ShaderDesc emptyShader = new()
    {
        Name = entryPoint,
        CodeBytes = []
    };
    string body = FormatShaderDesc(regionName, emptyShader);
    return string.Concat(prefix, before, FormatRegion(regionName, body), after, suffix);
}

static string ReplaceRegion(string text, string regionName, string body)
{
    if (FindRegion(text, regionName, out int start, out int end) is false)
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
    if (start is < 0)
    {
        return false;
    }

    int finishStart = text.IndexOf($"\n{EndMarker}", start + startMarker.Length, StringComparison.Ordinal);
    if (finishStart is < 0)
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
    if (File.Exists(path) is false)
    {
        return string.Empty;
    }

    return NormalizeGeneratedText(File.ReadAllText(path));
}

static string NormalizeGeneratedText(string text)
{
    text = text.ReplaceLineEndings("\n").TrimEnd('\n');
    text = text.Replace("#endregion\n    #region", "#endregion\n\n    #region", StringComparison.Ordinal);
    return text.Replace("#endregion\n}", "#endregion\n\n}", StringComparison.Ordinal);
}

static void WriteGeneratedFile(string path, string text)
{
    File.WriteAllText(path, $"{NormalizeGeneratedText(text)}\n", new UTF8Encoding(true));
    Console.WriteLine($"wrote {path}");
}

static string FormatShaderDesc(string constantName, ShaderDesc shaderDesc)
{
    StringBuilder builder = new();
    builder.AppendLine($"    private static readonly ShaderDesc {constantName} = new()");
    builder.AppendLine("    {");
    builder.AppendLine($"        Name = \"{shaderDesc.Name}\",");
    builder.AppendLine("        CodeBytes =");
    builder.AppendLine("        [");

    byte[] codeBytes = shaderDesc.CodeBytes;
    const int BytesPerLine = 16;
    for (int index = 0; index < codeBytes.Length; index += BytesPerLine)
    {
        int count = Math.Min(BytesPerLine, codeBytes.Length - index);

        builder.Append("            ");
        for (int offset = 0; offset < count; offset++)
        {
            if (offset is not 0)
            {
                builder.Append(", ");
            }

            builder.Append("0x");
            builder.Append(codeBytes[index + offset].ToString("X2", CultureInfo.InvariantCulture));
        }

        if (index + count != codeBytes.Length)
        {
            builder.Append(',');
        }

        builder.AppendLine();
    }

    builder.AppendLine("        ],");
    builder.AppendLine("        ThreadGroupSize = new()");
    builder.AppendLine("        {");
    builder.AppendLine($"            X = {shaderDesc.ThreadGroupSize.X},");
    builder.AppendLine($"            Y = {shaderDesc.ThreadGroupSize.Y},");
    builder.AppendLine($"            Z = {shaderDesc.ThreadGroupSize.Z}");
    builder.AppendLine("        }");
    builder.AppendLine("    };");
    return builder.ToString();
}

static string GetShadersDirectory([CallerFilePath] string filePath = "")
{
    return Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();
}

file readonly struct ShaderDefinition(string suffix, string entryPoint, string colorSpace)
{
    public readonly string Suffix = suffix;

    public readonly string EntryPoint = entryPoint;

    public readonly string ColorSpace = colorSpace;
}
