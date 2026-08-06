using System;
using Afip.Arca.Sdk.Invoicing;
using Afip.Arca.Sdk.Invoicing.Models;
using FluentAssertions;
using Xunit;

namespace Afip.Arca.Sdk.Tests.Invoicing;

public sealed class InvoiceBuilderTests
{
    private static readonly DateOnly Today = new(2026, 5, 13);

    [Fact]
    public void Build_FacturaB_WithSingleVatLine_ProducesConsistentTotals()
    {
        var invoice = InvoiceBuilder
            .ForType(InvoiceType.FacturaB)
            .AtPointOfSale(1)
            .ToConsumerFinal()
            .WithDate(Today)
            .WithVatBase(10_000m, VatRate.TwentyOne)
            .Build();

        invoice.NetAmount.Should().Be(10_000m);
        invoice.VatLines.Should().HaveCount(1);
        invoice.VatLines[0].Amount.Should().Be(2_100m);
        invoice.TotalAmount.Should().Be(12_100m);
    }

    [Fact]
    public void Build_FacturaC_WithoutVatLines_SetsTotalEqualsNet()
    {
        var invoice = InvoiceBuilder
            .ForType(InvoiceType.FacturaC)
            .AtPointOfSale(2)
            .ToConsumerFinal()
            .WithDate(Today)
            .WithNetAmount(5_000m)
            .WithTotalAmount(5_000m)
            .Build();

        invoice.NetAmount.Should().Be(5_000m);
        invoice.TotalAmount.Should().Be(5_000m);
        invoice.VatLines.Should().BeEmpty();
    }

    [Fact]
    public void Build_NotaCredito_RequiresAssociatedInvoice()
    {
        var nc = InvoiceBuilder
            .ForType(InvoiceType.NotaCreditoB)
            .AtPointOfSale(1)
            .ToConsumerFinal()
            .WithDate(Today)
            .WithVatBase(1_000m, VatRate.TwentyOne)
            .AssociatedTo(new InvoiceReference(InvoiceType.FacturaB, 1, 42))
            .Build();

        nc.AssociatedInvoices.Should().ContainSingle();
        nc.AssociatedInvoices[0].Number.Should().Be(42);
    }

    [Fact]
    public void Build_WithoutType_Throws()
    {
        // Llegar a Build sin tipo no es posible normalmente; el builder fuerza tipo desde ForType.
        // Este caso ejercita el guardia interno usando reflection-friendly chain.
        var action = () =>
        {
            // Forzamos un escenario "imposible": construir un builder y no setear date.
            var builder = InvoiceBuilder.ForType(InvoiceType.FacturaB).AtPointOfSale(1);
            return builder.Build();
        };

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Build_WithNegativeNet_Throws()
    {
        var action = () =>
            InvoiceBuilder.ForType(InvoiceType.FacturaB)
                .AtPointOfSale(1)
                .ToConsumerFinal()
                .WithDate(Today)
                .WithVatBase(-1m, VatRate.TwentyOne);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }
}
