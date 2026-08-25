using System;
using System.Threading;
using System.Threading.Tasks;
using Afip.Arca.Sdk.Common.Exceptions;
using Afip.Arca.Sdk.Configuration;
using Microsoft.Extensions.Options;

namespace Afip.Arca.Sdk.Authentication;

/// <summary>
/// <see cref="IAccessTicketProvider"/> implementation that delegates ticket acquisition
/// to an external callback. Useful for scenarios where signing is performed by a
/// dedicated service (HSM, key vault, sidecar).
/// </summary>
public sealed class ExternalAccessTicketProvider : IAccessTicketProvider, IInvalidatableAccessTicketProvider, IDisposable
{
    private readonly IAccessTicketCache _cache;
    private readonly IOptionsMonitor<AfipOptions> _options;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Initializes a new instance of the <see cref="ExternalAccessTicketProvider"/> class.</summary>
    public ExternalAccessTicketProvider(IAccessTicketCache cache, IOptionsMonitor<AfipOptions> options)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<AccessTicket> GetAsync(string service, CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue;
        if (opts.ExternalTicketProvider is null)
        {
            throw new AfipAuthenticationException("ExternalTicketProvider is not configured.");
        }

        var cuit = opts.Cuit;

        if (_cache.TryGet(cuit, service, out var cached) && cached is not null)
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cache.TryGet(cuit, service, out cached) && cached is not null)
            {
                return cached;
            }

            var ticket = await opts.ExternalTicketProvider(service, cancellationToken).ConfigureAwait(false);
            if (ticket is null)
            {
                throw new AfipAuthenticationException("External ticket provider returned null.");
            }

            _cache.Set(ticket);
            return ticket;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Invalidate(string service)
    {
        if (string.IsNullOrWhiteSpace(service)) throw new ArgumentException("Service required.", nameof(service));
        _cache.Invalidate(_options.CurrentValue.Cuit, service);
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();
}
