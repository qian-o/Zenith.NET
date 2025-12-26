using System.Runtime.CompilerServices;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D12;

namespace Zenith.NET.DirectX12;

internal unsafe class DXRayTracingPipeline : RayTracingPipeline
{
    public ComPtr<ID3D12RootSignature> RootSignature;

    public ComPtr<ID3D12StateObject> StateObject;

    public GpuVirtualAddressRange RayGenerationRange;

    public GpuVirtualAddressRangeAndStride MissRange;

    public GpuVirtualAddressRangeAndStride HitGroupsRange;

    public DXRayTracingPipeline(DXGraphicsContext context, RayTracingPipelineDesc desc) : base(context, desc)
    {
        using ZenithMarshal.Scope scope = new();

        Shader[] shaders = [desc.RayGeneration, .. desc.Miss, .. desc.AnyHit, .. desc.Intersection, .. desc.ClosestHit];

        uint numSubObjects = (uint)(shaders.Length + desc.HitGroups.Length + 2 + 1);
        StateSubobject* subobjects = (StateSubobject*)ZenithMarshal.Allocate<StateSubobject>(scope, numSubObjects);

        StateObjectDesc stateObjectDesc = new()
        {
            Type = StateObjectType.RaytracingPipeline,
            NumSubobjects = numSubObjects,
            PSubobjects = subobjects
        };

        foreach (DXShader shader in shaders.Select(static item => item.DirectX12()))
        {
            ExportDesc exportDesc = new()
            {
                Name = (char*)ZenithMarshal.StringToPointer(scope, shader.Desc.EntryPoint, StringEncoding.Uni)
            };

            DxilLibraryDesc dxilLibraryDesc = new()
            {
                DXILLibrary = shader.GetShaderBytecode(scope),
                NumExports = 1,
                PExports = (ExportDesc*)ZenithMarshal.AllocateAndFill(scope, [exportDesc])
            };

            *subobjects++ = new()
            {
                Type = StateSubobjectType.DxilLibrary,
                PDesc = (DxilLibraryDesc*)ZenithMarshal.AllocateAndFill(scope, [dxilLibraryDesc])
            };
        }

        foreach (HitGroup hitGroup in desc.HitGroups)
        {
            HitGroupDesc hitGroupDesc = new()
            {
                HitGroupExport = (char*)ZenithMarshal.StringToPointer(scope, hitGroup.Name, StringEncoding.Uni),
                Type = DXFormats.DirectX12(hitGroup.Type),
                AnyHitShaderImport = hitGroup.AnyHit is null ? null : (char*)ZenithMarshal.StringToPointer(scope, hitGroup.AnyHit, StringEncoding.Uni),
                ClosestHitShaderImport = hitGroup.ClosestHit is null ? null : (char*)ZenithMarshal.StringToPointer(scope, hitGroup.ClosestHit, StringEncoding.Uni),
                IntersectionShaderImport = hitGroup.Intersection is null ? null : (char*)ZenithMarshal.StringToPointer(scope, hitGroup.Intersection, StringEncoding.Uni)
            };

            *subobjects++ = new()
            {
                Type = StateSubobjectType.HitGroup,
                PDesc = (HitGroupDesc*)ZenithMarshal.AllocateAndFill(scope, [hitGroupDesc])
            };
        }

        RaytracingPipelineConfig raytracingPipelineConfig = new()
        {
            MaxTraceRecursionDepth = desc.MaxTraceRecursionDepth
        };

        *subobjects++ = new()
        {
            Type = StateSubobjectType.RaytracingPipelineConfig,
            PDesc = (RaytracingPipelineConfig*)ZenithMarshal.AllocateAndFill(scope, [raytracingPipelineConfig])
        };

        RaytracingShaderConfig raytracingShaderConfig = new()
        {
            MaxAttributeSizeInBytes = desc.MaxAttributeSizeInBytes,
            MaxPayloadSizeInBytes = desc.MaxPayloadSizeInBytes
        };

        *subobjects++ = new()
        {
            Type = StateSubobjectType.RaytracingShaderConfig,
            PDesc = (RaytracingShaderConfig*)ZenithMarshal.AllocateAndFill(scope, [raytracingShaderConfig])
        };

        // ResourceLayouts
        {
            List<RootParameter> parameters = [];
            for (int i = 0; i < desc.ResourceLayouts.Length; i++)
            {
                if (desc.ResourceLayouts[i].DirectX12().DescriptorRanges(ShaderStageFlags.None, (uint)i, out DescriptorRange[] cbvSrvUavRanges, out DescriptorRange[] samplerRanges))
                {
                    if (cbvSrvUavRanges.Length > 0)
                    {
                        parameters.Add(new()
                        {
                            ParameterType = RootParameterType.TypeDescriptorTable,
                            DescriptorTable = new()
                            {
                                NumDescriptorRanges = (uint)cbvSrvUavRanges.Length,
                                PDescriptorRanges = (DescriptorRange*)ZenithMarshal.AllocateAndFill(scope, cbvSrvUavRanges)
                            }
                        });
                    }

                    if (samplerRanges.Length > 0)
                    {
                        parameters.Add(new()
                        {
                            ParameterType = RootParameterType.TypeDescriptorTable,
                            DescriptorTable = new()
                            {
                                NumDescriptorRanges = (uint)samplerRanges.Length,
                                PDescriptorRanges = (DescriptorRange*)ZenithMarshal.AllocateAndFill(scope, samplerRanges)
                            }
                        });
                    }
                }
            }

            RootSignatureDesc rootSignatureDesc = new()
            {
                NumParameters = (uint)parameters.Count,
                PParameters = (RootParameter*)ZenithMarshal.AllocateAndFill(scope, [.. parameters]),
                Flags = RootSignatureFlags.AllowInputAssemblerInputLayout
            };

            ComPtr<ID3D10Blob> blob = default;
            ComPtr<ID3D10Blob> error = default;
            context.D3D12.SerializeRootSignature(&rootSignatureDesc, D3DRootSignatureVersion.Version1, ref blob, ref error).Success();
            context.Device.CreateRootSignature(0, blob.GetBufferPointer(), blob.GetBufferSize(), out RootSignature).Success();
            blob.Dispose();
            error.Dispose();

            GlobalRootSignature globalRootSignature = new()
            {
                PGlobalRootSignature = RootSignature
            };

            *subobjects = new()
            {
                Type = StateSubobjectType.GlobalRootSignature,
                PDesc = (GlobalRootSignature*)ZenithMarshal.AllocateAndFill(scope, [globalRootSignature])
            };
        }

        context.Device5?.CreateStateObject(&stateObjectDesc, out StateObject).Success();

        const uint HandleSize = 32;
        const uint HandleSizeAligned = 64;

        StateObject.QueryInterface(out ComPtr<ID3D12StateObjectProperties> stateObjectProperties).Success();

        RayGenerationBuffer = CreateSectionBuffer([desc.RayGeneration.Desc.EntryPoint]);
        MissBuffer = CreateSectionBuffer([.. desc.Miss.Select(static item => item.Desc.EntryPoint)]);
        HitGroupsBuffer = CreateSectionBuffer([.. desc.HitGroups.Select(static item => item.Name)]);

        RayGenerationRange = new()
        {
            StartAddress = RayGenerationBuffer.GPUVirtualAddress,
            SizeInBytes = RayGenerationBuffer.Desc.SizeInBytes
        };

        MissRange = new()
        {
            StartAddress = MissBuffer.GPUVirtualAddress,
            SizeInBytes = MissBuffer.Desc.SizeInBytes,
            StrideInBytes = HandleSizeAligned
        };

        HitGroupsRange = new()
        {
            StartAddress = HitGroupsBuffer.GPUVirtualAddress,
            SizeInBytes = HitGroupsBuffer.Desc.SizeInBytes,
            StrideInBytes = HandleSizeAligned
        };

        stateObjectProperties.Dispose();

        DXBuffer CreateSectionBuffer(string[] entryPoints)
        {
            DXBuffer buffer = new(context, new()
            {
                SizeInBytes = (uint)(HandleSizeAligned * entryPoints.Length),
                StrideInBytes = HandleSizeAligned,
                Flags = BufferUsageFlags.MapWrite
            });

            MappedMemory mappedMemory = buffer.Map();

            byte* shaderBindleStorage = (byte*)mappedMemory.Pointer;
            foreach (string entryPoint in entryPoints)
            {
                Unsafe.CopyBlock(shaderBindleStorage, stateObjectProperties.GetShaderIdentifier(entryPoint), HandleSize);

                shaderBindleStorage += HandleSizeAligned;
            }

            buffer.Unmap();

            return buffer;
        }
    }

    public new DXGraphicsContext Context => (DXGraphicsContext)base.Context;

    public DXBuffer RayGenerationBuffer { get; }

    public DXBuffer MissBuffer { get; }

    public DXBuffer HitGroupsBuffer { get; }

    protected override void SetResourceName(string name)
    {
        StateObject.SetName(name).Success();
    }

    protected override void Destroy()
    {
        HitGroupsBuffer.Dispose();
        MissBuffer.Dispose();
        RayGenerationBuffer.Dispose();

        StateObject.Dispose();
        RootSignature.Dispose();
    }
}
