using System;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Frog.Client.Network;
using Frog.Core.Constants;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>Real <see cref="FrogGameClient"/> handshake rejects incompatible Hello version.</summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class ClientProtocolVersionSmokeTests
{
    [Fact]
    public void FrogGameClient_RejectsIncompatibleHello_DoesNotAuthenticate()
    {
        StaTestRunner.Run(() =>
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            string? error = null;
            var loginSeen = false;
            var helloSeen = false;
            var closed = false;

            var accept = Task.Run(async () =>
            {
                using var serverTcp = await listener.AcceptTcpClientAsync();
                await using var stream = serverTcp.GetStream();
                var hello = WireHello.BuildPayload();
                BinaryPrimitives.WriteUInt16LittleEndian(
                    hello.AsSpan(hello.Length - 2),
                    (ushort)(FrogWireProtocol.Version + 9));
                var frame = new byte[4 + hello.Length];
                BinaryPrimitives.WriteInt32LittleEndian(frame, hello.Length);
                hello.CopyTo(frame.AsSpan(4));
                await stream.WriteAsync(frame);
                // Client should disconnect; wait briefly for close.
                await Task.Delay(500);
            });

            try
            {
                using var client = new FrogGameClient(SynchronizationContext.Current!);
                client.HelloReceived += _ => helloSeen = true;
                client.LoginResultReceived += (_, _) => loginSeen = true;
                client.ErrorReceived += msg => error = msg;
                client.ConnectionClosed += () => closed = true;

                var connect = client.ConnectAsync("127.0.0.1", port);
                StaTestRunner.PumpUntil(() => connect.IsCompleted, TimeSpan.FromSeconds(10));
                Assert.True(connect.IsCompletedSuccessfully);

                StaTestRunner.PumpUntil(
                    () => !string.IsNullOrEmpty(error) || closed || !client.IsConnected,
                    TimeSpan.FromSeconds(10));

                Assert.False(helloSeen, "compatible Hello handler must not fire for mismatched version");
                Assert.False(loginSeen, "must not enter authenticated/login-ok state");
                Assert.False(string.IsNullOrEmpty(error));
                Assert.Contains("protocole", error!, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("REDACTED", error!, StringComparison.Ordinal);
                Assert.False(client.IsConnected);
            }
            finally
            {
                listener.Stop();
                try
                {
                    accept.Wait(TimeSpan.FromSeconds(2));
                }
                catch
                {
                    // ignore
                }
            }
        });
    }
}
