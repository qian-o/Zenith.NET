using NGX.NET;

namespace Zenith.NET.Extensions.DLSS;

public unsafe class DLSSCapabilities
{
    internal DLSSCapabilities(NGXParameter parameters)
    {
        SuperResolutionSupported = GetI(parameters, Ngx.ParameterSuperSamplingAvailable) is not 0;
        RayReconstructionSupported = GetI(parameters, Ngx.ParameterSuperSamplingDenoisingAvailable) is not 0;
        FrameGenerationSupported = GetI(parameters, Ngx.ParameterFrameGenerationAvailable) is not 0;
        MaxGeneratedFrameCount = GetUI(parameters, Ngx.DLSSGParameterMultiFrameCountMax);
    }

    public bool SuperResolutionSupported { get; }

    public bool RayReconstructionSupported { get; }

    public bool FrameGenerationSupported { get; }

    public uint MaxGeneratedFrameCount { get; }

    private static int GetI(NGXParameter parameters, ReadOnlySpan<byte> name)
    {
        fixed (byte* pointer = name)
        {
            int value = 0;
            Ngx.Parameter.GetI(parameters, pointer, &value).Success();

            return value;
        }
    }

    private static uint GetUI(NGXParameter parameters, ReadOnlySpan<byte> name)
    {
        fixed (byte* pointer = name)
        {
            uint value = 0;
            Ngx.Parameter.GetUI(parameters, pointer, &value).Success();

            return value;
        }
    }
}
