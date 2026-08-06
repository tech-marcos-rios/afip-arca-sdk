namespace Afip.Arca.Sdk.Invoicing.Models;

/// <summary>
/// VAT condition of the invoice receiver, mandatory since RG 5616/2024
/// (AFIP, code <c>CondicionIVAReceptorId</c>). Values come from the table returned
/// by <c>FEParamGetCondicionIvaReceptor</c>.
/// </summary>
public enum ReceiverVatCondition
{
    /// <summary>IVA Responsable Inscripto.</summary>
    RegisteredVat = 1,

    /// <summary>IVA Sujeto Exento.</summary>
    Exempt = 4,

    /// <summary>Consumidor Final.</summary>
    ConsumerFinal = 5,

    /// <summary>Responsable Monotributo.</summary>
    Monotributo = 6,

    /// <summary>Sujeto No Categorizado.</summary>
    Uncategorized = 7,

    /// <summary>Proveedor del Exterior.</summary>
    ForeignSupplier = 8,

    /// <summary>Cliente del Exterior.</summary>
    ForeignClient = 9,

    /// <summary>IVA Liberado — Ley Nº 19.640.</summary>
    VatExemptLaw19640 = 10,

    /// <summary>IVA Responsable Inscripto — Agente de Percepción.</summary>
    RegisteredVatCollectionAgent = 11,

    /// <summary>Monotributista Social.</summary>
    SocialMonotributo = 13,

    /// <summary>IVA No Alcanzado.</summary>
    NotApplicable = 15,

    /// <summary>Monotributo Trabajador Independiente Promovido.</summary>
    PromotedIndependentMonotributo = 16,
}
