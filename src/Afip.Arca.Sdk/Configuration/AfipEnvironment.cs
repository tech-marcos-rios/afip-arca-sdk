namespace Afip.Arca.Sdk.Configuration;

/// <summary>
/// AFIP/ARCA environment selector. Determines which set of endpoints the SDK will hit.
/// </summary>
public enum AfipEnvironment
{
    /// <summary>Testing environment (homologación). Free, requires CN registered in WSASS.</summary>
    Homologation = 0,

    /// <summary>Production environment. Requires a certificate issued by the Digital Certificate Administrator.</summary>
    Production = 1,
}
