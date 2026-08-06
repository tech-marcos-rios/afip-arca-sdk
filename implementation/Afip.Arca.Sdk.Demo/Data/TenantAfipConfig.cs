using System;
using System.ComponentModel.DataAnnotations;

namespace Afip.Arca.Sdk.Demo.Data;

internal sealed class TenantAfipConfig
{
    [Key, MaxLength(100)]
    public string TenantId { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, MaxLength(11)]
    public string Cuit { get; set; } = string.Empty;

    public bool UseHomologation { get; set; } = true;

    // PFX bytes encrypted with AES-256-GCM
    public byte[] CertificateEncrypted { get; set; } = Array.Empty<byte>();
    public byte[] CertificateNonce { get; set; } = Array.Empty<byte>();
    public byte[] CertificateTag { get; set; } = Array.Empty<byte>();

    // PFX password encrypted with AES-256-GCM
    public byte[] PasswordEncrypted { get; set; } = Array.Empty<byte>();
    public byte[] PasswordNonce { get; set; } = Array.Empty<byte>();
    public byte[] PasswordTag { get; set; } = Array.Empty<byte>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
