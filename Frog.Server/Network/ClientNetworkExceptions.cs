using System.IO;
using System.Net.Sockets;

namespace Frog.Server.Network;

internal static class ClientNetworkExceptions
{
    internal static bool IsExpectedTermination(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is OperationCanceledException)
            {
                return true;
            }

            if (current is ObjectDisposedException)
            {
                return true;
            }

            if (current is IOException)
            {
                return true;
            }

            if (current is SocketException)
            {
                return true;
            }
        }

        return false;
    }
}
