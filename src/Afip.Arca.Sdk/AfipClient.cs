using System;
using Afip.Arca.Sdk.IncomeTax.Calculation;
using Afip.Arca.Sdk.IncomeTax.Reporting;
using Afip.Arca.Sdk.Invoicing;

namespace Afip.Arca.Sdk;

/// <summary>
/// Default <see cref="IAfipClient"/> implementation. Just a thin aggregate over the
/// three submodules — no logic, no state.
/// </summary>
public sealed class AfipClient : IAfipClient
{
    /// <summary>Initializes a new instance of the <see cref="AfipClient"/> class.</summary>
    public AfipClient(
        IInvoiceService invoicing,
        IIncomeTaxCalculator incomeTaxCalculator,
        ISireService sire)
    {
        Invoicing = invoicing ?? throw new ArgumentNullException(nameof(invoicing));
        IncomeTaxCalculator = incomeTaxCalculator ?? throw new ArgumentNullException(nameof(incomeTaxCalculator));
        Sire = sire ?? throw new ArgumentNullException(nameof(sire));
    }

    /// <inheritdoc />
    public IInvoiceService Invoicing { get; }

    /// <inheritdoc />
    public IIncomeTaxCalculator IncomeTaxCalculator { get; }

    /// <inheritdoc />
    public ISireService Sire { get; }
}
