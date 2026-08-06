using System;
using System.Threading;
using System.Xml.Linq;
using Afip.Arca.Sdk.Common.Time;

namespace Afip.Arca.Sdk.Authentication.Cms;

/// <summary>
/// Builds the WSAA <c>loginTicketRequest</c> (TRA) XML document.
/// </summary>
/// <remarks>
/// AFIP requires the <c>uniqueId</c> to be strictly increasing across requests
/// targeting the same <c>(cuit, service)</c> pair. This builder uses
/// <see cref="DateTimeOffset.ToUnixTimeSeconds"/> combined with an in-process
/// counter to guarantee uniqueness even when the call rate exceeds one per second.
/// </remarks>
public sealed class TraDocumentBuilder
{
    private readonly IClock _clock;
    private readonly int _validityMinutes;
    private long _counter;

    /// <summary>Initializes a new instance of the <see cref="TraDocumentBuilder"/> class.</summary>
    /// <param name="clock">Clock abstraction.</param>
    /// <param name="validityMinutes">How many minutes the TRA stays valid (typically 10).</param>
    public TraDocumentBuilder(IClock clock, int validityMinutes)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        if (validityMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(validityMinutes));
        _validityMinutes = validityMinutes;
    }

    /// <summary>Builds a fresh TRA XML for the given service.</summary>
    /// <param name="service">AFIP service identifier (e.g. <c>wsfe</c>).</param>
    public string Build(string service)
    {
        if (string.IsNullOrWhiteSpace(service)) throw new ArgumentException("Service required.", nameof(service));

        var now = _clock.ArgentinaNow;
        var uniqueId = now.ToUnixTimeSeconds() + Interlocked.Increment(ref _counter);

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("loginTicketRequest",
                new XAttribute("version", "1.0"),
                new XElement("header",
                    new XElement("uniqueId", uniqueId),
                    new XElement("generationTime", FormatXsd(now)),
                    new XElement("expirationTime", FormatXsd(now.AddMinutes(_validityMinutes)))),
                new XElement("service", service)));

        return doc.Declaration + doc.ToString(SaveOptions.DisableFormatting);
    }

    private static string FormatXsd(DateTimeOffset value) =>
        value.ToString("yyyy-MM-ddTHH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);
}
