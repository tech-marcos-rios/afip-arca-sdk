namespace Afip.Arca.Sdk.IncomeTax.Reporting.Models;

/// <summary>Withheld subject's status in the AFIP padrón.</summary>
public enum SubjectCondition
{
    /// <summary>Registered (inscripto).</summary>
    Registered = 1,

    /// <summary>Not registered (no inscripto).</summary>
    NotRegistered = 2,

    /// <summary>Excluded by court order or padrón.</summary>
    Excluded = 3,
}
