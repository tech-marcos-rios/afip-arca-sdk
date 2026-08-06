using System.Threading;
using System.Threading.Tasks;
using Afip.Arca.Sdk.Configuration;
using Afip.Arca.Sdk.MultiTenancy;
using Afip.Arca.Sdk.Demo.Data;
using Microsoft.EntityFrameworkCore;

namespace Afip.Arca.Sdk.Demo.Services;

/// <summary>
/// <see cref="ITenantOptionsProvider"/> backed by <see cref="AfipDemoDbContext"/> (SQLite).
/// Decrypts the certificate and password before returning them to the factory.
/// </summary>
internal sealed class DbTenantOptionsProvider : ITenantOptionsProvider
{
    private readonly IDbContextFactory<AfipDemoDbContext> _dbFactory;
    private readonly AesCertificateEncryption _encryption;

    public DbTenantOptionsProvider(
        IDbContextFactory<AfipDemoDbContext> dbFactory,
        AesCertificateEncryption encryption)
    {
        _dbFactory = dbFactory;
        _encryption = encryption;
    }

    public async Task<TenantAfipOptions?> GetAsync(string tenantId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var config = await db.TenantConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.IsActive, cancellationToken);

        if (config is null) return null;

        var certBytes = _encryption.Decrypt(
            config.CertificateEncrypted, config.CertificateNonce, config.CertificateTag);

        var password = _encryption.DecryptString(
            config.PasswordEncrypted, config.PasswordNonce, config.PasswordTag);

        return new TenantAfipOptions
        {
            TenantId = tenantId,
            Cuit = config.Cuit,
            Environment = config.UseHomologation
                ? AfipEnvironment.Homologation
                : AfipEnvironment.Production,
            CertificateBytes = certBytes,
            CertificatePassword = password,
        };
    }
}
