#!/usr/bin/env dotnet
#:project ../../../Zenith.NET/Zenith.NET.csproj
#:property TargetFramework=net10.0

using System.Globalization;
using System.Text;
using Zenith.NET;

const string EntryName = "Main";

string shadersDirectory = ResolveShadersDirectory();
string passesDirectory = Path.GetFullPath(Path.Combine(shadersDirectory, "..", "Passes"));

PassDesc[] passes =
[
    new()
    {
        Name = "Sgsr1Pass",
        GeneratedFileName = "Sgsr1Pass.g.cs",
        Shaders =
        [
            new()
            {
                ConstantSuffix = "Main",
                FileName = "Sgsr1.slang"
            }
        ]
    },
    new()
    {
        Name = "Sgsr2ConvertPass",
        GeneratedFileName = "Sgsr2ConvertPass.g.cs",
        Shaders =
        [
            new()
            {
                ConstantSuffix = "SpeedMain",
                FileName = "Sgsr2ConvertSpeed.slang"
            },
            new()
            {
                ConstantSuffix = "QualityMain",
                FileName = "Sgsr2ConvertQuality.slang"
            }
        ]
    },
    new()
    {
        Name = "Sgsr2ActivatePass",
        GeneratedFileName = "Sgsr2ActivatePass.g.cs",
        Shaders =
        [
            new()
            {
                ConstantSuffix = "Main",
                FileName = "Sgsr2Activate.slang"
            }
        ]
    },
    new()
    {
        Name = "Sgsr2UpscalePass",
        GeneratedFileName = "Sgsr2UpscalePass.g.cs",
        Shaders =
        [
            new()
            {
                ConstantSuffix = "SpeedMain",
                FileName = "Sgsr2UpscaleSpeed.slang"
            },
            new()
            {
                ConstantSuffix = "QualityMain",
                FileName = "Sgsr2UpscaleQuality.slang"
            }
        ]
    }
];

foreach (PassDesc pass in passes)
{
    string generatedPath = Path.Combine(passesDirectory, pass.GeneratedFileName);
    Dictionary<string, string> previousConstants = ReadPreviousConstants(generatedPath);

    StringBuilder builder = new();
    builder.AppendLine("namespace Zenith.NET.Extensions.Upscaling.Passes;");
    builder.AppendLine();
    builder.AppendLine($"internal unsafe partial class {pass.Name}");
    builder.AppendLine("{");

    bool firstConstant = true;
    foreach (GraphicsApi graphicsApi in Enum.GetValues<GraphicsApi>())
    {
        foreach (ShaderSource shader in pass.Shaders)
        {
            string constantName = $"{graphicsApi}{shader.ConstantSuffix}";
            string shaderPath = Path.Combine(shadersDirectory, shader.FileName);

            try
            {
                ShaderDesc shaderDesc = ZenithCompiler.CompileFromFile(graphicsApi, shaderPath, EntryName);

                if (firstConstant is false)
                {
                    builder.AppendLine();
                }

                builder.Append(FormatShaderDesc(constantName, shaderDesc));
                firstConstant = false;

                Console.WriteLine($"compiled {shader.FileName} {graphicsApi} ({shaderDesc.CodeBytes.Length} bytes, threads={shaderDesc.ThreadGroupSize.X}x{shaderDesc.ThreadGroupSize.Y}x{shaderDesc.ThreadGroupSize.Z})");
            }
            catch (Exception exception)
            {
                Console.WriteLine($"skip {shader.FileName} {graphicsApi}: {exception.Message}");

                if (firstConstant is false)
                {
                    builder.AppendLine();
                }

                if (previousConstants.TryGetValue(constantName, out string? previousConstant))
                {
                    builder.Append(previousConstant);

                    if (previousConstant.EndsWith('\n') is false)
                    {
                        builder.AppendLine();
                    }
                }
                else
                {
                    Console.WriteLine($"missing {constantName} (no previous result)");
                    builder.Append(FormatShaderDesc(constantName, new()
                    {
                        Name = EntryName,
                        CodeBytes = []
                    }));
                }

                firstConstant = false;
            }
        }
    }

    builder.AppendLine("}");
    File.WriteAllText(generatedPath, builder.ToString());
    Console.WriteLine($"wrote {generatedPath}");
}

static string ResolveShadersDirectory()
{
    string[] candidates =
    [
        Directory.GetCurrentDirectory(),
        AppContext.BaseDirectory
    ];

    foreach (string candidate in candidates)
    {
        string directory = Path.GetFullPath(candidate);
        while (true)
        {
            string compilePath = Path.Combine(directory, "Compile.cs");
            string shaderPath = Path.Combine(directory, "Sgsr1.slang");
            if (File.Exists(compilePath) && File.Exists(shaderPath))
            {
                return directory;
            }

            DirectoryInfo? parent = Directory.GetParent(directory);
            if (parent is null)
            {
                break;
            }

            directory = parent.FullName;
        }
    }

    throw new InvalidOperationException("Shaders directory not found.");
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
    for (int i = 0; i < codeBytes.Length; i += 16)
    {
        int count = Math.Min(16, codeBytes.Length - i);

        builder.Append("            ");
        for (int j = 0; j < count; j++)
        {
            if (j is not 0)
            {
                builder.Append(", ");
            }

            builder.Append("0x");
            builder.Append(codeBytes[i + j].ToString("X2", CultureInfo.InvariantCulture));
        }

        if (i + count != codeBytes.Length)
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

static Dictionary<string, string> ReadPreviousConstants(string path)
{
    Dictionary<string, string> constants = [];
    if (File.Exists(path) is false)
    {
        return constants;
    }

    string text = File.ReadAllText(path);
    const string Prefix = "    private static readonly ShaderDesc ";

    int index = 0;
    while (true)
    {
        int start = text.IndexOf(Prefix, index, StringComparison.Ordinal);
        if (start is < 0)
        {
            return constants;
        }

        int nameStart = start + Prefix.Length;
        int nameEnd = text.IndexOf(" = new()", nameStart, StringComparison.Ordinal);
        if (nameEnd is < 0)
        {
            return constants;
        }

        string name = text[nameStart..nameEnd];
        int brace = text.IndexOf('{', nameEnd);
        if (brace is < 0)
        {
            return constants;
        }

        int depth = 0;
        int end = brace;
        for (; end < text.Length; end++)
        {
            if (text[end] is '{')
            {
                depth++;
            }
            else if (text[end] is '}')
            {
                depth--;
                if (depth is 0)
                {
                    break;
                }
            }
        }

        if (depth is not 0)
        {
            return constants;
        }

        int terminator = text.IndexOf(';', end);
        if (terminator is < 0)
        {
            return constants;
        }

        constants[name] = text[start..(terminator + 1)] + Environment.NewLine;
        index = terminator + 1;
    }
}

file struct PassDesc
{
    public string Name;

    public string GeneratedFileName;

    public ShaderSource[] Shaders;
}

file struct ShaderSource
{
    public string ConstantSuffix;

    public string FileName;
}
