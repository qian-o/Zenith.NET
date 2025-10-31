namespace Zenith.NET;

internal static class VKFormats
{
    internal static VkShaderStageFlags GetShaderStageFlags(ShaderStageFlags shaderStageFlags)
    {
        VkShaderStageFlags result = VkShaderStageFlags.None;

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Vertex))
        {
            result |= VkShaderStageFlags.VertexBit;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Hull))
        {
            result |= VkShaderStageFlags.TessellationControlBit;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Domain))
        {
            result |= VkShaderStageFlags.TessellationEvaluationBit;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Geometry))
        {
            result |= VkShaderStageFlags.GeometryBit;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Pixel))
        {
            result |= VkShaderStageFlags.FragmentBit;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Compute))
        {
            result |= VkShaderStageFlags.ComputeBit;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.RayGeneration))
        {
            result |= VkShaderStageFlags.RaygenBitKhr;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Miss))
        {
            result |= VkShaderStageFlags.MissBitKhr;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.AnyHit))
        {
            result |= VkShaderStageFlags.AnyHitBitKhr;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Intersection))
        {
            result |= VkShaderStageFlags.IntersectionBitKhr;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.ClosestHit))
        {
            result |= VkShaderStageFlags.ClosestHitBitKhr;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Amplification))
        {
            result |= VkShaderStageFlags.TaskBitExt;
        }

        if (shaderStageFlags.HasFlag(ShaderStageFlags.Mesh))
        {
            result |= VkShaderStageFlags.MeshBitExt;
        }

        return result;
    }
}
