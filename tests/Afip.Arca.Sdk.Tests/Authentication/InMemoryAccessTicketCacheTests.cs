using System;
using Afip.Arca.Sdk.Authentication;
using Afip.Arca.Sdk.Configuration;
using Afip.Arca.Sdk.Tests.Support;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Afip.Arca.Sdk.Tests.Authentication;

public sealed class InMemoryAccessTicketCacheTests
{
    private static readonly DateTimeOffset BaseNow = new(2026, 5, 13, 10, 0, 0, TimeSpan.Zero);

    private static InMemoryAccessTicketCache BuildSut(FakeClock clock)
    {
        var opts = Options.Create(new AfipOptions { TicketRefreshLeewayMinutes = 5 });
        return new InMemoryAccessTicketCache(clock, opts);
    }

    [Fact]
    public void TryGet_WhenEmpty_ReturnsFalse()
    {
        var sut = BuildSut(new FakeClock(BaseNow));

        sut.TryGet("20123456789", "wsfe", out var ticket).Should().BeFalse();
        ticket.Should().BeNull();
    }

    [Fact]
    public void TryGet_AfterSet_ReturnsTheTicket()
    {
        var clock = new FakeClock(BaseNow);
        var sut = BuildSut(clock);

        var ticket = new AccessTicket("wsfe", "20123456789", "token", "sign",
            BaseNow, BaseNow.AddHours(12));
        sut.Set(ticket);

        sut.TryGet("20123456789", "wsfe", out var found).Should().BeTrue();
        found.Should().Be(ticket);
    }

    [Fact]
    public void TryGet_WhenTicketExpiredWithinLeeway_ReturnsFalse()
    {
        var clock = new FakeClock(BaseNow);
        var sut = BuildSut(clock);

        var ticket = new AccessTicket("wsfe", "20123456789", "token", "sign",
            BaseNow, BaseNow.AddMinutes(4)); // expira en 4 min, leeway 5
        sut.Set(ticket);

        sut.TryGet("20123456789", "wsfe", out var _).Should().BeFalse();
    }

    [Fact]
    public void Invalidate_RemovesEntry()
    {
        var sut = BuildSut(new FakeClock(BaseNow));
        var ticket = new AccessTicket("wsfe", "20123456789", "t", "s",
            BaseNow, BaseNow.AddHours(12));
        sut.Set(ticket);

        sut.Invalidate("20123456789", "wsfe");

        sut.TryGet("20123456789", "wsfe", out _).Should().BeFalse();
    }

    [Fact]
    public void Cache_IsKeyedByCuitAndService()
    {
        var sut = BuildSut(new FakeClock(BaseNow));
        var wsfe = new AccessTicket("wsfe", "20123456789", "t1", "s1",
            BaseNow, BaseNow.AddHours(12));
        var sire = new AccessTicket("sire-ws", "20123456789", "t2", "s2",
            BaseNow, BaseNow.AddHours(12));

        sut.Set(wsfe);
        sut.Set(sire);

        sut.TryGet("20123456789", "wsfe", out var foundWsfe).Should().BeTrue();
        sut.TryGet("20123456789", "sire-ws", out var foundSire).Should().BeTrue();
        foundWsfe!.Token.Should().Be("t1");
        foundSire!.Token.Should().Be("t2");
    }
}
