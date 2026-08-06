using System;

namespace Afip.Arca.Sdk.Invoicing.Models;

/// <summary>
/// Lightweight reference to a previously authorized comprobante. Used both to query
/// it back and to associate a credit/debit note to it.
/// </summary>
/// <param name="Type">Comprobante type.</param>
/// <param name="PointOfSale">Sales point that issued it.</param>
/// <param name="Number">Sequential number assigned by AFIP.</param>
/// <param name="Cuit">Optional CUIT of the issuer (defaults to the configured one).</param>
/// <param name="Date">Optional comprobante date.</param>
public sealed record InvoiceReference(
    InvoiceType Type,
    int PointOfSale,
    long Number,
    string? Cuit = null,
    DateOnly? Date = null);
