using System;

namespace Afip.Arca.Sdk.IncomeTax.Reporting.Models;

/// <summary>
/// Input for <c>ISireService.IssueAsync</c>: everything SIRE needs to emit a
/// withholding certificate (F. 2003/2004).
/// </summary>
/// <param name="TaxCode">Which tax this withholding relates to.</param>
/// <param name="Regime">Regime code (depends on the tax).</param>
/// <param name="WithholdingDate">Date of the withholding.</param>
/// <param name="WithheldCuit">CUIT of the subject from whom the amount was withheld.</param>
/// <param name="TaxableBase">Base amount used in the calculation.</param>
/// <param name="WithheldAmount">Withheld amount, in ARS.</param>
/// <param name="SourceComprobanteType">Comprobante type that originated the payment.</param>
/// <param name="SourceComprobanteNumber">Comprobante number that originated the payment.</param>
/// <param name="Condition">Padrón condition of the subject.</param>
public sealed record WithholdingCertificateRequest(
    TaxCode TaxCode,
    int Regime,
    DateOnly WithholdingDate,
    string WithheldCuit,
    decimal TaxableBase,
    decimal WithheldAmount,
    int SourceComprobanteType,
    string SourceComprobanteNumber,
    SubjectCondition Condition);
