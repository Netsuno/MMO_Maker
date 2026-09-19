using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Frog.LoadHarness;

/// <summary>CA + feuille localhost de test (jamais Git). Pas d'AcceptAll.</summary>
public static class LoadTestCertificates
{
    public static void Write(string directory, string dnsName = "localhost")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);

        using var caRsa = RSA.Create(2048);
        var caReq = new CertificateRequest(
            "CN=Frog P10-8 Test CA",
            caRsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        caReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        caReq.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        using var ca = caReq.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(7));

        using var leafRsa = RSA.Create(2048);
        var leafReq = new CertificateRequest(
            "CN=" + dnsName,
            leafRsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        leafReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        leafReq.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        leafReq.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, true));
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(dnsName);
        leafReq.CertificateExtensions.Add(san.Build());
        using var leaf = leafReq.Create(ca, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(6), [1, 0, 0]);
        using var leafWithKey = leaf.CopyWithPrivateKey(leafRsa);

        File.WriteAllText(Path.Combine(directory, "ca.pem"), ca.ExportCertificatePem());
        File.WriteAllText(Path.Combine(directory, "leaf.pem"), leafWithKey.ExportCertificatePem());
        File.WriteAllText(Path.Combine(directory, "leaf.key"), leafRsa.ExportPkcs8PrivateKeyPem());
    }
}
