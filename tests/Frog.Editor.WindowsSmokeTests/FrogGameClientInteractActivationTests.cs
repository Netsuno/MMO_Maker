using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Frog.Client.Network;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>
/// Deterministic coverage of FrogGameClient pending Interact activationId locking.
/// </summary>
public sealed class FrogGameClientInteractActivationTests
{
    [Fact]
    public async Task PendingIdentity_CreateRetainClear_IsAtomicAndMatchedOnly()
    {
        using var client = new FrogGameClient(new ImmediateSynchronizationContext());

        Assert.False(client.PeekInteractActivationId(out _));

        var firstTask = client.SendInteractRequestAsync();
        Assert.True(client.PeekInteractActivationId(out var firstId));
        Assert.NotEqual(Guid.Empty, firstId);
        await ExpectSendFailureOrCompleteAsync(firstTask);

        var retryTask = client.SendInteractRequestAsync();
        Assert.True(client.PeekInteractActivationId(out var reused));
        Assert.Equal(firstId, reused);
        await ExpectSendFailureOrCompleteAsync(retryTask);

        var sameTask = client.SendInteractRequestAsync(firstId);
        Assert.True(client.PeekInteractActivationId(out var still));
        Assert.Equal(firstId, still);
        await ExpectSendFailureOrCompleteAsync(sameTask);

        var other = Guid.NewGuid();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SendInteractRequestAsync(other));
        Assert.Contains("déjà en cours", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(client.PeekInteractActivationId(out var afterReject));
        Assert.Equal(firstId, afterReject);

        client.ApplyInteractResultIdentity(Guid.NewGuid());
        Assert.True(client.PeekInteractActivationId(out var afterMismatch));
        Assert.Equal(firstId, afterMismatch);

        client.ApplyInteractResultIdentity(Guid.Empty);
        Assert.True(client.PeekInteractActivationId(out var afterEmpty));
        Assert.Equal(firstId, afterEmpty);

        client.ApplyInteractResultIdentity(firstId);
        Assert.False(client.PeekInteractActivationId(out _));

        var nextTask = client.SendInteractRequestAsync();
        Assert.True(client.PeekInteractActivationId(out var secondId));
        Assert.NotEqual(Guid.Empty, secondId);
        Assert.NotEqual(firstId, secondId);
        await ExpectSendFailureOrCompleteAsync(nextTask);

        await client.DisconnectAsync();
        Assert.True(client.PeekInteractActivationId(out var afterDisconnect));
        Assert.Equal(secondId, afterDisconnect);
    }

    [Fact]
    public async Task ConcurrentPeekAndSend_DoNotCorruptPendingIdentity()
    {
        using var client = new FrogGameClient(new ImmediateSynchronizationContext());
        var minted = new ConcurrentDictionary<Guid, byte>();
        var errors = new ConcurrentBag<Exception>();

        var send = Task.Run(async () =>
        {
            try
            {
                for (var i = 0; i < 40; i++)
                {
                    try
                    {
                        await client.SendInteractRequestAsync().ConfigureAwait(false);
                    }
                    catch (InvalidOperationException)
                    {
                        // No socket — pending id is assigned before SendRawAsync throws.
                    }

                    if (client.PeekInteractActivationId(out var pending) && pending != Guid.Empty)
                    {
                        minted.TryAdd(pending, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        });

        var peek = Task.Run(() =>
        {
            try
            {
                for (var i = 0; i < 200; i++)
                {
                    if (client.PeekInteractActivationId(out var id) && id != Guid.Empty)
                    {
                        minted.TryAdd(id, 0);
                    }

                    Thread.SpinWait(20);
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        });

        await Task.WhenAll(send, peek);
        Assert.Empty(errors);
        Assert.True(client.PeekInteractActivationId(out var finalId));
        Assert.NotEqual(Guid.Empty, finalId);
        Assert.Single(minted.Keys);
        Assert.True(minted.ContainsKey(finalId));
    }

    private static async Task ExpectSendFailureOrCompleteAsync(Task sendTask)
    {
        try
        {
            await sendTask.ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // No live socket — identity mutations happen before I/O.
        }
    }

    private sealed class ImmediateSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => d(state);

        public override void Send(SendOrPostCallback d, object? state) => d(state);
    }
}
