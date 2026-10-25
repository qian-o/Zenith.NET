namespace Zenith.NET.Extensions.DLSS;

public readonly struct DLSSOptimalSettings(uint inputWidth, uint inputHeight, uint minInputWidth, uint minInputHeight, uint maxInputWidth, uint maxInputHeight)
{
    public readonly uint InputWidth = inputWidth;

    public readonly uint InputHeight = inputHeight;

    public readonly uint MinInputWidth = minInputWidth;

    public readonly uint MinInputHeight = minInputHeight;

    public readonly uint MaxInputWidth = maxInputWidth;

    public readonly uint MaxInputHeight = maxInputHeight;
}
