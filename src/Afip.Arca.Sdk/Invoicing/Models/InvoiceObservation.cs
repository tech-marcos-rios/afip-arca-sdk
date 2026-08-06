namespace Afip.Arca.Sdk.Invoicing.Models;

/// <summary>
/// A non-blocking observation returned by AFIP on an authorized comprobante. AFIP
/// approves the comprobante and emits the CAE but flags issues the contributor
/// should know about (typical example: receiver CUIT not found in padrón).
/// </summary>
/// <param name="Code">AFIP-defined code.</param>
/// <param name="Message">Human-readable description.</param>
public sealed record InvoiceObservation(int Code, string Message);
