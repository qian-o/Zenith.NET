namespace Zenith.NET;

internal static class ValidationMessages
{
    public const string MustNotBeNull = "{0} must not be null.";

    public const string MustNotBeZero = "{0} must not be zero.";

    public const string MustHaveExactlyNHandles = "{0} must have exactly {1} handles for {2}.";

    public const string MustBeValidHandle = "{0} must be a valid handle for {1}.";

    public const string MustBeValidHandles = "{0} must be valid handles for {1}.";

    public const string HasUnsupportedSurfaceType = "{0} has unsupported SurfaceType '{1}'.";

    public const string HasInvalidValue = "{0} has an invalid value '{1}'.";

    public const string HasNoAttachments = "{0} has no attachments.";

    public const string MustNotBeDisposed = "{0} must not be disposed.";

    public const string MustBeLessThan = "{0} must be less than {1}.";

    public const string MustNotBeNullOrEmpty = "{0} must not be null or empty.";

    public const string MustNotBeNullOrWhitespace = "{0} must not be null or whitespace.";

    public const string MustBeGreaterThanZero = "{0} must be greater than zero.";

    public const string IsZeroWarning = "{0} is zero, which may be valid for some {1} but could indicate an issue.";

    public const string IsSetToNoneWarning = "{0} is set to None, which may be valid but could indicate an issue.";

    public const string MustBeWithinBounds = "{0} must be greater than zero and within the bounds of {1}.";

    public const string MustBeLessThanOrEqualTo = "{0} must be less than or equal to {1}.";

    public const string MustBeGreaterThanOrEqualTo = "{0} must be greater than or equal to {1}.";

    public const string MustBeEqualTo = "{0} must be equal to {1}.";

    public const string MustBeBetween = "{0} must be between {1} and {2}.";

    public const string MustBeAMultipleOf = "{0} must be a multiple of {1}.";

    public const string MustDescribeACompleteCube = "{0} must describe a complete cube view.";

    public const string MustBeOneOf = "{0} must be one of: {1}.";

    public const string MustHaveFlag = "{0} must have the flag '{1}' set.";

    public const string MustHaveUsage = "{0} must have usage flag '{1}'.";

    public const string MustHaveSameValue = "{0} must match. First value: '{1}', second value: '{2}'.";

    public const string MustBeWithinResourceBounds = "{0} must be within the bounds of {1}.";

    public const string MustNotBeDepthStencilFormat = "{0} must not use a depth/stencil format.";

    public const string MustBeDepthStencilFormat = "{0} must use a depth/stencil format.";

    public const string MustBeSingleSampled = "{0} must use SampleCount.Count1.";

    public const string MustBeMultisampled = "{0} must use a sample count greater than SampleCount.Count1.";

    public const string MustBeFinite = "{0} must be finite.";

    public const string MustHaveCurrentPipeline = "{0} requires a pipeline to be set.";

    public const string MustHaveCurrentPipelineType = "{0} requires current pipeline type {1}, but current pipeline is {2}.";

    public const string MustNotBeTimestampQuery = "{0} cannot be used with QueryType.Timestamp.";

    public const string MustBeTimestampQuery = "{0} requires QueryType.Timestamp.";

    public const string InstanceCountMustRemainSame = "When updating a TopLevelAccelerationStructure, the number of instances must remain the same.";

    public const string UsagesIncompatibleWithAccess = "{0} contains flags '{1}' that require GPU read-write access and cannot be combined with BufferAccess.{2}.";

    public const string MustHaveNonZeroConstantsSize = "{0} reports a zero size for {1}; the constants layout has no payload for the current backend.";

    public const string MustBeCpuAccessible = "{0} must use BufferAccess.CpuReadOnly or BufferAccess.CpuWriteOnly to be mappable.";
}
