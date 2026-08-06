using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Afip.Arca.Sdk.MultiTenancy;
using Afip.Arca.Sdk.Demo.Data;
using Microsoft.EntityFrameworkCore;

namespace Afip.Arca.Sdk.Demo.Services;

/// <summary>
/// Handles the lifecycle of AFIP tenant configurations: register, update, deactivate.
/// This is the "admin" workflow that runs before (or alongside) the normal billing flow.
/// </summary>
internal sealed class TenantOnboardingService
{
    private readonly IDbContextFactory<AfipDemoDbContext> _dbFactory;
    private readonly AesCertificateEncryption _encryption;
    private readonly IAfipClientFactory _factory;

    public TenantOnboardingService(
        IDbContextFactory<AfipDemoDbContext> dbFactory,
        AesCertificateEncryption encryption,
        IAfipClientFactory factory)
    {
        _dbFactory = dbFactory;
        _encryption = encryption;
        _factory = factory;
    }

    /// <summary>
    /// Registers a new tenant or updates an existing one. If the tenant already exists,
    /// its cached client is invalidated so the next billing call picks up the new cert.
    /// </summary>
    public async Task RegisterOrUpdateAsync(
        string tenantId,
        string displayName,
        string cuit,
        bool useHomologation,
        string pfxPath,
        string pfxPassword,
        CancellationToken ct)
    {
        var pfxBytes = await File.ReadAllBytesAsync(pfxPath, ct);

        var (certCipher, certNonce, certTag) = _encryption.Encrypt(pfxBytes);
        var (pwdCipher, pwdNonce, pwdTag) = _encryption.EncryptString(pfxPassword);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var existing = await db.TenantConfigs
            .FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);

        if (existing is not null)
        {
            existing.DisplayName = displayName;
            existing.Cuit = cuit;
            existing.UseHomologation = useHomologation;
            existing.CertificateEncrypted = certCipher;
            existing.CertificateNonce = certNonce;
            existing.CertificateTag = certTag;
            existing.PasswordEncrypted = pwdCipher;
            existing.PasswordNonce = pwdNonce;
            existing.PasswordTag = pwdTag;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.IsActive = true;

            await db.SaveChangesAsync(ct);

            // Force the factory to rebuild the client with the new certificate.
            _factory.InvalidateClient(tenantId);
            return;
        }

        db.TenantConfigs.Add(new TenantAfipConfig
        {
            TenantId = tenantId,
            DisplayName = displayName,
            Cuit = cuit,
            UseHomologation = useHomologation,
            CertificateEncrypted = certCipher,
            CertificateNonce = certNonce,
            CertificateTag = certTag,
            PasswordEncrypted = pwdCipher,
            PasswordNonce = pwdNonce,
            PasswordTag = pwdTag,
        });

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Marks the tenant as inactive and removes its cached client.
    /// Subsequent calls to <c>IAfipClientFactory.GetClientAsync</c> for this tenant
    /// will throw <see cref="TenantNotFoundException"/>.
    /// </summary>
    public async Task DeactivateAsync(string tenantId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var config = await db.TenantConfigs
            .FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);

        if (config is null) return;

        config.IsActive = false;
        config.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        _factory.InvalidateClient(tenantId);
    }
}
