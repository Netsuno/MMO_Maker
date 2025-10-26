// #TODO (FR) : Décider de l’endianness et de la stratégie de versionnage des fichiers binaires.
#nullable enable
namespace Frog.Core.IO;

/// <summary>Contrat de sérialisation binaire/byte-span pour les types de base.</summary>
public interface ISerializer<T>
{
    byte[] Serialize(T value);
    T Deserialize(ReadOnlySpan<byte> data);
}
