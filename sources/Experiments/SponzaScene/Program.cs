using SponzaScene;
using Zenith.NET;
using Zenith.NET.DirectX12;
using Zenith.NET.Extensions.Slang;
using Zenith.NET.Metal;
using Zenith.NET.Vulkan;

foreach (Backend backend in Enum.GetValues<Backend>())
{
    try
    {
        using GraphicsContext context = backend switch
        {
            Backend.DirectX12 => GraphicsContext.CreateDirectX12(true),
            Backend.Metal => GraphicsContext.CreateMetal(true),
            Backend.Vulkan => GraphicsContext.CreateVulkan(true),
            _ => throw new NotSupportedException()
        };

        string code = ImGuiShaders(context);

        Console.WriteLine(code);
    }
    catch (Exception)
    {
        Console.WriteLine($"Backend {backend} is not supported.");
    }

    Console.WriteLine();
}

App.Run();

static string ImGuiShaders(GraphicsContext context)
{
    const string Source = @"
struct VSInput
{
    float2 Position : POSITION0;
    
    float2 UV : TEXCOORD0;
    
    float4 Color : COLOR0;
};

struct VSOutput
{
    float4 Position : SV_POSITION;
    
    float2 UV : TEXCOORD0;
    
    float4 Color : COLOR0;
};

struct Constants
{
    float4x4 Projection;
};

uniform Constants constants;
uniform Texture2D texture;
uniform SamplerState sampler;

float3 SrgbToLinear(float3 srgb)
{
    return srgb * (srgb * (srgb * 0.305306011 + 0.682171111) + 0.012522878);
}

VSOutput VSMain(VSInput input)
{
    VSOutput output;
    
    output.Position = mul(float4(input.Position, 0.0, 1.0), constants.Projection);
    output.UV = input.UV;
    output.Color = input.Color;
    
#if 0
    output.Color.rgb = SrgbToLinear(output.Color.rgb);
#endif
    
    return output;
}

float4 PSMain(VSOutput input) : SV_TARGET
{
    return input.Color * texture.Sample(sampler, input.UV);
}
";

    return $"""
    [
        // Vertex Shader - Legacy
        "{Convert.ToHexString(context.LoadShaderFromSource(Source, "VSMain", ShaderStageFlags.Vertex).Desc.ShaderBytes)}",

        // Vertex Shader - Linear
        "{Convert.ToHexString(context.LoadShaderFromSource(Source.Replace("#if 0", "#if 1"), "VSMain", ShaderStageFlags.Vertex).Desc.ShaderBytes)}",

        // Pixel Shader
        "{Convert.ToHexString(context.LoadShaderFromSource(Source, "PSMain", ShaderStageFlags.Pixel).Desc.ShaderBytes)}"
    ];
""";
}