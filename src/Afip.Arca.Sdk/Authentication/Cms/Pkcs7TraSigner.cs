using System;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Afip.Arca.Sdk.Common.Exceptions;

namespace Afip.Arca.Sdk.Authentication.Cms;

/// <summary>
/// Default <see cref="ITraSigner"/> implementation that produces a CMS/PKCS#7 signed
/// envelope using SHA-256 as the digest algorithm.
/// </summary>
public sealed class Pkcs7TraSigner : ITraSigner
{
    private readonly X509Certificate2 _certificate;

    /// <summary>Initializes a new instance of the <see cref="Pkcs7TraSigner"/> class.</summary>
    /// <param name="certificate">Certificate with a usable private key.</param>
    public Pkcs7TraSigner(X509Certificate2 certificate)
    {
        _certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
        if (!_certificate.HasPrivateKey)
        {
            throw new AfipAuthenticationException("Certificate must contain a private key to sign the TRA.");
        }
    }

    /// <inheritdoc />
    public string Sign(string traXml)
    {
        if (string.IsNullOrEmpty(traXml)) throw new ArgumentException("TRA XML required.", nameof(traXml));

        try
        {
            var content = new ContentInfo(Encoding.UTF8.GetBytes(traXml));
            var signedCms = new SignedCms(content, detached: false);
            var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, _certificate)
            {
                IncludeOption = X509IncludeOption.WholeChain,
                DigestAlgorithm = new System.Security.Cryptography.Oid("2.16.840.1.101.3.4.2.1"), // SHA-256
            };

            signedCms.ComputeSignature(signer, silent: true);
            return Convert.ToBase64String(signedCms.Encode());
        }
        catch (Exception ex) when (ex is not AfipAuthenticationException)
        {
            throw new AfipAuthenticationException("Failed to sign the TRA with the configured certificate.", ex);
        }
    }
}
