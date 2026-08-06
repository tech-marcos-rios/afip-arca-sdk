namespace Afip.Arca.Sdk.Common.Soap;

/// <summary>
/// Strongly-typed representation of a SOAP 1.1 <c>Fault</c> element.
/// </summary>
/// <param name="FaultCode">Fault code as returned by the server (e.g. <c>soap:Server</c>).</param>
/// <param name="FaultString">Human-readable description of the fault.</param>
/// <param name="Detail">Optional opaque detail block from the fault, if present.</param>
public sealed record SoapFault(string FaultCode, string FaultString, string? Detail);
