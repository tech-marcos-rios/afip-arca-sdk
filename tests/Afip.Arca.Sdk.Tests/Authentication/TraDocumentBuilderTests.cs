using System;
using System.Xml.Linq;
using Afip.Arca.Sdk.Authentication.Cms;
using Afip.Arca.Sdk.Tests.Support;
using FluentAssertions;
using Xunit;

namespace Afip.Arca.Sdk.Tests.Authentication;

public sealed class TraDocumentBuilderTests
{
    [Fact]
    public void Build_ProducesExpectedSchema()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 13, 13, 0, 0, TimeSpan.Zero));
        var sut = new TraDocumentBuilder(clock, validityMinutes: 10);

        var xml = sut.Build("wsfe");
        var doc = XDocument.Parse(xml);

        doc.Root!.Name.LocalName.Should().Be("loginTicketRequest");
        doc.Root.Element("service")!.Value.Should().Be("wsfe");
        doc.Root.Element("header")!.Element("uniqueId").Should().NotBeNull();
        doc.Root.Element("header")!.Element("generationTime").Should().NotBeNull();
        doc.Root.Element("header")!.Element("expirationTime").Should().NotBeNull();
    }

    [Fact]
    public void Build_TwoCallsInSameSecond_ProduceDifferentUniqueIds()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 13, 13, 0, 0, TimeSpan.Zero));
        var sut = new TraDocumentBuilder(clock, validityMinutes: 10);

        var first = ExtractUniqueId(sut.Build("wsfe"));
        var second = ExtractUniqueId(sut.Build("wsfe"));

        first.Should().NotBe(second);
    }

    private static long ExtractUniqueId(string xml)
    {
        var doc = XDocument.Parse(xml);
        return long.Parse(doc.Root!.Element("header")!.Element("uniqueId")!.Value);
    }
}
