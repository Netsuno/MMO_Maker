using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Frog.Tests.Support;

/// <summary>
/// Certificats RSA 2048 / SHA-256 de test (jamais versionnés). CA + feuille SAN DNS.
/// Les clés sont écrites en PKCS#8 / PKCS#12 sur disque temp puis réimportées avec
/// <see cref="X509KeyStorageFlags.UserKeySet"/> (Schannel Windows refuse les clés
/// éphémères). Pas d'AcceptAll.
/// </summary>
internal sealed class EphemeralTlsCertificates : IDisposable
{
    public string DirectoryPath { get; }
    public X509Certificate2 Ca { get; }
    public X509Certificate2 Leaf { get; }
    public string LeafCertPemPath { get; }
    public string LeafKeyPemPath { get; }
    public string CaPemPath { get; }
    public string LeafPfxPath { get; }
    public X509Certificate2Collection TrustRoots { get; }

    private EphemeralTlsCertificates(
        string directoryPath,
        X509Certificate2 ca,
        X509Certificate2 leaf)
    {
        DirectoryPath = directoryPath;
        LeafCertPemPath = Path.Combine(directoryPath, "leaf.crt.pem");
        LeafKeyPemPath = Path.Combine(directoryPath, "leaf.key.pem");
        CaPemPath = Path.Combine(directoryPath, "ca.crt.pem");
        LeafPfxPath = Path.Combine(directoryPath, "leaf.pfx");

        File.WriteAllText(LeafCertPemPath, leaf.ExportCertificatePem());
        using (var rsa = leaf.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("Leaf certificate has no RSA private key."))
        {
            File.WriteAllText(LeafKeyPemPath, rsa.ExportPkcs8PrivateKeyPem());
        }

        File.WriteAllText(CaPemPath, ca.ExportCertificatePem());
        File.WriteAllBytes(LeafPfxPath, leaf.Export(X509ContentType.Pfx));

        Ca = new X509Certificate2(ca.RawData);
        Leaf = LoadPersistedPfx(LeafPfxPath);
        TrustRoots = new X509Certificate2Collection { new X509Certificate2(ca.RawData) };
    }

    public static EphemeralTlsCertificates Create(string dnsName = "localhost", bool expiredLeaf = false)
    {
        var dir = Path.Combine(Path.GetTempPath(), "frog-p10-5-tls-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        using var caRsa = CreatePersistedRsa(Path.Combine(dir, "ca.key.pem"));
        var caReq = new CertificateRequest(
            "CN=Frog P10-5 Test CA",
            caRsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        caReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        caReq.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        caReq.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(caReq.PublicKey, false));
        using var ca = caReq.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-90),
            DateTimeOffset.UtcNow.AddDays(14));

        using var leafRsa = CreatePersistedRsa(Path.Combine(dir, "leaf.key.work.pem"));
        var leafReq = new CertificateRequest(
            "CN=" + dnsName,
            leafRsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        leafReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        leafReq.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        var serverAuth = new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") };
        leafReq.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(serverAuth, true));
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(dnsName);
        leafReq.CertificateExtensions.Add(san.Build());
        leafReq.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(leafReq.PublicKey, false));
        leafReq.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(ca, includeKeyIdentifier: true, includeIssuerAndSerial: false));

        DateTimeOffset notBefore;
        DateTimeOffset notAfter;
        if (expiredLeaf)
        {
            notBefore = DateTimeOffset.UtcNow.AddDays(-30);
            notAfter = DateTimeOffset.UtcNow.AddDays(-1);
        }
        else
        {
            notBefore = DateTimeOffset.UtcNow.AddHours(-1);
            notAfter = DateTimeOffset.UtcNow.AddDays(7);
        }

        var serial = RandomNumberGenerator.GetBytes(8);
        serial[0] &= 0x7F;
        using var leafPublic = leafReq.Create(ca, notBefore, notAfter, serial);
        using var leaf = leafPublic.CopyWithPrivateKey(leafRsa);

        return new EphemeralTlsCertificates(dir, ca, leaf);
    }

    /// <summary>
    /// PKCS#12 on disk + UserKeySet (Schannel-compatible persisted RSA; not an in-memory CSP key).
    /// </summary>
    internal static X509Certificate2 LoadPersistedPfx(string pfxPath)
    {
        return new X509Certificate2(
            pfxPath,
            (string?)null,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.UserKeySet);
    }

    private static RSA CreatePersistedRsa(string pkcs8Path)
    {
        using (var ephemeral = RSA.Create(2048))
        {
            File.WriteAllText(pkcs8Path, ephemeral.ExportPkcs8PrivateKeyPem());
        }

        var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(pkcs8Path));
        return rsa;
    }

    public void Dispose()
    {
        foreach (var cert in TrustRoots)
        {
            cert.Dispose();
        }

        Leaf.Dispose();
        Ca.Dispose();
        try
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
