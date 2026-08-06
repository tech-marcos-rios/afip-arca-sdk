using System;

namespace Afip.Arca.Sdk.Common.Exceptions;

/// <summary>
/// Thrown when authentication against the AFIP/ARCA WSAA service fails (e.g. invalid
/// certificate, expired TRA, or repeated <c>uniqueId</c>).
/// </summary>
public sealed class AfipAuthenticationException : AfipException
{
    /// <summary>Optional AFIP fault code returned by WSAA, when available.</summary>
    public string? FaultCode { get; }

    /// <summary>Initializes a new instance of the <see cref="AfipAuthenticationException"/> class.</summary>
    /// <param name="message">A human readable description of the error.</param>
    /// <param name="faultCode">Optional AFIP/SOAP fault code.</param>
    public AfipAuthenticationException(string message, string? faultCode = null) : base(message)
    {
        FaultCode = faultCode;
    }

    /// <summary>Initializes a new instance of the <see cref="AfipAuthenticationException"/> class.</summary>
    /// <param name="message">A human readable description of the error.</param>
    /// <param name="innerException">The exception that triggered this one.</param>
    /// <param name="faultCode">Optional AFIP/SOAP fault code.</param>
    public AfipAuthenticationException(string message, Exception innerException, string? faultCode = null)
        : base(message, innerException)
    {
        FaultCode = faultCode;
    }
}
