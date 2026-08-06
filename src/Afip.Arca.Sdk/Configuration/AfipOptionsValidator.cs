using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace Afip.Arca.Sdk.Configuration;

/// <summary>
/// Validates <see cref="AfipOptions"/> at startup so misconfiguration fails fast
/// instead of producing cryptic errors on the first AFIP call.
/// </summary>
public sealed class AfipOptionsValidator : IValidateOptions<AfipOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AfipOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Cuit))
        {
            failures.Add("AfipOptions.Cuit is required.");
        }
        else if (options.Cuit.Length != 11 || !IsAllDigits(options.Cuit))
        {
            failures.Add("AfipOptions.Cuit must be 11 numeric digits.");
        }

        if (options.CertificateSigning is null && options.ExternalTicketProvider is null)
        {
            failures.Add("Either UseLocalCertificateSigning(...) or UseExternalTicketProvider(...) must be configured.");
        }

        if (options.CertificateSigning is not null && options.CertificateSigning.Certificate is null)
        {
            failures.Add("CertificateSigningOptions has no certificate loaded.");
        }

        if (options.TicketRefreshLeewayMinutes < 0)
        {
            failures.Add("TicketRefreshLeewayMinutes must be >= 0.");
        }

        if (options.TraValidityMinutes <= 0)
        {
            failures.Add("TraValidityMinutes must be > 0.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsAllDigits(string s)
    {
        foreach (var c in s)
        {
            if (c < '0' || c > '9') return false;
        }
        return true;
    }
}
