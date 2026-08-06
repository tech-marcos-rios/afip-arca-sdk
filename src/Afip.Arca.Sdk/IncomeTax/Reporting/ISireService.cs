using System.Threading;
using System.Threading.Tasks;
using Afip.Arca.Sdk.IncomeTax.Reporting.Models;

namespace Afip.Arca.Sdk.IncomeTax.Reporting;

/// <summary>
/// SIRE (Sistema Integral de Retenciones Electrónicas) facade. Allows reporting
/// withholdings to AFIP so that the corresponding F. 2003/2004 certificate gets
/// issued.
/// </summary>
public interface ISireService
{
    /// <summary>Emits a withholding certificate.</summary>
    Task<WithholdingCertificateResult> IssueAsync(
        WithholdingCertificateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Cancels a previously issued certificate.</summary>
    Task<WithholdingCertificateResult> CancelAsync(
        string certificateNumber,
        CancellationToken cancellationToken = default);

    /// <summary>Queries a certificate by its number.</summary>
    Task<WithholdingCertificateResult> GetAsync(
        string certificateNumber,
        CancellationToken cancellationToken = default);
}
