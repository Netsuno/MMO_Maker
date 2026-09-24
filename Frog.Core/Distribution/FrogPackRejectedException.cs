using System.IO;

namespace Frog.Core.Distribution;

/// <summary>Paquet .frogpack refusé (intégrité, signature ou forme). Aucune tuile n’est retournée.</summary>
public sealed class FrogPackRejectedException : IOException
{
    public FrogPackRejectedException(string message)
        : base(message)
    {
    }
}
