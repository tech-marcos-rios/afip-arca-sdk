using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Afip.Arca.Sdk.Authentication;
using Afip.Arca.Sdk.Common.Exceptions;
using Afip.Arca.Sdk.Common.Soap;
using Afip.Arca.Sdk.Configuration;
using Afip.Arca.Sdk.Invoicing;
using Afip.Arca.Sdk.Invoicing.Models;
using Afip.Arca.Sdk.Invoicing.Soap;
using Afip.Arca.Sdk.Invoicing.Validation;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Afip.Arca.Sdk.Tests.Invoicing;

public sealed class InvoiceServiceTests
{
    private static readonly XNamespace Ar = "http://ar.gov.afip.dif.FEV1/";
    private static readonly DateOnly Today = new(2026, 5, 13);

    private sealed class FakeInvalidatableTicketProvider : IAccessTicketProvider, IInvalidatableAccessTicketProvider
    {
        public int InvalidateCallCount { get; private set; }

        public Task<AccessTicket> GetAsync(string service, CancellationToken cancellationToken) =>
            Task.FromResult(new AccessTicket(service, "20123456789", "token", "sign",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(12)));

        public void Invalidate(string service) => InvalidateCallCount++;
    }

    private static IOptionsMonitor<AfipOptions> BuildOptionsMonitor()
    {
        var options = new AfipOptions { Cuit = "20123456789", Environment = AfipEnvironment.Homologation };
        var monitor = Substitute.For<IOptionsMonitor<AfipOptions>>();
        monitor.CurrentValue.Returns(options);
        return monitor;
    }

    private static Invoice BuildValidInvoice() =>
        InvoiceBuilder
            .ForType(InvoiceType.FacturaB)
            .AtPointOfSale(1)
            .ToConsumerFinal()
            .WithDate(Today)
            .WithVatBase(1_000m, VatRate.TwentyOne)
            .Build();

    private static XElement TokenErrorEnvelope(string resultElementName) =>
        new(Ar + resultElementName,
            new XElement(Ar + "Errors",
                new XElement(Ar + "Err",
                    new XElement(Ar + "Code", 1000),
                    new XElement(Ar + "Msg", "Token inválido o vencido"))));

    private static XElement LastNumberEnvelope(long number) =>
        new(Ar + "FECompUltimoAutorizadoResult", new XElement(Ar + "CbteNro", number));

    private static XElement AuthorizeSuccessEnvelope(long number) =>
        new(Ar + "FECAESolicitarResult",
            new XElement(Ar + "FECAEDetResponse",
                new XElement(Ar + "Resultado", "A"),
                new XElement(Ar + "CAE", "86200173262441"),
                new XElement(Ar + "CAEFchVto", "20260601"),
                new XElement(Ar + "CbteDesde", number)));

    [Fact]
    public async Task GetLastAuthorizedNumberAsync_WhenTokenIsInvalidAndProviderIsInvalidatable_RetriesOnceWithFreshTicket()
    {
        var invoker = Substitute.For<IHttpSoapInvoker>();
        invoker.InvokeAsync(Arg.Any<Uri>(), Arg.Any<string>(), Arg.Any<XElement>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TokenErrorEnvelope("FECompUltimoAutorizadoResult")), Task.FromResult(LastNumberEnvelope(42)));

        var soap = new WsfeSoapClient(invoker, BuildOptionsMonitor());
        var ticketProvider = new FakeInvalidatableTicketProvider();
        var sut = new InvoiceService(ticketProvider, soap, new InvoiceValidator(), NullLogger<InvoiceService>.Instance);

        var result = await sut.GetLastAuthorizedNumberAsync(InvoiceType.FacturaB, 1, CancellationToken.None);

        result.Should().Be(42);
        ticketProvider.InvalidateCallCount.Should().Be(1);
        await invoker.Received(2).InvokeAsync(Arg.Any<Uri>(), Arg.Any<string>(), Arg.Any<XElement>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetLastAuthorizedNumberAsync_WhenProviderCannotInvalidate_PropagatesTheOriginalError()
    {
        var invoker = Substitute.For<IHttpSoapInvoker>();
        invoker.InvokeAsync(Arg.Any<Uri>(), Arg.Any<string>(), Arg.Any<XElement>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TokenErrorEnvelope("FECompUltimoAutorizadoResult")));

        var soap = new WsfeSoapClient(invoker, BuildOptionsMonitor());
        var ticketProvider = Substitute.For<IAccessTicketProvider>();
        ticketProvider.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AccessTicket("wsfe", "20123456789", "t", "s",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(12))));

        var sut = new InvoiceService(ticketProvider, soap, new InvoiceValidator(), NullLogger<InvoiceService>.Instance);

        var act = () => sut.GetLastAuthorizedNumberAsync(InvoiceType.FacturaB, 1, CancellationToken.None);

        await act.Should().ThrowAsync<AfipBusinessException>();
        await invoker.Received(1).InvokeAsync(Arg.Any<Uri>(), Arg.Any<string>(), Arg.Any<XElement>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthorizeAsync_WhenWsfeRejectsWithInvalidTokenError_InvalidatesAndRetriesOnce()
    {
        var invoker = Substitute.For<IHttpSoapInvoker>();
        invoker.InvokeAsync(Arg.Any<Uri>(), Arg.Any<string>(), Arg.Any<XElement>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TokenErrorEnvelope("FECAESolicitarResult")), Task.FromResult(AuthorizeSuccessEnvelope(42)));

        var soap = new WsfeSoapClient(invoker, BuildOptionsMonitor());
        var ticketProvider = new FakeInvalidatableTicketProvider();
        var sut = new InvoiceService(ticketProvider, soap, new InvoiceValidator(), NullLogger<InvoiceService>.Instance);

        var result = await sut.AuthorizeAsync(BuildValidInvoice(), explicitNumber: 42, cancellationToken: CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Cae.Should().Be("86200173262441");
        ticketProvider.InvalidateCallCount.Should().Be(1);
    }
}
