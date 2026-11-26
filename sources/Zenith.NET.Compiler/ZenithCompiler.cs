using Slangc.NET;

namespace Zenith.NET;

public static class ZenithCompiler
{
    private const string Preamble =
        """
        export T getDescriptorFromHandle<T : IOpaqueDescriptor>(DescriptorHandle<T> handleValue)
        {
            __target_switch
            {
                case spirv:
                {
                    if (T.kind == DescriptorKind.AccelerationStructure)
                    {
                        return __slang_noop_cast<T>(RaytracingAccelerationStructure(__asuint64((uint2)handleValue)));
                    }

                    if (T.kind == DescriptorKind.Unknown)
                    {
                        return __declVal<__DynamicResource<__DynamicResourceKind.General>>().asOpaqueDescriptor<T>();
                    }

                    return defaultGetDescriptorFromHandle(handleValue);
                }

                default:
                    return defaultGetDescriptorFromHandle(handleValue);
            }
        }
        """;

    public static ShaderDesc CompileFromFile(GraphicsApi graphicsApi, string file, string entryPoint, Dictionary<string, string>? preprocessorMacros = null, string[]? searchPaths = null)
    {
        return new()
        {
            Name = entryPoint,
            CodeBytes = SlangCompiler.CompileWithReflection(Preamble, [file, .. Arguments(graphicsApi, entryPoint, preprocessorMacros, searchPaths)], out SlangReflection reflection),
            ThreadGroupSize = ThreadGroupSize(reflection, entryPoint)
        };
    }

    public static ShaderDesc CompileFromSource(GraphicsApi graphicsApi, string source, string entryPoint, Dictionary<string, string>? preprocessorMacros = null, string[]? searchPaths = null)
    {
        return new()
        {
            Name = entryPoint,
            CodeBytes = SlangCompiler.CompileWithReflection(Preamble + Environment.NewLine + source, Arguments(graphicsApi, entryPoint, preprocessorMacros, searchPaths), out SlangReflection reflection),
            ThreadGroupSize = ThreadGroupSize(reflection, entryPoint)
        };
    }

    private static string[] Arguments(GraphicsApi graphicsApi, string entryPoint, Dictionary<string, string>? preprocessorMacros, string[]? searchPaths)
    {
        List<string> arguments =
        [
            "-entry", entryPoint,
            "-matrix-layout-row-major"
        ];

        if (preprocessorMacros is not null)
        {
            foreach ((string key, string value) in preprocessorMacros)
            {
                arguments.AddRange(["-D", string.IsNullOrEmpty(value) ? key : $"{key}={value}"]);
            }
        }

        if (searchPaths is not null)
        {
            foreach (string searchPath in searchPaths)
            {
                arguments.AddRange(["-I", searchPath]);
            }
        }

        arguments.Add("-target");

        switch (graphicsApi)
        {
            case GraphicsApi.DirectX12:
                arguments.AddRange(["dxil", "-profile", "sm_6_6"]);
                break;

            case GraphicsApi.Metal:
                arguments.AddRange(["metallib", "-capability", "metallib_latest", "-Xmetal", "-std=metal4.0"]);
                break;

            case GraphicsApi.Vulkan:
                arguments.AddRange(["spirv", "-capability", "spirv_latest", "-capability", "spvDescriptorHeapEXT", "-capability", "spvRayQueryKHR", "-fvk-use-entrypoint-name"]);
                break;
        }

        return [.. arguments];
    }

    private static ThreadGroupSize ThreadGroupSize(SlangReflection reflection, string entryPoint)
    {
        if (reflection.EntryPoints.FirstOrDefault(p => p.Name == entryPoint) is SlangEntryPoint slangEntryPoint && slangEntryPoint.ThreadGroupSize.Length is 3)
        {
            return new()
            {
                X = slangEntryPoint.ThreadGroupSize[0],
                Y = slangEntryPoint.ThreadGroupSize[1],
                Z = slangEntryPoint.ThreadGroupSize[2]
            };
        }

        return new();
    }
}
