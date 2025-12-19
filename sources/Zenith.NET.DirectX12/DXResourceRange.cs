namespace Zenith.NET.DirectX12;

internal readonly record struct DXResourceRange(ResourceType Type, ShaderStageFlags StageFlags, uint Index, uint Count);