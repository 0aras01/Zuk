using System;

namespace Fractal.Core.Exceptions;

public class GpuAccelerationNotAvailableException : Exception
{
    public GpuAccelerationNotAvailableException()
        : base("No suitable GPU accelerator was found. Falling back to CPU generator.") { }

    public GpuAccelerationNotAvailableException(string message)
        : base(message) { }

    public GpuAccelerationNotAvailableException(string message, Exception innerException)
        : base(message, innerException) { }
}
