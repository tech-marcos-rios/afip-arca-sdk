using System;
using Afip.Arca.Sdk.Common.Time;

namespace Afip.Arca.Sdk.Tests.Support;

internal sealed class FakeClock : IClock
{
    private DateTimeOffset _now;

    public FakeClock(DateTimeOffset initial) => _now = initial;

    public DateTimeOffset UtcNow => _now.ToUniversalTime();

    public DateTimeOffset ArgentinaNow => _now.ToOffset(TimeSpan.FromHours(-3));

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
