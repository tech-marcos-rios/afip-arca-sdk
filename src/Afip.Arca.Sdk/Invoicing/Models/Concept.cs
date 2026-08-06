namespace Afip.Arca.Sdk.Invoicing.Models;

/// <summary>Concept code (<c>Concepto</c>) describing what the comprobante invoices.</summary>
public enum Concept
{
    /// <summary>Products.</summary>
    Products = 1,
    /// <summary>Services.</summary>
    Services = 2,
    /// <summary>Mixed: products and services.</summary>
    ProductsAndServices = 3,
}
