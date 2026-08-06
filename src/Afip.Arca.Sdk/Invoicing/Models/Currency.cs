namespace Afip.Arca.Sdk.Invoicing.Models;

/// <summary>
/// Currency code (<c>MonId</c>) as AFIP requires it. A subset of the most common
/// values is exposed; other currencies can be provided as raw string at the call site.
/// </summary>
public static class Currency
{
    /// <summary>Argentine peso (PES).</summary>
    public const string ArgentinePeso = "PES";
    /// <summary>US dollar (DOL).</summary>
    public const string UsDollar = "DOL";
    /// <summary>Euro (060).</summary>
    public const string Euro = "060";
}
