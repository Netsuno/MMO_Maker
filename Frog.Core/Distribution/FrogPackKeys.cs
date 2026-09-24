using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace Frog.Core.Distribution;

/// <summary>Clés Ed25519 (RFC 8032) : graine privée 32 octets, clé publique 32 octets, signature 64 octets.</summary>
public static class FrogPackKeys
{
    public const int PublicKeyLength = FrogPackFormat.PublicKeyLength;
    public const int PrivateSeedLength = 32;
    public const int SignatureLength = FrogPackFormat.SignatureLength;

    public readonly record struct Ed25519KeyPair(byte[] PublicKey, byte[] PrivateSeed);

    public static Ed25519KeyPair Generate()
    {
        var generator = new Ed25519KeyPairGenerator();
        generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        var pair = generator.GenerateKeyPair();
        var privateKey = (Ed25519PrivateKeyParameters)pair.Private;
        var publicKey = (Ed25519PublicKeyParameters)pair.Public;
        return new Ed25519KeyPair(publicKey.GetEncoded(), privateKey.GetEncoded());
    }

    public static byte[] PublicKeyFromSeed(ReadOnlySpan<byte> privateSeed)
    {
        var privateKey = CreatePrivate(privateSeed);
        return privateKey.GeneratePublicKey().GetEncoded();
    }

    internal static byte[] Sign(ReadOnlySpan<byte> privateSeed, ReadOnlySpan<byte> message)
    {
        var signer = new Ed25519Signer();
        signer.Init(true, CreatePrivate(privateSeed));
        signer.BlockUpdate(message.ToArray(), 0, message.Length);
        return signer.GenerateSignature();
    }

    internal static bool Verify(ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature)
    {
        if (publicKey.Length != PublicKeyLength || signature.Length != SignatureLength)
        {
            return false;
        }

        var verifier = new Ed25519Signer();
        verifier.Init(false, new Ed25519PublicKeyParameters(publicKey.ToArray(), 0));
        verifier.BlockUpdate(message.ToArray(), 0, message.Length);
        return verifier.VerifySignature(signature.ToArray());
    }

    private static Ed25519PrivateKeyParameters CreatePrivate(ReadOnlySpan<byte> privateSeed)
    {
        if (privateSeed.Length != PrivateSeedLength)
        {
            throw new ArgumentException("Graine Ed25519 : 32 octets.", nameof(privateSeed));
        }

        return new Ed25519PrivateKeyParameters(privateSeed.ToArray(), 0);
    }
}
