using System.IO;
using System.Net.Sockets;

namespace Frog.Editor.Services;

/// <summary>Détecte un PostgreSQL injoignable pour ouvrir l’éditeur hors ligne.</summary>
internal static class EditorDatabaseAvailability
{
    public const string OfflineNotice = "PostgreSQL injoignable — édition locale (non enregistrée)";

    public static bool IsUnavailable(Exception exception)
    {
        if (exception is AggregateException aggregate)
        {
            return aggregate.InnerExceptions.Any(IsUnavailable);
        }

        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is OperationCanceledException)
            {
                return false;
            }

            if (current is SocketException or TimeoutException or IOException)
            {
                return true;
            }

            var name = current.GetType().Name;
            if (name is "NpgsqlException" or "PostgresException")
            {
                return true;
            }
        }

        return false;
    }
}
