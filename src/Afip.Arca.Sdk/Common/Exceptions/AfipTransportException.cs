using System;

namespace Afip.Arca.Sdk.Common.Exceptions;

/// <summary>
/// Thrown when the SDK cannot reach the AFIP service or receives a malformed/SOAP-fault
/// response. Business errors (rejection of an invoice, invalid CUIT, etc.) are NOT
/// transport errors — they are returned inside the operation result.
/// </summary>
public sealed class AfipTransportException : AfipException
{
    /// <summary>HTTP status code (if applicable).</summary>
    public int? HttpStatusCode { get; }

    /// <summary>SOAP fault code (if applicable).</summary>
    public string? SoapFaultCode { get; }

    /// <summary>Initializes a new instance of the <see cref="AfipTransportException"/> class.</summary>
    /// <param name="message">A human readable description of the error.</param>
    /// <param name="httpStatusCode">HTTP status code returned by the server, if known.</param>
    /// <param name="soapFaultCode">SOAP fault code, if a fault envelope was received.</param>
    /// <param name="innerException">The exception that triggered this one, if any.</param>
    public AfipTransportException(
        string message,
        int? httpStatusCode = null,
        string? soapFaultCode = null,
        Exception? innerException = null)
        : base(message, innerException ?? new InvalidOperationException(message))
    {
        HttpStatusCode = httpStatusCode;
        SoapFaultCode = soapFaultCode;
    }
}
