using System;
using System.Collections.Generic;

namespace Afip.Arca.Sdk.IncomeTax.Reporting.Models;

/// <summary>Outcome of a SIRE certificate emission attempt.</summary>
/// <param name="IsSuccess">True if SIRE emitted the certificate.</param>
/// <param name="CertificateNumber">Certificate number assigned by AFIP, when successful.</param>
/// <param name="IssueDate">Issue date, when successful.</param>
/// <param name="Status">Free-text status returned by SIRE.</param>
/// <param name="Errors">Errors reported by SIRE, when any.</param>
public sealed record WithholdingCertificateResult(
    bool IsSuccess,
    string? CertificateNumber,
    DateOnly? IssueDate,
    string? Status,
    IReadOnlyList<(int Code, string Message)> Errors);
