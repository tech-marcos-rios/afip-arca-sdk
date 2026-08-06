using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Afip.Arca.Sdk.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Afip.Arca.Sdk.MultiTenancy;

/// <summary>
/// Default <see cref="IAfipClientFactory"/> implementation.
/// Creates one isolated DI child container (and therefore one isolated
/// <see cref="IAfipClient"/>) per tenant, caches it, and disposes containers on
/// invalidation or when the factory itself is disposed.
/// HTTP transport and logging are shared from the root container; everything
/// tenant-specific (options, certificate, TA cache) lives in the child container.
/// </summary>
public sealed class DynamicAfipClientFactory : IAfipClientFactory, IDisposable
{
    private readonly ITenantOptionsProvider _provider;
    private readonly IServiceProvider _root;
    private readonly ConcurrentDictionary<string, (ServiceProvider Container, IAfipClient Client)> _cache
        = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _buildLock = new(1, 1);
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="DynamicAfipClientFactory"/> class.</summary>
    public DynamicAfipClientFactory(ITenantOptionsProvider provider, IServiceProvider root)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _root = root ?? throw new ArgumentNullException(nameof(root));
    }

    /// <inheritdoc />
    public async Task<IAfipClient> GetClientAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("TenantId required.", nameof(tenantId));
        if (_disposed) throw new ObjectDisposedException(nameof(DynamicAfipClientFactory));

        if (_cache.TryGetValue(tenantId, out var cached))
            return cached.Client;

        await _buildLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-checked: another thread may have built it while we waited.
            if (_cache.TryGetValue(tenantId, out cached))
                return cached.Client;

            var opts = await _provider.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
            if (opts is null)
                throw new TenantNotFoundException(tenantId);

            var entry = BuildEntry(opts);
            _cache[tenantId] = entry;
            return entry.Client;
        }
        finally
        {
            _buildLock.Release();
        }
    }

    /// <inheritdoc />
    public void InvalidateClient(string tenantId)
    {
        if (_cache.TryRemove(tenantId, out var entry))
            entry.Container.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var entry in _cache.Values)
            entry.Container.Dispose();
        _cache.Clear();
        _buildLock.Dispose();
    }

    private (ServiceProvider Container, IAfipClient Client) BuildEntry(TenantAfipOptions tenantOpts)
    {
        var child = new ServiceCollection();

        // Share the HTTP factory from root — it already has the named AFIP client
        // configured with retry + timeout policies, so we don't register it again.
        child.AddSingleton(_root.GetRequiredService<IHttpClientFactory>());

        // Share the logger factory so tenant logs go to the same sink as the host.
        var loggerFactory = _root.GetRequiredService<ILoggerFactory>();
        child.AddSingleton<ILoggerFactory>(loggerFactory);
        child.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        // Register all per-tenant AFIP services (options, cert, TA cache, SOAP clients…)
        // without re-registering the HttpClient (already above).
        ServiceCollectionExtensions.RegisterAfipSdkServices(child, opts =>
        {
            opts.Environment = tenantOpts.Environment;
            opts.Cuit = tenantOpts.Cuit;
            opts.UseLocalCertificateSigning(c =>
                c.FromBytes(tenantOpts.CertificateBytes, tenantOpts.CertificatePassword));
        });

        var container = child.BuildServiceProvider();
        return (container, container.GetRequiredService<IAfipClient>());
    }
}
