using System.IO;
using System.Net.Sockets;
using System.Security.Authentication;

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

            if (current is AuthenticationException)
            {
                return true;
            }
        }

        return false;
    }
}
