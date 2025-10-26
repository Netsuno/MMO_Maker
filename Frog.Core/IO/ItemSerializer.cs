// #TODO (FR) : Implémenter le format binaire des Items, compatible fichiers existants.
#nullable enable
namespace Frog.Core.IO;

using Frog.Core.Models;

/// <summary>Sérialiseur binaire pour <see cref="Item"/>.</summary>
public sealed class ItemSerializer : ISerializer<Item>
{
    public byte[] Serialize(Item value)
    {
        // #TODO (FR) : Écrire les champs clés + version.
        throw new System.NotImplementedException();
    }

    public Item Deserialize(ReadOnlySpan<byte> data)
    {
        // #TODO (FR) : Lire les champs dans le même ordre/endianness.
        throw new System.NotImplementedException();
    }
}
