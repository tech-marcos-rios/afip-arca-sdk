using System;

namespace Afip.Arca.Sdk.Common.Time;

/// <summary>
/// Abstraction over the system clock to keep time-dependent code unit-testable.
/// Production code should depend on this interface instead of calling
/// <see cref="DateTime"/>.<c>UtcNow</c> directly.
/// </summary>
public interface IClock
{
    /// <summary>Current UTC instant.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Current instant in the Argentina time zone (UTC-3, used by AFIP).</summary>
    DateTimeOffset ArgentinaNow { get; }
}
