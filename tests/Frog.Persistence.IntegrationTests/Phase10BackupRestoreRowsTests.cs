using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using Frog.Application.Gameplay;
using Frog.Application.Identity;
using Frog.Application.Social;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Protocol;
using Frog.Persistence.IntegrationTests.Support;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Auth;
using Frog.Persistence.PostgreSql.Repositories.Ops;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Frog.Server.Gameplay;
using Frog.Server.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Frog.Persistence.IntegrationTests;

/// <summary>
/// P10-7 : pg_dump d’une base avec sanctions + guildes + amis + trades,
/// restore, puis <c>Frog.Server</c> publié lit les lignes (login OK / ban rejeté).
/// </summary>
[Collection("PostgresIsolated")]
public sealed class Phase10BackupRestoreRowsTests
{
    private const string Password = "password12345";

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task DumpSeededSocialTradeSanctions_Restore_PackagedServerReadsRows()
    {
        PostgresBackupScripts.AssertClientToolsAvailable();

        var admin = Environment.GetEnvironmentVariable(IsolatedPostgresFixture.ConnectionEnvironmentVariable);
        Assert.False(string.IsNullOrWhiteSpace(admin));

        await using var source = await DisposablePostgresDatabase.CreateAsync(admin!, "frog_p107s_");
        await using var dest = await DisposablePostgresDatabase.CreateAsync(admin!, "frog_p107d_");

        string aliceUser;
        string bobUser;
        string bannedUser;
        Guid aliceCharId;
        Guid bobCharId;
        Guid bannedCharId;
        Guid guildId;
        Guid tradeId;
        Guid commitRequestId;

        await using (var sourceDb = new FrogDbContext(FrogDbContextOptions.Create(source.ConnectionString)))
        {
            await sourceDb.Database.MigrateAsync();
            Assert.Empty((await sourceDb.Database.GetPendingMigrationsAsync()).ToArray());
        }

        using (var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(source.ConnectionString))))
        {
            var seed = await Phase7PostgresContentSeed.PublishAsync(gate);
            var accounts = new PostgresAccountRepository(gate);
            var characters = new PostgresCharacterRepository(gate);
            var inventory = new PostgresInventoryRepository(gate);
            var bank = new PostgresBankRepository(gate);
            var social = new PostgresSocialStore(gate);
            var items = new PostgresItemRepository(gate);
            var trades = new PostgresTradeCommitRepository(gate, items);
            var operators = new PostgresOperatorDirectory(gate);
            var sanctions = new PostgresAccountSanctionStore(gate);

            aliceUser = UniqueUser("al");
            bobUser = UniqueUser("bo");
            bannedUser = UniqueUser("bn");
            var gmUser = UniqueUser("gm");

            var aliceAcc = await accounts.TryCreateAsync(aliceUser, Password);
            var bobAcc = await accounts.TryCreateAsync(bobUser, Password);
            var bannedAcc = await accounts.TryCreateAsync(bannedUser, Password);
            var gmAcc = await accounts.TryCreateAsync(gmUser, Password);
            Assert.Equal(AccountCreateStatus.Created, aliceAcc.Status);
            Assert.Equal(AccountCreateStatus.Created, bobAcc.Status);
            Assert.Equal(AccountCreateStatus.Created, bannedAcc.Status);
            Assert.Equal(AccountCreateStatus.Created, gmAcc.Status);

            aliceCharId = await CreateHeroAsync(characters, seed, aliceAcc.AccountId!.Value, "AliceHero");
            bobCharId = await CreateHeroAsync(characters, seed, bobAcc.AccountId!.Value, "BobHero");
            bannedCharId = await CreateHeroAsync(characters, seed, bannedAcc.AccountId!.Value, "BanHero");

            var aliceRec = await characters.FindByIdAsync(aliceCharId);
            var bobRec = await characters.FindByIdAsync(bobCharId);
            Assert.NotNull(aliceRec);
            Assert.NotNull(bobRec);
            await characters.SaveAsync(aliceRec! with { Gold = 250, BankGold = 40 });
            await characters.SaveAsync(bobRec! with { Gold = 80 });

            Assert.Equal(
                InventoryMutationStatus.Ok,
                (await inventory.TryAddAsync(aliceCharId, Phase7ContentSeed.DefaultItemId, 4, 20)).Status);
            Assert.Equal(
                InventoryMutationStatus.Ok,
                (await inventory.TryAddAsync(bobCharId, Phase7ContentSeed.DefaultWeaponId, 1, 1)).Status);
            Assert.Equal(
                BankMutationStatus.Ok,
                (await bank.DepositItemAsync(aliceCharId, Phase7ContentSeed.DefaultArmorId, 1, 1)).Status);

            var guild = await social.CreateGuildAsync(
                aliceCharId,
                "RestoreKnights",
                SocialWire.GuildNameKey("RestoreKnights"));
            Assert.True(guild.Success, guild.Message);
            guildId = guild.SubjectId;
            var invite = await social.InviteToGuildAsync(
                guildId,
                aliceCharId,
                bobCharId,
                DateTimeOffset.UtcNow.AddHours(1));
            Assert.True(invite.Success, invite.Message);
            var join = await social.RespondGuildInviteAsync(invite.SubjectId, bobCharId, accept: true, maxMembers: 50);
            Assert.True(join.Success, join.Message);

            var friendReq = await social.RequestFriendAsync(
                aliceCharId,
                bobCharId,
                DateTimeOffset.UtcNow.AddDays(7),
                maxFriends: 100);
            Assert.True(friendReq.Success, friendReq.Message);
            var friendOk = await social.RespondFriendAsync(bobCharId, aliceCharId, accept: true, maxFriends: 100);
            Assert.True(friendOk.Success, friendOk.Message);

            var blocked = await social.BlockAsync(aliceCharId, bannedCharId, maxBlocks: 100);
            Assert.True(blocked.Success, blocked.Message);

            tradeId = Guid.NewGuid();
            commitRequestId = Guid.NewGuid();
            var commit = await trades.TryCommitAsync(
                tradeId,
                commitRequestId,
                aliceCharId,
                bobCharId,
                initiatorGold: 10,
                partnerGold: 0,
                initiatorItems: [new TradeStackOffer(Phase7ContentSeed.DefaultItemId, 2)],
                partnerItems: [new TradeStackOffer(Phase7ContentSeed.DefaultWeaponId, 1)]);
            Assert.True(commit.Success, commit.Message);
            Assert.False(commit.IdempotentReplay);

            Assert.Equal(
                OperatorGrantStatus.Granted,
                (await operators.GrantAsync(gmAcc.AccountId!.Value, "sql", "p10-7")).Status);
            await sanctions.ApplyAsync(bannedAcc.AccountId!.Value, SanctionKinds.Mute, gmAcc.AccountId.Value, "spam");
            await sanctions.ApplyAsync(bannedAcc.AccountId.Value, SanctionKinds.Ban, gmAcc.AccountId.Value, "cheat");
            Assert.True(await sanctions.HasActiveBanAsync(bannedAcc.AccountId.Value));
            Assert.True(await sanctions.HasActiveMuteAsync(bannedAcc.AccountId.Value));
        }

        NpgsqlConnection.ClearAllPools();

        var dumpPath = Path.Combine(Path.GetTempPath(), "frog_p107_" + Guid.NewGuid().ToString("N") + ".dump");
        var publishDir = string.Empty;
        try
        {
            await PostgresBackupScripts.BackupAsync(source.ConnectionString, dumpPath);
            Assert.True(new FileInfo(dumpPath).Length > 0);
            await PostgresBackupScripts.RestoreAsync(dest.ConnectionString, dumpPath);
            await PostgresBackupScripts.VerifyAsync(dest.ConnectionString);

            await using (var destDb = new FrogDbContext(FrogDbContextOptions.Create(dest.ConnectionString)))
            {
                Assert.True(await destDb.PlayerGuilds.AsNoTracking().AnyAsync(g => g.Id == guildId));
                Assert.Equal(2, await destDb.PlayerGuildMembers.AsNoTracking().CountAsync(m => m.GuildId == guildId));
                Assert.Equal(
                    1,
                    await destDb.PlayerFriendships.AsNoTracking().CountAsync(f => f.Status == FriendshipStatuses.Accepted));
                Assert.Equal(
                    1,
                    await destDb.PlayerCharacterBlocks.AsNoTracking().CountAsync(
                        b => b.BlockerCharacterId == aliceCharId && b.BlockedCharacterId == bannedCharId));
                Assert.Equal(1, await destDb.PlayerTradeExecutions.AsNoTracking().CountAsync(t => t.TradeId == tradeId));
                Assert.Equal(
                    1,
                    await destDb.OpsAccountSanctions.AsNoTracking().CountAsync(
                        s => s.Kind == SanctionKinds.Ban && s.RevokedAtUtc == null));
                Assert.Equal(
                    1,
                    await destDb.OpsAccountSanctions.AsNoTracking().CountAsync(
                        s => s.Kind == SanctionKinds.Mute && s.RevokedAtUtc == null));

                var alice = await destDb.PlayerCharacters.AsNoTracking().FirstAsync(c => c.Id == aliceCharId);
                var bob = await destDb.PlayerCharacters.AsNoTracking().FirstAsync(c => c.Id == bobCharId);
                Assert.Equal(240, alice.Gold);
                Assert.Equal(40, alice.BankGold);
                Assert.Equal(90, bob.Gold);

                var alicePotion = await destDb.PlayerInventorySlots.AsNoTracking()
                    .Where(s => s.CharacterId == aliceCharId && s.ItemId == Phase7ContentSeed.DefaultItemId)
                    .SumAsync(s => s.Quantity);
                Assert.Equal(2, alicePotion);
                Assert.True(await destDb.PlayerInventorySlots.AsNoTracking().AnyAsync(
                    s => s.CharacterId == aliceCharId && s.ItemId == Phase7ContentSeed.DefaultWeaponId));
                Assert.True(await destDb.PlayerBankSlots.AsNoTracking().AnyAsync(
                    s => s.CharacterId == aliceCharId && s.ItemId == Phase7ContentSeed.DefaultArmorId));
            }

            using (var destGate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(dest.ConnectionString))))
            {
                var replayRepo = new PostgresTradeCommitRepository(destGate, new PostgresItemRepository(destGate));
                var replay = await replayRepo.TryReplayAsync(bobCharId, commitRequestId);
                Assert.NotNull(replay);
                Assert.True(replay!.Success);
                Assert.True(replay.IdempotentReplay);
                var ledger = await replayRepo.FindExecutionAsync(tradeId);
                Assert.NotNull(ledger);
                Assert.DoesNotContain("password", ledger!.ContentsJson, StringComparison.OrdinalIgnoreCase);
            }

            publishDir = await PublishReleaseServerAsync();
            var port = GetFreePort();
            WritePackagedServerConfig(publishDir, dest.ConnectionString, port);
            var shutdownFilePath = Path.Combine(publishDir, ".frog-shutdown-request");
            using var process = StartPackagedServer(publishDir, shutdownFilePath);
            var logLines = new List<string>();
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    lock (logLines)
                    {
                        logLines.Add(e.Data);
                    }
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    lock (logLines)
                    {
                        logLines.Add(e.Data);
                    }
                }
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await WaitForServerReadyAsync(process, port, logLines, TimeSpan.FromSeconds(60));

                await using var aliceClient = new Phase7TcpTestClient();
                await aliceClient.ConnectAsync("127.0.0.1", port);
                Assert.Equal((byte)PacketId.Hello, (await aliceClient.ReadFrameAsync())[0]);
                await aliceClient.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(aliceUser, Password));
                var aliceLogin = await aliceClient.ReadUntilAsync(PacketId.LoginResult);
                Assert.True(Phase8WireDecoders.TryDecodeStatusResult(aliceLogin, out var aliceOk, out _));
                Assert.True(aliceOk);
                await aliceClient.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(aliceCharId.ToString("D")));
                Assert.NotEqual(0, (await aliceClient.ReadUntilAsync(PacketId.CharacterSelectResult))[1]);

                await using var bannedClient = new Phase7TcpTestClient();
                await bannedClient.ConnectAsync("127.0.0.1", port);
                Assert.Equal((byte)PacketId.Hello, (await bannedClient.ReadFrameAsync())[0]);
                await bannedClient.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(bannedUser, Password));
                var bannedLogin = await bannedClient.ReadUntilAsync(PacketId.LoginResult);
                Assert.True(Phase8WireDecoders.TryDecodeStatusResult(bannedLogin, out var bannedOk, out var bannedMsg));
                Assert.False(bannedOk);
                Assert.Equal(ModerationMessages.Banned, bannedMsg);
            }
            finally
            {
                _ = await StopServerGracefullyAsync(process, shutdownFilePath, logLines);
            }
        }
        finally
        {
            try
            {
                File.Delete(dumpPath);
            }
            catch
            {
                // temp
            }

            TryDeleteDirectory(publishDir);
        }
    }

    private static async Task<Guid> CreateHeroAsync(
        PostgresCharacterRepository characters,
        Phase7PostgresContentSeedResult seed,
        Guid accountId,
        string name)
    {
        var created = await characters.CreateAsync(
            accountId,
            name,
            seed.ClassId,
            new CharacterStats(10, 10, 10, 10, 10, 10),
            maxHp: 100,
            maxMp: 50,
            seed.SpellId,
            seed.RuntimeMapId,
            GameplayLimits.DefaultSpawnTileX * 32,
            GameplayLimits.DefaultSpawnTileY * 32);
        Assert.Equal(CharacterCreateStatus.Created, created.Status);
        Assert.NotNull(created.Character);
        return created.Character!.Id;
    }

    private static string UniqueUser(string prefix) => prefix + Guid.NewGuid().ToString("N")[..10];

    private static void TryDeleteDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // publish trees are under /tmp
        }
    }

    private static async Task<string> PublishReleaseServerAsync()
    {
        var repoRoot = FindRepoRoot();
        var script = Path.Combine(repoRoot, "scripts", "publish-frog.sh");
        ProcessStartInfo startInfo;
        string outputDir;
        if (!OperatingSystem.IsWindows() && File.Exists(script))
        {
            var outputRoot = Path.Combine(Path.GetTempPath(), "frog-p107-pkg-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputRoot);
            startInfo = new ProcessStartInfo
            {
                FileName = script,
                Arguments = $"--target server-linux-x64 --output-root \"{outputRoot}\" --force",
                WorkingDirectory = repoRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            outputDir = Path.Combine(outputRoot, "server-linux-x64");
        }
        else
        {
            var publishDir = Path.Combine(Path.GetTempPath(), "frog-p107-pkg-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(publishDir);
            startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments =
                    $"publish \"{Path.Combine(repoRoot, "Frog.Server", "Frog.Server.csproj")}\" -c Release -r win-x64 --self-contained true -o \"{publishDir}\" -p:PublishSingleFile=false",
                WorkingDirectory = repoRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            outputDir = publishDir;
        }

        using var publish = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start publish.");
        await publish.WaitForExitAsync();
        Assert.True(publish.ExitCode == 0, "publish failed with exit code " + publish.ExitCode);
        return outputDir;
    }

    private static void WritePackagedServerConfig(string publishDir, string connectionString, int port)
    {
        var configPath = Path.Combine(publishDir, "appsettings.Local.json");
        var json = JsonSerializer.Serialize(new
        {
            Server = new { Port = port, BindAddress = "127.0.0.1" },
            MariaDb = new { Enabled = false },
            PostgreSql = new
            {
                Enabled = true,
                AllowInMemoryFallback = false,
                ConnectionString = connectionString,
            },
        });
        File.WriteAllText(configPath, json);
    }

    private static Process StartPackagedServer(string publishDir, string shutdownFilePath)
    {
        var apphost = Path.Combine(publishDir, OperatingSystem.IsWindows() ? "Frog.Server.exe" : "Frog.Server");
        var startInfo = new ProcessStartInfo
        {
            FileName = apphost,
            Arguments = $"--contentRoot \"{publishDir}\"",
            WorkingDirectory = publishDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment[ShutdownFileWatcherService.ShutdownFileEnvironmentVariable] = shutdownFilePath;
        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start packaged Frog.Server.");
    }

    private static async Task WaitForServerReadyAsync(
        Process process,
        int port,
        List<string> logLines,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"Packaged server exited early ({process.ExitCode}):\n{string.Join('\n', logLines)}");
            }

            if (await IsPortOpenAsync(port))
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException(
            $"Packaged server did not bind port {port} in time.\n{string.Join('\n', logLines)}");
    }

    private static async Task<int> StopServerGracefullyAsync(
        Process process,
        string shutdownFilePath,
        IReadOnlyList<string> logLines)
    {
        if (process.HasExited)
        {
            return process.ExitCode;
        }

        if (!OperatingSystem.IsWindows())
        {
            NativeSignal.TrySendSigterm(process.Id);
        }

        File.WriteAllText(shutdownFilePath, string.Empty);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await process.WaitForExitAsync(cts.Token);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch
            {
                // failure cleanup
            }

            throw new TimeoutException(
                "Packaged server did not shut down gracefully.\n" + string.Join('\n', logLines));
        }
    }

    private static class NativeSignal
    {
        private const int Sigterm = 15;

        public static bool TrySendSigterm(int pid)
        {
            try
            {
                return kill(pid, Sigterm) == 0;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int kill(int pid, int sig);
    }

    private static async Task<bool> IsPortOpenAsync(int port)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(300);
            await client.ConnectAsync(IPAddress.Loopback, port, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Frog.Creator.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate Frog.Creator.sln.");
    }
}
