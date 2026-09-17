using System.Threading;
using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Frog.Persistence.IntegrationTests.Support;
using Frog.Server.Gameplay;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class Phase7PostgresE2ETests
{
    private readonly IsolatedPostgresFixture _fixture;

    public Phase7PostgresE2ETests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task FullGameplayFlow_PostgreSqlHeadless_AllSteps()
    {
        var (seed, groundWeaponId) = await SeedPublishedContentAsync(seedGroundItem: true);

        // Valeurs deterministes derivees du contenu publie (pas de nombres magiques) :
        // niveau/recompense XP du monstre seede, cout en mana du sort, prix boutique.
        var monsterLevel = Phase7ContentSeed.CreateDefaultMonster().Level;
        var expectedMonsterXp = CombatFormulas.MonsterExperienceReward(monsterLevel);
        var spellManaCost = Phase7ContentSeed.CreateDefaultSpell().ManaCost;
        var consumableBuyPrice = Phase7ContentSeed.CreateDefaultConsumable().BuyPrice;
        var consumableSellPrice = Phase7ContentSeed.CreateDefaultConsumable().SellPrice;
        var (respawnPixelX, respawnPixelY) = WorldMetrics.TileCenterToPixels(
            GameplayLimits.DefaultSpawnTileX,
            GameplayLimits.DefaultSpawnTileY);

        var port = Phase7TcpTestPorts.GetFreePort();
        using var host = Phase7PostgresE2EHost
            .CreateBuilder(_fixture.ConnectionString, port)
            .Build();
        await host.StartAsync();
        string token = string.Empty;
        string characterId = string.Empty;
        string user = string.Empty;

        // Etat de progression/economie attendu apres la mise a mort du monstre, calcule via la
        // meme courbe que le serveur — reutilise pour verifier l'etat post-restart exact.
        int expectedLevelAfterKill = 0;
        long expectedXpAfterKill = 0;

        // Or joueur/banque attendus une fois toute la sequence boutique/banque terminee — memes
        // valeurs verifiees a nouveau apres redemarrage du serveur.
        int expectedGoldAfterGoldWithdraw = 0;
        const int bankGoldDeposit = 25;
        const int bankGoldWithdraw = 10;
        var expectedBankGoldAfterWithdraw = bankGoldDeposit - bankGoldWithdraw;

        try
        {
            user = $"pg-{Guid.NewGuid():N}"[..18];
            var chatUser = $"cht-{Guid.NewGuid():N}"[..18];
            var killerUser = $"kil-{Guid.NewGuid():N}"[..18];
            const string password = "password12345";

            await using var client = new Phase7TcpTestClient();
            await client.ConnectAsync("127.0.0.1", port);
            Assert.Equal((byte)PacketId.Hello, (await client.ReadFrameAsync())[0]);

            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildRegister(user, password));
            Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.RegisterResult))[1]);

            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(user, password));
            var login = await client.ReadUntilAsync(PacketId.LoginResult);
            token = Phase7WireDecoders.DecodeLoginToken(login);

            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterCreate("PgHero", seed.ClassId));
            var create = await client.ReadUntilAsync(PacketId.CharacterCreateResult);
            characterId = Phase7WireDecoders.DecodeCharacterId(create);

            // Step 2 (liste persos) : le personnage cree doit apparaitre dans CharacterListResult.
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterList());
            var characterListFrame = await client.ReadUntilAsync(PacketId.CharacterListResult);
            Assert.True(Phase7WireDecoders.TryDecodeCharacterList(characterListFrame, out var characters));
            Assert.Contains(characters, c => c.Id == characterId && c.Name == "PgHero");

            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(characterId));
            Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.CharacterSelectResult))[1]);

            var combat = await client.ReadUntilAsync(PacketId.CombatState);
            Assert.True(Phase7WireDecoders.TryDecodeCombatState(combat, out _, out _, out _, out _, out _, out _, out var startGold, out _));
            Assert.Equal(GameplayLimits.StartingGold, startGold);

            // Step 3 (carte) : l'identifiant de carte recu doit correspondre a la carte monde
            // publiee par le seed (liaison runtime <-> Phase7PostgresContentSeed.RuntimeMapId).
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildMapRequest());
            var mapFrame = await client.ReadUntilAnyAsync([PacketId.MapData, PacketId.MapAlreadySynced]);
            Assert.Equal(seed.RuntimeMapId, Phase7WireDecoders.DecodeMapId(mapFrame));

            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildPickup(groundWeaponId!.Value));
            Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.PickupItemResult))[1]);
            var invAfterPickup = await client.ReadUntilAsync(PacketId.InventorySnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(invAfterPickup, out var pickupSnap));
            Assert.Contains(pickupSnap.Slots, s => s.ItemId == seed.WeaponId);

            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildEquip(0));
            Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.EquipResult))[1]);
            var invEquipped = await client.ReadUntilAsync(PacketId.InventorySnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(invEquipped, out var equipSnap));
            Assert.Equal(seed.WeaponId, equipSnap.EquippedWeaponItemId);

            await client.DisconnectAsync();
            await Task.Delay(150);
            await using var client2 = new Phase7TcpTestClient();
            await client2.ConnectAsync("127.0.0.1", port);
            _ = await client2.ReadFrameAsync();
            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildReconnect(token));
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.ReconnectResult))[1]);
            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(characterId));
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.CharacterSelectResult))[1]);
            var invReconnect = await client2.ReadUntilAsync(PacketId.InventorySnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(invReconnect, out var reconnectSnap));
            Assert.Equal(seed.WeaponId, reconnectSnap.EquippedWeaponItemId);

            // Step 7 (melee valide) : le coup doit reussir (octet succes != 0), puis un
            // CombatState frais (propre HP/MP/or non affectes par un simple coup mele).
            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildMelee("Slime"));
            var meleeResult = await client2.ReadUntilAsync(PacketId.MeleeAttackResult);
            Assert.NotEqual(0, meleeResult[1]);
            var combatAfterMelee = await client2.ReadUntilAsync(PacketId.CombatState);
            Assert.True(Phase7WireDecoders.TryDecodeCombatState(
                combatAfterMelee,
                out _,
                out _,
                out var hpAfterMelee,
                out _,
                out var mpAfterMelee,
                out _,
                out var goldAfterMelee,
                out _));

            // Step 8 (sort valide) : succes + mana deduit exactement du cout du sort seede
            // (ou, a defaut, un cooldown est pose — ici on verifie la deduction de mana reelle).
            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildSpellCast(seed.SpellId, "Slime"));
            var spellResult = await client2.ReadUntilAsync(PacketId.SpellCastResult);
            Assert.NotEqual(0, spellResult[1]);
            var afterSpell = await client2.ReadUntilAnyAsync(
                [PacketId.ExperienceGain, PacketId.CombatState],
                TimeSpan.FromSeconds(2));
            if (afterSpell[0] == (byte)PacketId.ExperienceGain)
            {
                afterSpell = await client2.ReadUntilAsync(PacketId.CombatState);
            }

            Assert.True(Phase7WireDecoders.TryDecodeCombatState(
                afterSpell,
                out var levelAfterSpell,
                out var xpAfterSpell,
                out var hpAfterSpell,
                out _,
                out var mpAfterSpell,
                out _,
                out var goldAfterSpell,
                out _));
            Assert.Equal(mpAfterMelee - spellManaCost, mpAfterSpell);
            Assert.Equal(hpAfterMelee, hpAfterSpell);
            Assert.Equal(goldAfterMelee, goldAfterSpell);

            // Step 9 (sort invalide) : capture exacte de l'etat pre-echec, puis rejet
            // (octet succes == 0) sans effet de bord.
            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildSpellCast(Guid.NewGuid(), "Slime"));
            var badSpell = await client2.ReadUntilAsync(PacketId.SpellCastResult);
            Assert.Equal(0, badSpell[1]);
            await client2.DrainPendingAsync(TimeSpan.FromMilliseconds(200));

            // Force une relecture fraiche, servie depuis l'enregistrement persiste (une
            // re-selection du meme personnage recharge le CombatState depuis la BDD), pour
            // prouver que l'echec ci-dessus n'a modifie ni niveau, ni XP, ni HP, ni MP, ni or —
            // sans tautologie du type "beforeLevel >= 1".
            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(characterId));
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.CharacterSelectResult))[1]);
            var combatAfterBadSpell = await client2.ReadUntilAsync(PacketId.CombatState);
            Assert.True(Phase7WireDecoders.TryDecodeCombatState(
                combatAfterBadSpell,
                out var levelAfterBadSpell,
                out var xpAfterBadSpell,
                out var hpAfterBadSpell,
                out _,
                out var mpAfterBadSpell,
                out _,
                out var goldAfterBadSpell,
                out _));
            Assert.Equal(levelAfterSpell, levelAfterBadSpell);
            Assert.Equal(xpAfterSpell, xpAfterBadSpell);
            Assert.Equal(hpAfterSpell, hpAfterBadSpell);
            Assert.Equal(mpAfterSpell, mpAfterBadSpell);
            Assert.Equal(goldAfterSpell, goldAfterBadSpell);

            // Step 10 (XP) : montant exact via CombatFormulas.MonsterExperienceReward (pas
            // seulement > 0), et exactement un ExperienceGain pour cette mise a mort.
            var xpFromKill = await KillMonsterOrReadExperienceAsync(client2, "Slime");
            Assert.Equal(expectedMonsterXp, xpFromKill);
            var combatAfterKill = await client2.ReadUntilAsync(PacketId.CombatState);
            Assert.True(Phase7WireDecoders.TryDecodeCombatState(
                combatAfterKill, out var levelAfterKill, out var experience, out _, out _, out _, out _, out _, out _));
            (expectedLevelAfterKill, expectedXpAfterKill, _) =
                ProgressionCurve.ApplyExperience(levelAfterBadSpell, xpAfterBadSpell, expectedMonsterXp);
            Assert.Equal(expectedLevelAfterKill, levelAfterKill);
            Assert.Equal(expectedXpAfterKill, experience);
            await AssertNoExtraExperienceGainAsync(client2);

            await using var chatPeer = new Phase7TcpTestClient();
            await RegisterLoginSelectAsync(chatPeer, port, chatUser, password, "Chatter", seed.ClassId);
            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildChat(ChatChannel.Map, "hello map"));
            var mapChat = await chatPeer.ReadUntilAsync(PacketId.ChatMessage);
            Assert.True(Phase7WireDecoders.TryDecodeChatMessage(mapChat, out var mapChannel, out _, out _, out var mapMsg));
            Assert.Equal(ChatChannel.Map, mapChannel);
            Assert.Equal("hello map", mapMsg);

            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildChat(ChatChannel.Global, "hello world"));
            var globalChat = await chatPeer.ReadUntilAsync(PacketId.ChatMessage);
            Assert.True(Phase7WireDecoders.TryDecodeChatMessage(globalChat, out var globalChannel, out _, out _, out _));
            Assert.Equal(ChatChannel.Global, globalChannel);

            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildChat(ChatChannel.Whisper, "psst", chatUser));
            var whisper = await chatPeer.ReadUntilAsync(PacketId.ChatMessage);
            Assert.True(Phase7WireDecoders.TryDecodeChatMessage(whisper, out var whisperChannel, out _, out _, out var whisperMsg));
            Assert.Equal(ChatChannel.Whisper, whisperChannel);
            Assert.Equal("psst", whisperMsg);

            // Step 11 (rate limit chat) : au-dela du quota, le serveur doit repondre par un
            // paquet Error explicite ("Trop de messages.") — pas seulement un drain silencieux.
            for (var i = 0; i < GameplayLimits.MaxChatMessagesPerWindow + 2; i++)
            {
                await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildChat(ChatChannel.Global, $"spam{i}"));
            }

            var rateLimitError = await client2.ReadUntilAsync(PacketId.Error, TimeSpan.FromSeconds(5));
            Assert.Contains("Trop de messages", Phase7WireDecoders.DecodeErrorMessage(rateLimitError));
            await client2.DrainPendingAsync(TimeSpan.FromMilliseconds(200));

            // Step 12 (achat/vente) : inventaire + or exacts a chaque etape (prix boutique
            // deterministes issus du contenu seede), pas de simple "< StartingGold".
            var buyRequestId = Guid.NewGuid();
            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildShopBuy(seed.ShopId, seed.ConsumableId, 1, buyRequestId));
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.ShopBuyResult))[1]);
            var invAfterBuy = await client2.ReadUntilAsync(PacketId.InventorySnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(invAfterBuy, out var buySnap));
            Assert.Contains(buySnap.Slots, s => s.ItemId == seed.ConsumableId);
            var combatAfterBuy = await client2.ReadUntilAsync(PacketId.CombatState);
            Assert.True(Phase7WireDecoders.TryDecodeCombatState(combatAfterBuy, out _, out _, out _, out _, out _, out _, out var goldAfterBuy, out _));
            var expectedGoldAfterBuy = GameplayLimits.StartingGold - consumableBuyPrice;
            Assert.Equal(expectedGoldAfterBuy, goldAfterBuy);

            var sellSlot = buySnap.Slots.First(s => s.ItemId == seed.ConsumableId).SlotIndex;
            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildShopSell((byte)sellSlot, 1, Guid.NewGuid()));
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.ShopSellResult))[1]);
            var invAfterSell = await client2.ReadUntilAsync(PacketId.InventorySnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(invAfterSell, out var sellSnap));
            Assert.DoesNotContain(sellSnap.Slots, s => s.ItemId == seed.ConsumableId);
            var combatAfterSell = await client2.ReadUntilAsync(PacketId.CombatState);
            Assert.True(Phase7WireDecoders.TryDecodeCombatState(combatAfterSell, out _, out _, out _, out _, out _, out _, out var goldAfterSell, out _));
            var expectedGoldAfterSell = expectedGoldAfterBuy + consumableSellPrice;
            Assert.Equal(expectedGoldAfterSell, goldAfterSell);

            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildShopBuy(seed.ShopId, seed.ConsumableId, 1, Guid.NewGuid()));
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.ShopBuyResult))[1]);
            var invForBank = await client2.ReadUntilAsync(PacketId.InventorySnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(invForBank, out var bankInv));
            var bankSlot = bankInv.Slots.First(s => s.ItemId == seed.ConsumableId).SlotIndex;
            var combatAfterBuy2 = await client2.ReadUntilAsync(PacketId.CombatState);
            Assert.True(Phase7WireDecoders.TryDecodeCombatState(combatAfterBuy2, out _, out _, out _, out _, out _, out _, out var goldAfterBuy2, out _));
            var expectedGoldAfterBuy2 = expectedGoldAfterSell - consumableBuyPrice;
            Assert.Equal(expectedGoldAfterBuy2, goldAfterBuy2);

            // Step 13 (banque, objet) : InventorySnapshot + BankSnapshot verifies des deux
            // cotes du transfert (l'objet quitte l'inventaire, arrive en banque).
            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildBankDepositItem((byte)bankSlot, 1, Guid.NewGuid()));
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.BankDepositResult))[1]);
            var invAfterItemDeposit = await client2.ReadUntilAsync(PacketId.InventorySnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(invAfterItemDeposit, out var invAfterItemDepositSnap));
            Assert.DoesNotContain(invAfterItemDepositSnap.Slots, s => s.ItemId == seed.ConsumableId);
            var bankAfterDeposit = await client2.ReadUntilAsync(PacketId.BankSnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeBankSnapshot(bankAfterDeposit, out var depositedBank));
            Assert.Contains(depositedBank.Slots, s => s.ItemId == seed.ConsumableId);

            // Step 13 (banque, or) : CombatState.gold + BankSnapshot.BankGold verifies des deux
            // cotes (pas seulement le cote banque).
            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildBankDepositGold(bankGoldDeposit, Guid.NewGuid()));
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.BankDepositResult))[1]);
            var combatAfterGoldDeposit = await client2.ReadUntilAsync(PacketId.CombatState);
            Assert.True(Phase7WireDecoders.TryDecodeCombatState(combatAfterGoldDeposit, out _, out _, out _, out _, out _, out _, out var goldAfterGoldDeposit, out _));
            var expectedGoldAfterGoldDeposit = expectedGoldAfterBuy2 - bankGoldDeposit;
            Assert.Equal(expectedGoldAfterGoldDeposit, goldAfterGoldDeposit);
            var bankGoldSnap = await client2.ReadUntilAsync(PacketId.BankSnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeBankSnapshot(bankGoldSnap, out var goldBank));
            Assert.Equal(bankGoldDeposit, goldBank.BankGold);

            // Step 13 (retrait objet) : BankSnapshot + InventorySnapshot verifies apres retrait
            // (l'objet quitte la banque, revient dans l'inventaire) — auparavant non verifie.
            var depositedSlot = depositedBank.Slots.First(s => s.ItemId == seed.ConsumableId).SlotIndex;
            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildBankWithdrawItem((byte)depositedSlot, 1, Guid.NewGuid()));
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.BankWithdrawResult))[1]);
            var invAfterItemWithdraw = await client2.ReadUntilAsync(PacketId.InventorySnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(invAfterItemWithdraw, out var invAfterItemWithdrawSnap));
            Assert.Contains(invAfterItemWithdrawSnap.Slots, s => s.ItemId == seed.ConsumableId && s.Quantity == 1);
            var bankAfterItemWithdraw = await client2.ReadUntilAsync(PacketId.BankSnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeBankSnapshot(bankAfterItemWithdraw, out var bankAfterItemWithdrawSnap));
            Assert.DoesNotContain(bankAfterItemWithdrawSnap.Slots, s => s.ItemId == seed.ConsumableId);
            Assert.Equal(bankGoldDeposit, bankAfterItemWithdrawSnap.BankGold);

            // Step 13 (retrait or, AJOUTE) : jusqu'ici absent du flux — CombatState.gold +
            // BankSnapshot.BankGold verifies des deux cotes apres un retrait d'or de la banque.
            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildBankWithdrawGold(bankGoldWithdraw, Guid.NewGuid()));
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.BankWithdrawResult))[1]);
            var combatAfterGoldWithdraw = await client2.ReadUntilAsync(PacketId.CombatState);
            Assert.True(Phase7WireDecoders.TryDecodeCombatState(combatAfterGoldWithdraw, out _, out _, out _, out _, out _, out _, out var goldAfterGoldWithdraw, out _));
            expectedGoldAfterGoldWithdraw = expectedGoldAfterGoldDeposit + bankGoldWithdraw;
            Assert.Equal(expectedGoldAfterGoldWithdraw, goldAfterGoldWithdraw);
            var bankAfterGoldWithdraw = await client2.ReadUntilAsync(PacketId.BankSnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeBankSnapshot(bankAfterGoldWithdraw, out var bankAfterGoldWithdrawSnap));
            Assert.Equal(expectedBankGoldAfterWithdraw, bankAfterGoldWithdrawSnap.BankGold);

            await using var killer = new Phase7TcpTestClient();
            await RegisterLoginSelectAsync(killer, port, killerUser, password, "Killer", seed.ClassId);
            await client2.DrainPendingAsync();
            var deadCombat = await KillPlayerWithMeleeAsync(killer, user, client2);
            Assert.True(Phase7WireDecoders.TryDecodeCombatState(deadCombat, out _, out _, out var deadHp, out _, out _, out _, out var goldWhileDead, out var isDead));
            Assert.True(isDead);
            Assert.Equal(0, deadHp);
            Assert.Equal(expectedGoldAfterGoldWithdraw, goldWhileDead);

            // Step 15 (respawn) : HP/MP au max, ET position (carte + pixels) verifiee contre
            // world_spawn_settings / GameplayLimits.DefaultSpawnTileX/Y via le PositionUpdate
            // diffuse par le serveur juste apres le respawn.
            await client2.SendFrameAsync(Phase7TcpPacketBuilder.BuildRespawn());
            Assert.NotEqual(0, (await client2.ReadUntilAsync(PacketId.RespawnResult))[1]);
            var respawnCombat = await client2.ReadUntilAsync(PacketId.CombatState);
            Assert.True(Phase7WireDecoders.TryDecodeCombatState(respawnCombat, out _, out _, out var respawnHp, out var respawnMaxHp, out var respawnMp, out var respawnMaxMp, out var respawnGold, out var respawnDead));
            Assert.False(respawnDead);
            Assert.Equal(respawnMaxHp, respawnHp);
            Assert.Equal(respawnMaxMp, respawnMp);
            Assert.Equal(expectedGoldAfterGoldWithdraw, respawnGold);

            var respawnPosition = await client2.ReadUntilAsync(PacketId.PositionUpdate);
            Assert.True(Phase7WireDecoders.TryDecodePositionUpdate(
                respawnPosition, out var respawnUsername, out var respawnMapId, out var respawnPosX, out var respawnPosY));
            Assert.Equal(user, respawnUsername);
            Assert.Equal(seed.RuntimeMapId, respawnMapId);
            Assert.Equal(respawnPixelX, respawnPosX);
            Assert.Equal(respawnPixelY, respawnPosY);

            await client2.DisconnectAsync();
        }
        finally
        {
            await host.StopAsync();
        }

        using var host2 = Phase7PostgresE2EHost
            .CreateBuilder(_fixture.ConnectionString, port)
            .Build();
        await host2.StartAsync();
        try
        {
            await using var client3 = new Phase7TcpTestClient();
            await client3.ConnectAsync("127.0.0.1", port);
            _ = await client3.ReadFrameAsync();
            await client3.SendFrameAsync(Phase7TcpPacketBuilder.BuildReconnect(token));
            Assert.NotEqual(0, (await client3.ReadUntilAsync(PacketId.ReconnectResult))[1]);
            await client3.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(characterId));
            Assert.NotEqual(0, (await client3.ReadUntilAsync(PacketId.CharacterSelectResult))[1]);

            // Step 16 (redemarrage + reconnexion) : chaque champ persiste est verifie avec sa
            // valeur EXACTE (equipement, quantites d'inventaire, or joueur, or banque, slots
            // banque, niveau/XP, position de respawn) — plus de simples ">= 1" / ">= 0".
            var persistedCombat = await client3.ReadUntilAsync(PacketId.CombatState);
            Assert.True(Phase7WireDecoders.TryDecodeCombatState(
                persistedCombat,
                out var persistedLevel,
                out var persistedXp,
                out var persistedHp,
                out var persistedMaxHp,
                out var persistedMp,
                out var persistedMaxMp,
                out var persistedGold,
                out var persistedDead));

            var persistedInv = await client3.ReadUntilAsync(PacketId.InventorySnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(persistedInv, out var persisted));

            var persistedBank = await client3.ReadUntilAsync(PacketId.BankSnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeBankSnapshot(persistedBank, out var persistedBankSnap));

            var persistedPosition = await client3.ReadUntilAsync(PacketId.PositionUpdate);
            Assert.True(Phase7WireDecoders.TryDecodePositionUpdate(
                persistedPosition, out var persistedUsername, out var persistedMapId, out var persistedPixelX, out var persistedPixelY));

            // Equipement exact : l'epee reste equipee, aucune armure.
            Assert.Equal(seed.WeaponId, persisted.EquippedWeaponItemId);
            Assert.Null(persisted.EquippedArmorItemId);

            // Inventaire exact : uniquement la potion retiree de la banque, quantite 1.
            var persistedItemSlots = persisted.Slots.Where(s => s.ItemId is not null).ToList();
            Assert.Single(persistedItemSlots);
            Assert.Equal(seed.ConsumableId, persistedItemSlots[0].ItemId);
            Assert.Equal(1, persistedItemSlots[0].Quantity);

            // Or joueur + banque exacts, banque vide de tout objet.
            Assert.Equal(expectedGoldAfterGoldWithdraw, persistedGold);
            Assert.Equal(expectedBankGoldAfterWithdraw, persistedBankSnap.BankGold);
            Assert.DoesNotContain(persistedBankSnap.Slots, s => s.ItemId is not null);

            // Progression exacte (niveau + XP calcules via ProgressionCurve, pas de tautologie).
            Assert.Equal(expectedLevelAfterKill, persistedLevel);
            Assert.Equal(expectedXpAfterKill, persistedXp);
            Assert.False(persistedDead);
            Assert.Equal(persistedMaxHp, persistedHp);
            Assert.Equal(persistedMaxMp, persistedMp);

            // Position de respawn persistee (carte + tuile de spawn monde publiee).
            Assert.Equal(user, persistedUsername);
            Assert.Equal(seed.RuntimeMapId, persistedMapId);
            Assert.Equal(respawnPixelX, persistedPixelX);
            Assert.Equal(respawnPixelY, persistedPixelY);
        }
        finally
        {
            await host2.StopAsync();
        }
    }

    /// <summary>Verifie qu'aucun second ExperienceGain n'arrive dans la fenetre courte suivant une mise a mort (draine au passage les paquets residuels de la cible collaterale eventuelle).</summary>
    private static async Task AssertNoExtraExperienceGainAsync(Phase7TcpTestClient client, TimeSpan? window = null)
    {
        var deadline = DateTime.UtcNow + (window ?? TimeSpan.FromMilliseconds(300));
        while (true)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            byte[] frame;
            try
            {
                frame = await client.ReadFrameAsync(remaining);
            }
            catch
            {
                break;
            }

            Assert.NotEqual((byte)PacketId.ExperienceGain, frame[0]);
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task GroundPickupRace_TwoClients_ExactlyOneWinner()
    {
        var (seed, groundItemId) = await SeedPublishedContentAsync(seedGroundItem: true, useConsumableAsGround: true);
        var port = Phase7TcpTestPorts.GetFreePort();
        using var host = Phase7PostgresE2EHost
            .CreateBuilder(_fixture.ConnectionString, port)
            .Build();
        await host.StartAsync();
        try
        {
            var userA = $"a-{Guid.NewGuid():N}"[..16];
            var userB = $"b-{Guid.NewGuid():N}"[..16];
            const string password = "password12345";
            await using var tcpA = new Phase7TcpTestClient();
            await using var tcpB = new Phase7TcpTestClient();
            await RegisterLoginSelectAsync(tcpA, port, userA, password, "Alpha", seed.ClassId);
            await RegisterLoginSelectAsync(tcpB, port, userB, password, "Beta", seed.ClassId);
            await tcpA.SendFrameAsync(Phase7TcpPacketBuilder.BuildPickup(groundItemId!.Value));
            await tcpB.SendFrameAsync(Phase7TcpPacketBuilder.BuildPickup(groundItemId.Value));
            var rA = await tcpA.ReadUntilAsync(PacketId.PickupItemResult);
            var rB = await tcpB.ReadUntilAsync(PacketId.PickupItemResult);
            var wins = (rA[1] != 0 ? 1 : 0) + (rB[1] != 0 ? 1 : 0);
            Assert.Equal(1, wins);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Reconnect_DisplacesStaleConnection()
    {
        var (seed, _) = await SeedPublishedContentAsync();
        var port = Phase7TcpTestPorts.GetFreePort();
        using var host = Phase7PostgresE2EHost
            .CreateBuilder(_fixture.ConnectionString, port)
            .Build();
        await host.StartAsync();
        try
        {
            var user = $"rc-{Guid.NewGuid():N}"[..16];
            const string password = "password12345";
            await using var oldClient = new Phase7TcpTestClient();
            var token = await RegisterLoginCreateAsync(oldClient, port, user, password, "Stale", seed.ClassId);
            await using var newClient = new Phase7TcpTestClient();
            await newClient.ConnectAsync("127.0.0.1", port);
            _ = await newClient.ReadFrameAsync();
            await newClient.SendFrameAsync(Phase7TcpPacketBuilder.BuildReconnect(token));
            Assert.NotEqual(0, (await newClient.ReadUntilAsync(PacketId.ReconnectResult))[1]);
            await Task.Delay(200);
            await Assert.ThrowsAnyAsync<Exception>(() => oldClient.ReadFrameAsync(TimeSpan.FromMilliseconds(500)));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task CombatRace_TwoClients_SameMonster_ExactlyOneExperienceGrant()
    {
        var (seed, _) = await SeedPublishedContentAsync(monsterSpawnCount: 1);
        var port = Phase7TcpTestPorts.GetFreePort();
        using var host = Phase7PostgresE2EHost
            .CreateBuilder(_fixture.ConnectionString, port)
            .Build();
        await host.StartAsync();
        try
        {
            const string password = "password12345";
            await using var a = new Phase7TcpTestClient();
            await using var b = new Phase7TcpTestClient();
            var characterIdA = await RegisterLoginSelectAsync(
                a, port, $"ca-{Guid.NewGuid():N}"[..16], password, "FighterA", seed.ClassId);
            var characterIdB = await RegisterLoginSelectAsync(
                b, port, $"cb-{Guid.NewGuid():N}"[..16], password, "FighterB", seed.ClassId);

            var xpEvents = 0;
            var winnerLock = new object();
            Guid? winnerCharacterId = null;
            long grantedXp = -1;

            async Task AttackLoopAsync(Phase7TcpTestClient client, Guid characterId)
            {
                for (var i = 0; i < 20; i++)
                {
                    await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildMelee("Slime"));
                    try
                    {
                        var frame = await client.ReadUntilAnyAsync(
                            [PacketId.MeleeAttackResult, PacketId.ExperienceGain, PacketId.CombatState],
                            TimeSpan.FromSeconds(2));
                        if (frame[0] == (byte)PacketId.ExperienceGain)
                        {
                            Assert.True(Phase7WireDecoders.TryDecodeExperienceGain(frame, out var amount, out _, out _));
                            Interlocked.Increment(ref xpEvents);
                            lock (winnerLock)
                            {
                                winnerCharacterId = characterId;
                                grantedXp = amount;
                            }

                            return;
                        }
                    }
                    catch (TimeoutException)
                    {
                        // continue
                    }

                    await Task.Delay(CombatFormulas.BasicAttackCooldownMs + 20);
                }
            }

            await Task.WhenAll(AttackLoopAsync(a, characterIdA), AttackLoopAsync(b, characterIdB));

            // Exactement un gain d'XP cote wire, pour le montant exact attendu (monstre
            // niveau 1 seede) — pas seulement "au moins un evenement".
            Assert.Equal(1, Volatile.Read(ref xpEvents));
            var expectedXp = CombatFormulas.MonsterExperienceReward(1);
            Assert.Equal(expectedXp, grantedXp);
            Assert.NotNull(winnerCharacterId);
            var loserCharacterId = winnerCharacterId!.Value == characterIdA ? characterIdB : characterIdA;

            // Etat final autoritatif du monstre : disparu de la carte (pas seulement "un
            // paquet XP a ete recu"), directement depuis le repository combat du serveur.
            var combatMutations = host.Services.GetRequiredService<ICombatMutationRepository>();
            Assert.Empty(combatMutations.ListMonstersOnMap(seed.RuntimeMapId));

            // XP reellement persiste en base : le gagnant a exactement l'XP du monstre,
            // le perdant n'a recu aucune XP fantome.
            using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
            var (winnerExperience, loserExperience) = await gate.ExecuteAsync(async (db, ct) =>
            {
                var winner = await db.PlayerCharacters.AsNoTracking()
                    .SingleAsync(c => c.Id == winnerCharacterId.Value, ct)
                    .ConfigureAwait(false);
                var loser = await db.PlayerCharacters.AsNoTracking()
                    .SingleAsync(c => c.Id == loserCharacterId, ct)
                    .ConfigureAwait(false);
                return (winner.Experience, loser.Experience);
            }).ConfigureAwait(false);

            Assert.Equal(expectedXp, winnerExperience);
            Assert.Equal(0L, loserExperience);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ShopBuyRace_TwoClients_FinalStockUnit_ExactlyOneWinner()
    {
        var (seed, _) = await SeedPublishedContentAsync();
        await PublishLimitedStockWeaponListingAsync(seed);
        var port = Phase7TcpTestPorts.GetFreePort();
        using var host = Phase7PostgresE2EHost
            .CreateBuilder(_fixture.ConnectionString, port)
            .Build();
        await host.StartAsync();
        try
        {
            const string password = "password12345";
            var userA = $"sa-{Guid.NewGuid():N}"[..16];
            var userB = $"sb-{Guid.NewGuid():N}"[..16];
            await using var tcpA = new Phase7TcpTestClient();
            await using var tcpB = new Phase7TcpTestClient();
            var characterIdA = await RegisterLoginSelectAsync(tcpA, port, userA, password, "BuyerA", seed.ClassId);
            var characterIdB = await RegisterLoginSelectAsync(tcpB, port, userB, password, "BuyerB", seed.ClassId);

            var requestA = Guid.NewGuid();
            var requestB = Guid.NewGuid();
            await Task.WhenAll(
                tcpA.SendFrameAsync(Phase7TcpPacketBuilder.BuildShopBuy(seed.ShopId, seed.WeaponId, 1, requestA)),
                tcpB.SendFrameAsync(Phase7TcpPacketBuilder.BuildShopBuy(seed.ShopId, seed.WeaponId, 1, requestB)));

            var resultA = await tcpA.ReadUntilAsync(PacketId.ShopBuyResult);
            var resultB = await tcpB.ReadUntilAsync(PacketId.ShopBuyResult);
            var successA = resultA[1] != 0;
            var successB = resultB[1] != 0;
            Assert.NotEqual(successA, successB);
            Assert.True(successA || successB);

            int qtyA = 0;
            int qtyB = 0;
            if (successA)
            {
                var invA = await tcpA.ReadUntilAsync(PacketId.InventorySnapshot);
                Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(invA, out var snapA));
                qtyA = snapA.Slots.Where(s => s.ItemId == seed.WeaponId).Sum(s => s.Quantity);
            }

            if (successB)
            {
                var invB = await tcpB.ReadUntilAsync(PacketId.InventorySnapshot);
                Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(invB, out var snapB));
                qtyB = snapB.Slots.Where(s => s.ItemId == seed.WeaponId).Sum(s => s.Quantity);
            }

            Assert.Equal(successA ? 1 : 0, qtyA);
            Assert.Equal(successB ? 1 : 0, qtyB);

            using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
            var invRepo = new PostgresInventoryRepository(gate);
            if (!successA)
            {
                var inv = await invRepo.GetAsync(characterIdA);
                qtyA = inv.Slots.Where(s => s.ItemId == seed.WeaponId).Sum(s => s.Quantity);
                Assert.Equal(0, qtyA);
            }

            if (!successB)
            {
                var inv = await invRepo.GetAsync(characterIdB);
                qtyB = inv.Slots.Where(s => s.ItemId == seed.WeaponId).Sum(s => s.Quantity);
                Assert.Equal(0, qtyB);
            }

            var stock = await gate.ExecuteAsync(async (db, ct) =>
                await db.PlayerShopStock.AsNoTracking()
                    .Where(s => s.ShopId == seed.ShopId && s.ItemId == seed.WeaponId)
                    .Select(s => s.Remaining)
                    .SingleOrDefaultAsync(ct));
            Assert.Equal(0, stock);

            var (goldA, goldB) = await gate.ExecuteAsync(async (db, ct) =>
            {
                var a = await db.PlayerCharacters.AsNoTracking().SingleAsync(c => c.Id == characterIdA, ct);
                var b = await db.PlayerCharacters.AsNoTracking().SingleAsync(c => c.Id == characterIdB, ct);
                return (a.Gold, b.Gold);
            });
            var winnerPaid = 100;
            if (successA)
            {
                Assert.Equal(GameplayLimits.StartingGold - winnerPaid, goldA);
                Assert.Equal(GameplayLimits.StartingGold, goldB);
            }
            else
            {
                Assert.Equal(GameplayLimits.StartingGold, goldA);
                Assert.Equal(GameplayLimits.StartingGold - winnerPaid, goldB);
            }

            await host.StopAsync();
            using var gate2 = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
            var stockAfterRestart = await gate2.ExecuteAsync(async (db, ct) =>
                await db.PlayerShopStock.AsNoTracking()
                    .Where(s => s.ShopId == seed.ShopId && s.ItemId == seed.WeaponId)
                    .Select(s => s.Remaining)
                    .SingleOrDefaultAsync(ct));
            Assert.Equal(0, stockAfterRestart);
        }
        finally
        {
            await RestoreDefaultShopListingAsync(seed);
            await host.StopAsync();
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ShopBuy_IdempotentRetry_DoesNotDuplicateItem()
    {
        var (seed, _) = await SeedPublishedContentAsync();
        var port = Phase7TcpTestPorts.GetFreePort();
        using var host = Phase7PostgresE2EHost
            .CreateBuilder(_fixture.ConnectionString, port)
            .Build();
        await host.StartAsync();
        try
        {
            await using var client = new Phase7TcpTestClient();
            await RegisterLoginSelectAsync(
                client,
                port,
                $"sh-{Guid.NewGuid():N}"[..16],
                "password12345",
                "Shopper",
                seed.ClassId);
            var requestId = Guid.NewGuid();
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildShopBuy(seed.ShopId, seed.ConsumableId, 1, requestId));
            Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.ShopBuyResult))[1]);
            var inv1 = await client.ReadUntilAsync(PacketId.InventorySnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(inv1, out var snap1));
            var qty1 = snap1.Slots.Where(s => s.ItemId == seed.ConsumableId).Sum(s => s.Quantity);

            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildShopBuy(seed.ShopId, seed.ConsumableId, 1, requestId));
            Assert.NotEqual(0, (await client.ReadUntilAsync(PacketId.ShopBuyResult))[1]);
            var inv2 = await client.ReadUntilAsync(PacketId.InventorySnapshot);
            Assert.True(Phase7WireDecoders.TryDecodeInventorySnapshot(inv2, out var snap2));
            var qty2 = snap2.Slots.Where(s => s.ItemId == seed.ConsumableId).Sum(s => s.Quantity);
            Assert.Equal(qty1, qty2);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task ChatWhisper_DoesNotLeakToThirdParty()
    {
        var (seed, _) = await SeedPublishedContentAsync();
        var port = Phase7TcpTestPorts.GetFreePort();
        using var host = Phase7PostgresE2EHost
            .CreateBuilder(_fixture.ConnectionString, port)
            .Build();
        await host.StartAsync();
        try
        {
            const string password = "password12345";
            var userA = $"wa-{Guid.NewGuid():N}"[..16];
            var userB = $"wb-{Guid.NewGuid():N}"[..16];
            var userC = $"wc-{Guid.NewGuid():N}"[..16];
            await using var a = new Phase7TcpTestClient();
            await using var b = new Phase7TcpTestClient();
            await using var c = new Phase7TcpTestClient();
            await RegisterLoginSelectAsync(a, port, userA, password, "Alice", seed.ClassId);
            await RegisterLoginSelectAsync(b, port, userB, password, "Bob", seed.ClassId);
            await RegisterLoginSelectAsync(c, port, userC, password, "Carol", seed.ClassId);

            await a.SendFrameAsync(Phase7TcpPacketBuilder.BuildChat(ChatChannel.Whisper, "secret", userB));
            var toBob = await b.ReadUntilAsync(PacketId.ChatMessage);
            Assert.True(Phase7WireDecoders.TryDecodeChatMessage(toBob, out var channel, out _, out _, out var msg));
            Assert.Equal(ChatChannel.Whisper, channel);
            Assert.Equal("secret", msg);

            await Assert.ThrowsAnyAsync<Exception>(() =>
                c.ReadUntilAsync(PacketId.ChatMessage, TimeSpan.FromMilliseconds(400)));
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static async Task DrainOptionalPacketsAsync(Phase7TcpTestClient client, params PacketId[] ids)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));
            while (!cts.IsCancellationRequested)
            {
                var frame = await client.ReadFrameAsync(TimeSpan.FromMilliseconds(200));
                if (ids.Any(i => frame[0] == (byte)i))
                {
                    return;
                }
            }
        }
        catch
        {
            // optional drain
        }
    }


    private async Task PublishLimitedStockWeaponListingAsync(Phase7PostgresContentSeedResult seed)
    {
        await SaveShopListingAsync(seed, [
            new ShopListing { ItemId = seed.WeaponId, Price = 100, Stock = 1 },
        ]);
    }

    private async Task RestoreDefaultShopListingAsync(Phase7PostgresContentSeedResult seed)
    {
        var shop = Phase7ContentSeed.CreateDefaultShop();
        await SaveShopListingAsync(seed, shop.Listings);
    }

    private async Task SaveShopListingAsync(Phase7PostgresContentSeedResult seed, IReadOnlyList<ShopListing> listings)
    {
        using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var items = new PostgresItemRepository(gate);
        var shops = new PostgresShopRepository(gate, items);
        var shop = Phase7ContentSeed.CreateDefaultShop();
        shop.Listings = listings.ToList();

        var revision = (await shops.LoadByIdAsync(seed.ShopId)
            ?? throw new InvalidOperationException("Shop draft missing for shop listing publish.")).Revision;

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var saved = await shops.SaveAsync(new SaveShopRequest
            {
                ShopId = seed.ShopId,
                Definition = shop,
                ExpectedRevision = revision,
                Intent = SaveContentIntent.Publish,
            });
            if (saved is SaveShopResult.Success)
            {
                return;
            }

            if (saved is SaveShopResult.Conflict conflict)
            {
                revision = conflict.CurrentRevision;
                continue;
            }

            throw new InvalidOperationException("Shop listing publish failed: " + saved.GetType().Name);
        }

        throw new InvalidOperationException("Shop listing publish conflicted after retries.");
    }

    private async Task<(Phase7PostgresContentSeedResult Seed, Guid? GroundItemId)> SeedPublishedContentAsync(
        bool seedGroundItem = false,
        bool useConsumableAsGround = false,
        int monsterSpawnCount = 2)
    {
        // Dispose the seed gate before starting the server host so two long-lived
        // FrogDbContext instances do not share the Npgsql pool during teardown.
        using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var seed = await Phase7PostgresContentSeed.PublishAsync(gate, monsterSpawnCount);
        await RestoreDefaultShopListingAsync(seed);
        Guid? groundId = null;
        if (seedGroundItem)
        {
            var itemId = useConsumableAsGround ? seed.ConsumableId : seed.WeaponId;
            groundId = await Phase7PostgresContentSeed.SeedGroundWeaponAsync(gate, itemId, seed.RuntimeMapId);
        }

        return (seed, groundId);
    }

    private static async Task<long> KillMonsterOrReadExperienceAsync(Phase7TcpTestClient client, string monsterName)
    {
        for (var i = 0; i < 12; i++)
        {
            await client.SendFrameAsync(Phase7TcpPacketBuilder.BuildMelee(monsterName));
            try
            {
                var frame = await client.ReadUntilAnyAsync(
                    [PacketId.MeleeAttackResult, PacketId.ExperienceGain, PacketId.CombatState],
                    TimeSpan.FromSeconds(3));
                if (frame[0] == (byte)PacketId.ExperienceGain
                    && Phase7WireDecoders.TryDecodeExperienceGain(frame, out var amount, out _, out _))
                {
                    return amount;
                }
            }
            catch (TimeoutException)
            {
                // continue attacking
            }

            await Task.Delay(CombatFormulas.BasicAttackCooldownMs + 50);
        }

        try
        {
            var frame = await client.ReadUntilAsync(PacketId.ExperienceGain, TimeSpan.FromSeconds(1));
            if (Phase7WireDecoders.TryDecodeExperienceGain(frame, out var amount, out _, out _))
            {
                return amount;
            }
        }
        catch (TimeoutException)
        {
            // fall through
        }

        var combat = await client.ReadUntilAsync(PacketId.CombatState);
        if (Phase7WireDecoders.TryDecodeCombatState(combat, out _, out var xp, out _, out _, out _, out _, out _, out _) && xp > 0)
        {
            return xp;
        }

        throw new TimeoutException("Monster XP was not awarded.");
    }

    private static async Task KillMonsterWithMeleeAsync(Phase7TcpTestClient client, string monsterName)
    {
        _ = await KillMonsterOrReadExperienceAsync(client, monsterName);
    }

    private static async Task<byte[]> KillPlayerWithMeleeAsync(Phase7TcpTestClient attacker, string targetUser, Phase7TcpTestClient victim)
    {
        for (var i = 0; i < 30; i++)
        {
            await attacker.SendFrameAsync(Phase7TcpPacketBuilder.BuildMelee(targetUser));
            _ = await attacker.ReadUntilAsync(PacketId.MeleeAttackResult);
            try
            {
                var frame = await victim.ReadUntilAnyAsync(
                    [PacketId.DeathNotify, PacketId.CombatState],
                    TimeSpan.FromSeconds(1));
                if (frame[0] == (byte)PacketId.DeathNotify)
                {
                    return await victim.ReadUntilAsync(PacketId.CombatState);
                }

                if (Phase7WireDecoders.TryDecodeCombatState(frame, out _, out _, out _, out _, out _, out _, out _, out var dead) && dead)
                {
                    return frame;
                }
            }
            catch (TimeoutException)
            {
                // keep attacking
            }

            await Task.Delay(CombatFormulas.BasicAttackCooldownMs + 50);
        }

        throw new TimeoutException("Player was not killed in time.");
    }

    private static async Task<(int level, long experience, int hp, int gold)> ReadLatestCombatStateAsync(Phase7TcpTestClient client)
    {
        var frame = await client.ReadUntilAsync(PacketId.CombatState);
        Assert.True(Phase7WireDecoders.TryDecodeCombatState(frame, out var level, out var xp, out var hp, out _, out _, out _, out var gold, out _));
        return (level, xp, hp, gold);
    }

    private static async Task<Guid> RegisterLoginSelectAsync(
        Phase7TcpTestClient tcp,
        int port,
        string user,
        string password,
        string charName,
        Guid classId)
    {
        await tcp.ConnectAsync("127.0.0.1", port);
        _ = await tcp.ReadFrameAsync();
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildRegister(user, password));
        _ = await tcp.ReadUntilAsync(PacketId.RegisterResult);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(user, password));
        _ = await tcp.ReadUntilAsync(PacketId.LoginResult);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterCreate(charName, classId));
        var create = await tcp.ReadUntilAsync(PacketId.CharacterCreateResult);
        var id = Phase7WireDecoders.DecodeCharacterId(create);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterSelect(id));
        _ = await tcp.ReadUntilAsync(PacketId.CharacterSelectResult);
        await tcp.DrainPendingAsync();
        return Guid.Parse(id);
    }

    private static async Task<string> RegisterLoginCreateAsync(
        Phase7TcpTestClient tcp,
        int port,
        string user,
        string password,
        string charName,
        Guid classId)
    {
        await tcp.ConnectAsync("127.0.0.1", port);
        _ = await tcp.ReadFrameAsync();
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildRegister(user, password));
        _ = await tcp.ReadUntilAsync(PacketId.RegisterResult);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildLogin(user, password));
        var login = await tcp.ReadUntilAsync(PacketId.LoginResult);
        var token = Phase7WireDecoders.DecodeLoginToken(login);
        await tcp.SendFrameAsync(Phase7TcpPacketBuilder.BuildCharacterCreate(charName, classId));
        _ = await tcp.ReadUntilAsync(PacketId.CharacterCreateResult);
        return token;
    }
}
