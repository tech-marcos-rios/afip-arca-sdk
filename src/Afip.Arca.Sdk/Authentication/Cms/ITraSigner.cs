namespace Afip.Arca.Sdk.Authentication.Cms;

/// <summary>
/// Signs a TRA XML document with the contributor's X.509 certificate, producing the
/// CMS/PKCS#7 base64-encoded blob that WSAA's <c>loginCms</c> method expects.
/// </summary>
public interface ITraSigner
{
    /// <summary>Signs the given TRA XML.</summary>
    /// <param name="traXml">Raw TRA XML.</param>
    /// <returns>Base64-encoded CMS structure.</returns>
    string Sign(string traXml);
}
