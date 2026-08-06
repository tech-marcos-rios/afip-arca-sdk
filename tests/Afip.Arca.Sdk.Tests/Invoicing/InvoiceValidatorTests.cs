using System;
using Afip.Arca.Sdk.Invoicing;
using Afip.Arca.Sdk.Invoicing.Models;
using Afip.Arca.Sdk.Invoicing.Validation;
using FluentAssertions;
using Xunit;

namespace Afip.Arca.Sdk.Tests.Invoicing;

public sealed class InvoiceValidatorTests
{
    private readonly InvoiceValidator _sut = new();
    private static readonly DateOnly Today = new(2026, 5, 13);

    [Fact]
    public void Validate_WhenInvoiceIsValid_ReturnsEmpty()
    {
        var invoice = InvoiceBuilder
            .ForType(InvoiceType.FacturaB)
            .AtPointOfSale(1)
            .ToConsumerFinal()
            .WithDate(Today)
            .WithVatBase(1_000m, VatRate.TwentyOne)
            .Build();

        _sut.Validate(invoice).Should().BeEmpty();
    }

    [Fact]
    public void Validate_WhenServiceConceptMissingDates_Fails()
    {
        var invoice = InvoiceBuilder
            .ForType(InvoiceType.FacturaB)
            .AtPointOfSale(1)
            .WithConcept(Concept.Services)
            .ToConsumerFinal()
            .WithDate(Today)
            .WithVatBase(1_000m, VatRate.TwentyOne)
            .Build();

        _sut.Validate(invoice).Should()
            .Contain(s => s.Contains("Service-based concepts require"));
    }

    [Fact]
    public void Validate_FacturaA_WithNonZeroNetAndNoVatLines_Fails()
    {
        var invoice = InvoiceBuilder
            .ForType(InvoiceType.FacturaA)
            .AtPointOfSale(1)
            .ToCuit(20123456780)
            .WithDate(Today)
            .WithNetAmount(1_000m)
            .WithTotalAmount(1_000m)
            .Build();

        _sut.Validate(invoice).Should()
            .Contain(s => s.Contains("require at least one VAT line"));
    }

    [Fact]
    public void Validate_WhenTotalsDoNotClose_Fails()
    {
        var invoice = InvoiceBuilder
            .ForType(InvoiceType.FacturaB)
            .AtPointOfSale(1)
            .ToConsumerFinal()
            .WithDate(Today)
            .WithVatBase(100m, VatRate.TwentyOne)
            .WithTotalAmount(200m)
            .Build();

        _sut.Validate(invoice).Should()
            .Contain(s => s.Contains("does not match the sum"));
    }

    [Fact]
    public void Validate_CreditNote_WithoutAssociatedInvoice_Fails()
    {
        var nc = InvoiceBuilder
            .ForType(InvoiceType.NotaCreditoB)
            .AtPointOfSale(1)
            .ToConsumerFinal()
            .WithDate(Today)
            .WithVatBase(100m, VatRate.TwentyOne)
            .Build();

        _sut.Validate(nc).Should()
            .Contain(s => s.Contains("must reference at least one original"));
    }

    [Fact]
    public void Validate_Cuit_MustBe11Digits()
    {
        var invoice = InvoiceBuilder
            .ForType(InvoiceType.FacturaB)
            .AtPointOfSale(1)
            .ToDocument(DocumentType.Cuit, 12345)
            .WithDate(Today)
            .WithVatBase(100m, VatRate.TwentyOne)
            .Build();

        _sut.Validate(invoice).Should()
            .Contain(s => s.Contains("must be 11 digits"));
    }
}
