using System;

namespace Afip.Arca.Sdk.Common.Time;

/// <summary>
/// Default <see cref="IClock"/> implementation that delegates to the operating system.
/// Argentina has not observed Daylight Saving Time since 2009, so a fixed UTC-3 offset
/// is correct and avoids depending on time zone databases that differ between Windows
/// and Linux hosts.
/// </summary>
public sealed class SystemClock : IClock
{
    private static readonly TimeSpan ArgentinaOffset = TimeSpan.FromHours(-3);

    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public DateTimeOffset ArgentinaNow => DateTimeOffset.UtcNow.ToOffset(ArgentinaOffset);
}
