using System.Threading;
using System.Threading.Tasks;
using Afip.Arca.Sdk.Invoicing.Models;

namespace Afip.Arca.Sdk.Invoicing;

/// <summary>
/// Application-facing API for the electronic invoicing module. Hides the SOAP
/// transport, the auth ticket caching and AFIP's request-shape minutiae.
/// </summary>
public interface IInvoiceService
{
    /// <summary>
    /// Authorizes the given <see cref="Invoice"/>. If no explicit number is provided
    /// the SDK fetches the last authorized number and uses the next.
    /// </summary>
    /// <param name="invoice">Invoice to authorize.</param>
    /// <param name="explicitNumber">Optional explicit number (only when the caller managed it).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<InvoiceAuthorizationResult> AuthorizeAsync(
        Invoice invoice,
        long? explicitNumber = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a previously authorized invoice by issuing the appropriate credit note.
    /// AFIP does not support direct cancellation — this is the conventional way.
    /// </summary>
    /// <param name="original">Reference to the original comprobante.</param>
    /// <param name="totalToCancel">Amount to credit (typically the original's total).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<InvoiceAuthorizationResult> CancelAsync(
        InvoiceReference original,
        decimal totalToCancel,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the last authorized number for the given (point of sale, type).</summary>
    Task<long> GetLastAuthorizedNumberAsync(
        InvoiceType type,
        int pointOfSale,
        CancellationToken cancellationToken = default);

    /// <summary>Health-check against AFIP <c>FEDummy</c>. Each segment is <c>OK</c> when healthy.</summary>
    Task<(string AppServer, string DbServer, string AuthServer)> HealthCheckAsync(CancellationToken cancellationToken = default);
}
