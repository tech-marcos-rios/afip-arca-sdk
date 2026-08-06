using System;
using System.Threading;
using System.Threading.Tasks;
using Afip.Arca.Sdk.Authentication;
using Afip.Arca.Sdk.IncomeTax.Reporting.Models;
using Afip.Arca.Sdk.IncomeTax.Reporting.Soap;
using Microsoft.Extensions.Logging;

namespace Afip.Arca.Sdk.IncomeTax.Reporting;

/// <summary>Default <see cref="ISireService"/> implementation.</summary>
public sealed class SireService : ISireService
{
    private readonly IAccessTicketProvider _ticketProvider;
    private readonly SireSoapClient _soap;
    private readonly ILogger<SireService> _logger;

    /// <summary>Initializes a new instance of the <see cref="SireService"/> class.</summary>
    public SireService(
        IAccessTicketProvider ticketProvider,
        SireSoapClient soap,
        ILogger<SireService> logger)
    {
        _ticketProvider = ticketProvider ?? throw new ArgumentNullException(nameof(ticketProvider));
        _soap = soap ?? throw new ArgumentNullException(nameof(soap));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<WithholdingCertificateResult> IssueAsync(
        WithholdingCertificateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        var ticket = await _ticketProvider.GetAsync(SireSoapClient.ServiceName, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Issuing SIRE certificate: tax {Tax} regime {Regime} amount {Amount}",
            request.TaxCode, request.Regime, request.WithheldAmount);

        return await _soap.IssueAsync(ticket, request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<WithholdingCertificateResult> CancelAsync(
        string certificateNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(certificateNumber)) throw new ArgumentException("Certificate number required.", nameof(certificateNumber));

        var ticket = await _ticketProvider.GetAsync(SireSoapClient.ServiceName, cancellationToken).ConfigureAwait(false);
        return await _soap.CancelAsync(ticket, certificateNumber, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<WithholdingCertificateResult> GetAsync(
        string certificateNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(certificateNumber)) throw new ArgumentException("Certificate number required.", nameof(certificateNumber));

        var ticket = await _ticketProvider.GetAsync(SireSoapClient.ServiceName, cancellationToken).ConfigureAwait(false);
        return await _soap.GetAsync(ticket, certificateNumber, cancellationToken).ConfigureAwait(false);
    }
}
