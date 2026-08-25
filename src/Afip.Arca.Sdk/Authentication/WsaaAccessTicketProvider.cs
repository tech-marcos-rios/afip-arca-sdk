using System;
using System.Threading;
using System.Threading.Tasks;
using Afip.Arca.Sdk.Authentication.Cms;
using Afip.Arca.Sdk.Authentication.Soap;
using Afip.Arca.Sdk.Common.Exceptions;
using Afip.Arca.Sdk.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Afip.Arca.Sdk.Authentication;

/// <summary>
/// <see cref="IAccessTicketProvider"/> implementation that signs the TRA locally with
/// an X.509 certificate and calls WSAA's <c>loginCms</c> to obtain the TA.
/// </summary>
public sealed class WsaaAccessTicketProvider : IAccessTicketProvider, IInvalidatableAccessTicketProvider, IDisposable
{
    private readonly IAccessTicketCache _cache;
    private readonly TraDocumentBuilder _traBuilder;
    private readonly ITraSigner _signer;
    private readonly WsaaSoapClient _soapClient;
    private readonly IOptionsMonitor<AfipOptions> _options;
    private readonly ILogger<WsaaAccessTicketProvider> _logger;
    private readonly SemaphoreSlim _gate = new(initialCount: 1, maxCount: 1);

    /// <summary>Initializes a new instance of the <see cref="WsaaAccessTicketProvider"/> class.</summary>
    public WsaaAccessTicketProvider(
        IAccessTicketCache cache,
        TraDocumentBuilder traBuilder,
        ITraSigner signer,
        WsaaSoapClient soapClient,
        IOptionsMonitor<AfipOptions> options,
        ILogger<WsaaAccessTicketProvider> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _traBuilder = traBuilder ?? throw new ArgumentNullException(nameof(traBuilder));
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
        _soapClient = soapClient ?? throw new ArgumentNullException(nameof(soapClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<AccessTicket> GetAsync(string service, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(service)) throw new ArgumentException("Service required.", nameof(service));

        var opts = _options.CurrentValue;
        var cuit = opts.Cuit;
        if (string.IsNullOrWhiteSpace(cuit))
        {
            throw new AfipAuthenticationException("AfipOptions.Cuit is not configured.");
        }

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

            _logger.LogInformation("Requesting new TA from WSAA for service {Service}", service);

            var traXml = _traBuilder.Build(service);
            var cms = _signer.Sign(traXml);
            var ticket = await _soapClient.LoginCmsAsync(service, cuit, cms, cancellationToken).ConfigureAwait(false);
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
