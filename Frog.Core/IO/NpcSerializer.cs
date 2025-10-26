// #TODO (FR) : Implémenter le format binaire des NPC.
#nullable enable
namespace Frog.Core.IO;

using Frog.Core.Models;

/// <summary>Sérialiseur binaire pour <see cref="Npc"/>.</summary>
public sealed class NpcSerializer : ISerializer<Npc>
{
    public byte[] Serialize(Npc value)
    {
        // #TODO (FR) : Écrire les caractéristiques essentielles du PNJ.
        throw new System.NotImplementedException();
    }

    public Npc Deserialize(ReadOnlySpan<byte> data)
    {
        // #TODO (FR) : Lire les champs + prévoir une section variable (IA/loot/dialogues).
        throw new System.NotImplementedException();
    }
}
