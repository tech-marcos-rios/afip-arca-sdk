namespace Afip.Arca.Sdk.Invoicing.Models;

/// <summary>
/// A blocking error returned by AFIP. When this is present, the comprobante was not
/// authorized.
/// </summary>
/// <param name="Code">AFIP-defined code.</param>
/// <param name="Message">Human-readable description.</param>
public sealed record InvoiceError(int Code, string Message);
