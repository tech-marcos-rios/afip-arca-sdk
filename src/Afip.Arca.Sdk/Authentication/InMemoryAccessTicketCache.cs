using System;
using System.Collections.Concurrent;
using Afip.Arca.Sdk.Common.Time;
using Afip.Arca.Sdk.Configuration;
using Microsoft.Extensions.Options;

namespace Afip.Arca.Sdk.Authentication;

/// <summary>
/// Default <see cref="IAccessTicketCache"/> implementation backed by a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>. Suitable for single-process
/// scenarios. For multi-process deployments, replace this implementation with one
/// backed by Redis or another distributed cache.
/// </summary>
public sealed class InMemoryAccessTicketCache : IAccessTicketCache
{
    private readonly ConcurrentDictionary<string, AccessTicket> _store = new(StringComparer.OrdinalIgnoreCase);
    private readonly IClock _clock;
    private readonly TimeSpan _leeway;

    /// <summary>Initializes a new instance of the <see cref="InMemoryAccessTicketCache"/> class.</summary>
    public InMemoryAccessTicketCache(IClock clock, IOptions<AfipOptions> options)
    {
        _clock = clock;
        _leeway = TimeSpan.FromMinutes(options.Value.TicketRefreshLeewayMinutes);
    }

    /// <inheritdoc />
    public bool TryGet(string cuit, string service, out AccessTicket? ticket)
    {
        if (_store.TryGetValue(BuildKey(cuit, service), out var found) &&
            !found.IsExpired(_clock.UtcNow, _leeway))
        {
            ticket = found;
            return true;
        }

        ticket = null;
        return false;
    }

    /// <inheritdoc />
    public void Set(AccessTicket ticket)
    {
        if (ticket is null) throw new ArgumentNullException(nameof(ticket));
        _store[BuildKey(ticket.Cuit, ticket.Service)] = ticket;
    }

    /// <inheritdoc />
    public void Invalidate(string cuit, string service)
    {
        _store.TryRemove(BuildKey(cuit, service), out _);
    }

    private static string BuildKey(string cuit, string service) => cuit + "|" + service;
}
