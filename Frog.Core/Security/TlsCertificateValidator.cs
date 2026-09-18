using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Frog.Core.Security;

/// <summary>
/// Validation stricte du certificat serveur : chaîne, nom/SNI, expiration, ancre CA.
/// Aucun AcceptAll, aucun callback qui renvoie true sans contrôle.
/// </summary>
public static class TlsCertificateValidator
{
    public static bool IsAcceptable(
        X509Certificate? certificate,
        X509Chain? _,
        SslPolicyErrors sslPolicyErrors,
        string targetHost,
        X509Certificate2Collection? customTrustRoots)
    {
        if (certificate is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(targetHost))
        {
            return false;
        }

        if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNotAvailable) != 0)
        {
            return false;
        }

        X509Certificate2 cert2;
        var ownsCert = false;
        if (certificate is X509Certificate2 typed)
        {
            cert2 = typed;
        }
        else
        {
            cert2 = new X509Certificate2(certificate);
            ownsCert = true;
        }

        try
        {
            if (!cert2.MatchesHostname(targetHost))
            {
                return false;
            }

            var now = DateTime.UtcNow;
            if (now < cert2.NotBefore.ToUniversalTime() || now > cert2.NotAfter.ToUniversalTime())
            {
                return false;
            }

            using var verifyChain = new X509Chain();
            verifyChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            verifyChain.ChainPolicy.DisableCertificateDownloads = true;
            verifyChain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

            if (customTrustRoots is { Count: > 0 })
            {
                verifyChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                foreach (var root in customTrustRoots)
                {
                    verifyChain.ChainPolicy.CustomTrustStore.Add(root);
                }
            }
            else
            {
                verifyChain.ChainPolicy.TrustMode = X509ChainTrustMode.System;
            }

            return verifyChain.Build(cert2);
        }
        finally
        {
            if (ownsCert)
            {
                cert2.Dispose();
            }
        }
    }
}
