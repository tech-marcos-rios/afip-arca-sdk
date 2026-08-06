using System;
using System.Collections.Generic;
using Afip.Arca.Sdk.Invoicing.Models;

namespace Afip.Arca.Sdk.Invoicing;

/// <summary>
/// Fluent builder for <see cref="Invoice"/>. Forces the consumer through a sequence
/// that yields syntactically valid instances; semantic validation happens via
/// <see cref="Afip.Arca.Sdk.Invoicing.Validation.InvoiceValidator"/>.
/// </summary>
public sealed class InvoiceBuilder
{
    private InvoiceType? _type;
    private int? _pointOfSale;
    private Concept _concept = Concept.Products;
    private DocumentType _docType = DocumentType.ConsumidorFinal;
    private long _docNumber;
    private ReceiverVatCondition _receiverVatCondition = ReceiverVatCondition.ConsumerFinal;
    private bool _vatConditionExplicitlySet;
    private DateOnly? _date;
    private string _currency = Currency.ArgentinePeso;
    private decimal _currencyQuotation = 1m;
    private DateOnly? _serviceFrom;
    private DateOnly? _serviceTo;
    private DateOnly? _paymentDueDate;
    private decimal _nonTaxable;
    private decimal _exempt;
    private readonly List<VatLine> _vat = new();
    private decimal _otherTaxes;
    private decimal? _totalOverride;
    private decimal? _netOverride;
    private readonly List<InvoiceReference> _associated = new();

    private InvoiceBuilder() { }

    /// <summary>Starts building a comprobante of the specified type.</summary>
    public static InvoiceBuilder ForType(InvoiceType type) =>
        new() { _type = type };

    /// <summary>Sets the sales point.</summary>
    public InvoiceBuilder AtPointOfSale(int pointOfSale)
    {
        if (pointOfSale <= 0) throw new ArgumentOutOfRangeException(nameof(pointOfSale));
        _pointOfSale = pointOfSale;
        return this;
    }

    /// <summary>Sets the comprobante concept.</summary>
    public InvoiceBuilder WithConcept(Concept concept)
    {
        _concept = concept;
        return this;
    }

    /// <summary>Sets a registered receiver by CUIT. Defaults the VAT condition to <see cref="ReceiverVatCondition.RegisteredVat"/> — override with <see cref="WithReceiverVatCondition"/> if needed.</summary>
    public InvoiceBuilder ToCuit(long cuit)
    {
        _docType = DocumentType.Cuit;
        _docNumber = cuit;
        if (!_vatConditionExplicitlySet) _receiverVatCondition = ReceiverVatCondition.RegisteredVat;
        return this;
    }

    /// <summary>Sets an individual receiver by DNI. Defaults the VAT condition to <see cref="ReceiverVatCondition.ConsumerFinal"/>.</summary>
    public InvoiceBuilder ToDni(long dni)
    {
        _docType = DocumentType.Dni;
        _docNumber = dni;
        if (!_vatConditionExplicitlySet) _receiverVatCondition = ReceiverVatCondition.ConsumerFinal;
        return this;
    }

    /// <summary>Sets a generic anonymous "consumidor final" receiver.</summary>
    public InvoiceBuilder ToConsumerFinal()
    {
        _docType = DocumentType.ConsumidorFinal;
        _docNumber = 0;
        if (!_vatConditionExplicitlySet) _receiverVatCondition = ReceiverVatCondition.ConsumerFinal;
        return this;
    }

    /// <summary>Sets a custom receiver document.</summary>
    public InvoiceBuilder ToDocument(DocumentType type, long number)
    {
        _docType = type;
        _docNumber = number;
        return this;
    }

    /// <summary>Overrides the inferred VAT condition of the receiver. Mandatory since RG 5616/2024 for non-default cases (monotributo, exento, etc.).</summary>
    public InvoiceBuilder WithReceiverVatCondition(ReceiverVatCondition condition)
    {
        _receiverVatCondition = condition;
        _vatConditionExplicitlySet = true;
        return this;
    }

    /// <summary>Sets the comprobante date.</summary>
    public InvoiceBuilder WithDate(DateOnly date)
    {
        _date = date;
        return this;
    }

    /// <summary>Sets a non-ARS currency and its quotation against ARS.</summary>
    public InvoiceBuilder WithCurrency(string currencyCode, decimal quotation)
    {
        if (string.IsNullOrWhiteSpace(currencyCode)) throw new ArgumentException("Currency required.", nameof(currencyCode));
        if (quotation <= 0) throw new ArgumentOutOfRangeException(nameof(quotation));
        _currency = currencyCode;
        _currencyQuotation = quotation;
        return this;
    }

    /// <summary>Sets the service period (mandatory for service-based concepts).</summary>
    public InvoiceBuilder WithServicePeriod(DateOnly from, DateOnly to, DateOnly paymentDue)
    {
        _serviceFrom = from;
        _serviceTo = to;
        _paymentDueDate = paymentDue;
        return this;
    }

    /// <summary>Adds a VAT-taxed line. The amount is computed as <paramref name="net"/> × rate.</summary>
    /// <param name="net">Net amount before VAT.</param>
    /// <param name="rate">VAT rate.</param>
    public InvoiceBuilder WithVatBase(decimal net, VatRate rate)
    {
        if (net < 0) throw new ArgumentOutOfRangeException(nameof(net));
        var amount = Math.Round(net * rate.ToMultiplier(), 2, MidpointRounding.AwayFromZero);
        _vat.Add(new VatLine(rate, net, amount));
        return this;
    }

    /// <summary>Adds a non-VAT-taxable amount (<c>ImpTotConc</c>).</summary>
    public InvoiceBuilder WithNonTaxableAmount(decimal amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        _nonTaxable = amount;
        return this;
    }

    /// <summary>Adds an exempt amount (<c>ImpOpEx</c>).</summary>
    public InvoiceBuilder WithExemptAmount(decimal amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        _exempt = amount;
        return this;
    }

    /// <summary>Adds an amount of other taxes (<c>ImpTrib</c>).</summary>
    public InvoiceBuilder WithOtherTaxes(decimal amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        _otherTaxes = amount;
        return this;
    }

    /// <summary>Associates this comprobante with a previous one (for ND/NC).</summary>
    public InvoiceBuilder AssociatedTo(InvoiceReference reference)
    {
        if (reference is null) throw new ArgumentNullException(nameof(reference));
        _associated.Add(reference);
        return this;
    }

    /// <summary>Overrides the computed total (rarely needed; only when the builder math diverges from a known total).</summary>
    public InvoiceBuilder WithTotalAmount(decimal total)
    {
        _totalOverride = total;
        return this;
    }

    /// <summary>Overrides the computed net amount (rarely needed).</summary>
    public InvoiceBuilder WithNetAmount(decimal net)
    {
        _netOverride = net;
        return this;
    }

    /// <summary>Produces the immutable <see cref="Invoice"/>.</summary>
    /// <exception cref="InvalidOperationException">When required fields are missing.</exception>
    public Invoice Build()
    {
        if (_type is null) throw new InvalidOperationException("Invoice type is required.");
        if (_pointOfSale is null) throw new InvalidOperationException("Point of sale is required.");
        if (_date is null) throw new InvalidOperationException("Date is required.");

        decimal vatBaseSum = 0;
        decimal vatAmountSum = 0;
        foreach (var line in _vat)
        {
            vatBaseSum += line.TaxableBase;
            vatAmountSum += line.Amount;
        }

        var net = _netOverride ?? vatBaseSum;
        var total = _totalOverride ?? (net + vatAmountSum + _nonTaxable + _exempt + _otherTaxes);

        return new Invoice
        {
            Type = _type.Value,
            PointOfSale = _pointOfSale.Value,
            Concept = _concept,
            ReceiverDocumentType = _docType,
            ReceiverDocumentNumber = _docNumber,
            ReceiverVatCondition = _receiverVatCondition,
            Date = _date.Value,
            CurrencyCode = _currency,
            CurrencyQuotation = _currencyQuotation,
            ServicePeriodStart = _serviceFrom,
            ServicePeriodEnd = _serviceTo,
            PaymentDueDate = _paymentDueDate,
            NetAmount = net,
            NonTaxableAmount = _nonTaxable,
            ExemptAmount = _exempt,
            VatLines = _vat.AsReadOnly(),
            OtherTaxesAmount = _otherTaxes,
            TotalAmount = total,
            AssociatedInvoices = _associated.AsReadOnly(),
        };
    }
}
