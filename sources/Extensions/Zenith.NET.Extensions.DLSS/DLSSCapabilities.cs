using NGX.NET;

namespace Zenith.NET.Extensions.DLSS;

public unsafe class DLSSCapabilities
{
    internal DLSSCapabilities(NGXParameter parameters)
    {
        SuperResolutionSupported = GetI(parameters, Ngx.ParameterSuperSamplingAvailable) is not 0;
        RayReconstructionSupported = GetI(parameters, Ngx.ParameterSuperSamplingDenoisingAvailable) is not 0;
        FrameGenerationSupported = GetI(parameters, Ngx.ParameterFrameGenerationAvailable) is not 0;

        if (FrameGenerationSupported)
        {
            fixed (byte* name = Ngx.DLSSGParameterMultiFrameCountMax)
            {
                uint value = 0;
                NGXResult result = Ngx.Parameter.GetUI(parameters, name, &value);

                MaxMultiFrameCount = Ngx.NVSDK_NGX_SUCCEED(result) && value > 1 ? value : 1;
            }
        }
    }

    public bool SuperResolutionSupported { get; }

    public bool RayReconstructionSupported { get; }

    public bool FrameGenerationSupported { get; }

    public uint MaxMultiFrameCount { get; }

    private static int GetI(NGXParameter parameters, ReadOnlySpan<byte> name)
    {
        fixed (byte* pointer = name)
        {
            int value = 0;
            Ngx.Parameter.GetI(parameters, pointer, &value).Success();

            return value;
        }
    }
}
