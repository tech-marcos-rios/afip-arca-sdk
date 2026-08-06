using System;
using System.Threading;
using System.Threading.Tasks;
using Afip.Arca.Sdk.Authentication;
using Afip.Arca.Sdk.Common.Exceptions;
using Afip.Arca.Sdk.Invoicing.Models;
using Afip.Arca.Sdk.Invoicing.Soap;
using Afip.Arca.Sdk.Invoicing.Validation;
using Microsoft.Extensions.Logging;

namespace Afip.Arca.Sdk.Invoicing;

/// <summary>
/// Default <see cref="IInvoiceService"/> implementation orchestrating
/// validation → ticket acquisition → SOAP call → result mapping.
/// </summary>
public sealed class InvoiceService : IInvoiceService
{
    private readonly IAccessTicketProvider _ticketProvider;
    private readonly WsfeSoapClient _soap;
    private readonly InvoiceValidator _validator;
    private readonly ILogger<InvoiceService> _logger;

    /// <summary>Initializes a new instance of the <see cref="InvoiceService"/> class.</summary>
    public InvoiceService(
        IAccessTicketProvider ticketProvider,
        WsfeSoapClient soap,
        InvoiceValidator validator,
        ILogger<InvoiceService> logger)
    {
        _ticketProvider = ticketProvider ?? throw new ArgumentNullException(nameof(ticketProvider));
        _soap = soap ?? throw new ArgumentNullException(nameof(soap));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<InvoiceAuthorizationResult> AuthorizeAsync(
        Invoice invoice,
        long? explicitNumber = null,
        CancellationToken cancellationToken = default)
    {
        if (invoice is null) throw new ArgumentNullException(nameof(invoice));

        var failures = _validator.Validate(invoice);
        if (failures.Count > 0)
        {
            throw new AfipValidationException(failures);
        }

        var ticket = await _ticketProvider.GetAsync(WsfeSoapClient.ServiceName, cancellationToken).ConfigureAwait(false);

        long number;
        if (explicitNumber is { } e)
        {
            number = e;
        }
        else
        {
            var last = await _soap.GetLastAuthorizedNumberAsync(ticket, invoice.Type, invoice.PointOfSale, cancellationToken).ConfigureAwait(false);
            number = last + 1;
        }

        _logger.LogInformation(
            "Authorizing comprobante type {Type} pos {Pos} number {Number}",
            invoice.Type, invoice.PointOfSale, number);

        var result = await _soap.AuthorizeAsync(ticket, invoice, number, cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Authorized comprobante {Type}-{Pos:D4}-{Number:D8} CAE {Cae}",
                invoice.Type, invoice.PointOfSale, number, result.Cae);
        }
        else
        {
            _logger.LogWarning("AFIP rejected comprobante {Type}-{Pos:D4}-{Number:D8} with {Count} error(s)",
                invoice.Type, invoice.PointOfSale, number, result.Errors.Count);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<InvoiceAuthorizationResult> CancelAsync(
        InvoiceReference original,
        decimal totalToCancel,
        CancellationToken cancellationToken = default)
    {
        if (original is null) throw new ArgumentNullException(nameof(original));
        if (totalToCancel <= 0) throw new ArgumentOutOfRangeException(nameof(totalToCancel));

        var creditNoteType = ResolveCreditNoteType(original.Type);

        var creditNote = InvoiceBuilder
            .ForType(creditNoteType)
            .AtPointOfSale(original.PointOfSale)
            .WithConcept(Concept.Products)
            .ToConsumerFinal()
            .WithDate(DateOnly.FromDateTime(DateTime.Today))
            .WithVatBase(net: 0, rate: VatRate.Zero)
            .WithNonTaxableAmount(totalToCancel)
            .WithTotalAmount(totalToCancel)
            .WithNetAmount(0)
            .AssociatedTo(original)
            .Build();

        return await AuthorizeAsync(creditNote, explicitNumber: null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long> GetLastAuthorizedNumberAsync(
        InvoiceType type,
        int pointOfSale,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _ticketProvider.GetAsync(WsfeSoapClient.ServiceName, cancellationToken).ConfigureAwait(false);
        return await _soap.GetLastAuthorizedNumberAsync(ticket, type, pointOfSale, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<(string AppServer, string DbServer, string AuthServer)> HealthCheckAsync(CancellationToken cancellationToken = default) =>
        _soap.DummyAsync(cancellationToken);

    private static InvoiceType ResolveCreditNoteType(InvoiceType originalType) =>
        originalType switch
        {
            InvoiceType.FacturaA or InvoiceType.NotaDebitoA => InvoiceType.NotaCreditoA,
            InvoiceType.FacturaB or InvoiceType.NotaDebitoB => InvoiceType.NotaCreditoB,
            InvoiceType.FacturaC or InvoiceType.NotaDebitoC => InvoiceType.NotaCreditoC,
            InvoiceType.FacturaM or InvoiceType.NotaDebitoM => InvoiceType.NotaCreditoM,
            _ => throw new ArgumentOutOfRangeException(
                nameof(originalType),
                "Cannot derive a credit note type from " + originalType + ".")
        };
}
