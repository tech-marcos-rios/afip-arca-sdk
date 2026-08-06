using System;
using System.Collections.Generic;

namespace Afip.Arca.Sdk.Invoicing.Models;

/// <summary>
/// Outcome of a <c>FECAESolicitar</c> call. Business failures are surfaced through
/// <see cref="IsSuccess"/> and <see cref="Errors"/> instead of exceptions to match
/// AFIP's protocol semantics.
/// </summary>
/// <param name="IsSuccess">True if AFIP returned <c>Resultado=A</c>.</param>
/// <param name="Cae">The 14-digit authorization code (when <see cref="IsSuccess"/>).</param>
/// <param name="CaeExpiration">Expiration date of the CAE (when <see cref="IsSuccess"/>).</param>
/// <param name="AssignedNumber">Sequential number assigned by AFIP for this comprobante.</param>
/// <param name="PointOfSale">Sales point.</param>
/// <param name="Type">Comprobante type.</param>
/// <param name="Observations">Non-blocking AFIP observations.</param>
/// <param name="Errors">Blocking AFIP errors (empty when <see cref="IsSuccess"/>).</param>
public sealed record InvoiceAuthorizationResult(
    bool IsSuccess,
    string? Cae,
    DateOnly? CaeExpiration,
    long? AssignedNumber,
    int PointOfSale,
    InvoiceType Type,
    IReadOnlyList<InvoiceObservation> Observations,
    IReadOnlyList<InvoiceError> Errors);
