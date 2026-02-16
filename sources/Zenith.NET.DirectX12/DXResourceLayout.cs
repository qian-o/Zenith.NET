using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal class DXResourceLayout : ResourceLayout
{
    private readonly DXResourceRange[] ranges;

    public DXResourceLayout(DXGraphicsContext context, ResourceLayoutDesc desc) : base(context, desc)
    {
        ranges = new DXResourceRange[desc.Bindings.Length];

        uint index = 0;
        for (int i = 0; i < desc.Bindings.Length; i++)
        {
            ResourceBinding binding = desc.Bindings[i];

            ranges[i] = new(binding.Type, binding.StageFlags, index, binding.Count);

            index += binding.Count;
        }

        if (ResourceRanges(ShaderStageFlags.None, out DXResourceRange[] cbvSrvUavRanges, out DXResourceRange[] samplerRanges))
        {
            if (cbvSrvUavRanges.Length > 0)
            {
                RootParameterCount++;
            }

            if (samplerRanges.Length > 0)
            {
                RootParameterCount++;
            }
        }

        foreach (ShaderStageFlags stage in ZenithHelper.GraphicShaderStages())
        {
            if (ResourceRanges(stage, out cbvSrvUavRanges, out samplerRanges))
            {
                if (cbvSrvUavRanges.Length > 0)
                {
                    GraphicsRootParameterCount++;
                }

                if (samplerRanges.Length > 0)
                {
                    GraphicsRootParameterCount++;
                }
            }
        }
    }

    public uint RootParameterCount { get; }

    public uint GraphicsRootParameterCount { get; }

    public bool ResourceRanges(ShaderStageFlags stage, out DXResourceRange[] cbvSrvUavRanges, out DXResourceRange[] samplerRanges)
    {
        List<DXResourceRange> cbvSrvUavRangeList = [];
        List<DXResourceRange> samplerRangeList = [];

        foreach (DXResourceRange range in ranges)
        {
            if (stage is not ShaderStageFlags.None && !range.StageFlags.HasFlag(stage))
            {
                continue;
            }

            if (range.Type is ResourceType.Sampler)
            {
                samplerRangeList.Add(range);
            }
            else
            {
                cbvSrvUavRangeList.Add(range);
            }
        }

        cbvSrvUavRanges = [.. cbvSrvUavRangeList];
        samplerRanges = [.. samplerRangeList];

        return cbvSrvUavRanges.Length > 0 || samplerRanges.Length > 0;
    }

    public bool DescriptorRanges(ShaderStageFlags stage, out DescriptorRange[] cbvSrvUavRanges, out DescriptorRange[] samplerRanges)
    {
        List<DescriptorRange> cbvSrvUavRangeList = [];
        List<DescriptorRange> samplerRangeList = [];

        uint cbvSrvUavRangeOffset = 0;
        uint samplerRangeOffset = 0;
        foreach (ResourceBinding binding in Desc.Bindings)
        {
            if (stage is not ShaderStageFlags.None && !binding.StageFlags.HasFlag(stage))
            {
                continue;
            }

            DescriptorRange range = new()
            {
                RangeType = DXFormats.DirectX12(binding.Type),
                NumDescriptors = binding.Count,
                BaseShaderRegister = binding.Index
            };

            if (binding.Type is ResourceType.Sampler)
            {
                range.OffsetInDescriptorsFromTableStart = samplerRangeOffset;
                samplerRangeOffset += binding.Count;

                samplerRangeList.Add(range);
            }
            else
            {
                range.OffsetInDescriptorsFromTableStart = cbvSrvUavRangeOffset;
                cbvSrvUavRangeOffset += binding.Count;

                cbvSrvUavRangeList.Add(range);
            }
        }

        cbvSrvUavRanges = [.. cbvSrvUavRangeList];
        samplerRanges = [.. samplerRangeList];

        return cbvSrvUavRanges.Length > 0 || samplerRanges.Length > 0;
    }

    protected override void SetResourceName(string name)
    {
    }

    protected override void Destroy()
    {
    }
}
