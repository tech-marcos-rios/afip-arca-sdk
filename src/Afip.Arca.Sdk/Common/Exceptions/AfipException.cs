using System;

namespace Afip.Arca.Sdk.Common.Exceptions;

/// <summary>
/// Base type for every exception thrown by the <c>Afip.Arca.Sdk</c> library.
/// Consumers can catch this single type to handle any failure originating from
/// the SDK while still being able to switch on specific subtypes for granular
/// recovery.
/// </summary>
public class AfipException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="AfipException"/> class.</summary>
    /// <param name="message">A human readable description of the error.</param>
    public AfipException(string message) : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AfipException"/> class.</summary>
    /// <param name="message">A human readable description of the error.</param>
    /// <param name="innerException">The exception that triggered this one, if any.</param>
    public AfipException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
