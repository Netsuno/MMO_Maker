using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Gameplay;
using Frog.Core.Gameplay;
using Frog.Server.Gameplay;
using Frog.Server.Models;
using Frog.Server.Network;
using Xunit;

namespace Frog.Tests;

public sealed class Phase7EquipPersistenceTests
{
    private sealed class CountingCharacterRepository(InMemoryCharacterRepository inner) : ICharacterRepository
    {
        public int SaveCount { get; private set; }

        public Task<IReadOnlyList<CharacterRecord>> ListByAccountAsync(
            Guid accountId,
            CancellationToken cancellationToken = default)
            => inner.ListByAccountAsync(accountId, cancellationToken);

        public Task<CharacterRecord?> FindByIdAsync(Guid characterId, CancellationToken cancellationToken = default)
            => inner.FindByIdAsync(characterId, cancellationToken);

        public Task<bool> IsOwnedByAccountAsync(
            Guid accountId,
            Guid characterId,
            CancellationToken cancellationToken = default)
            => inner.IsOwnedByAccountAsync(accountId, characterId, cancellationToken);

        public Task<CharacterCreateResult> CreateAsync(
            Guid accountId,
            string displayName,
            Guid classId,
            CharacterStats stats,
            int maxHp,
            int maxMp,
            Guid? startingSpellId,
            int mapId,
            int pixelX,
            int pixelY,
            CancellationToken cancellationToken = default)
            => inner.CreateAsync(
                accountId,
                displayName,
                classId,
                stats,
                maxHp,
                maxMp,
                startingSpellId,
                mapId,
                pixelX,
                pixelY,
                cancellationToken);

        public Task SaveAsync(CharacterRecord character, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return inner.SaveAsync(character, cancellationToken);
        }
    }

    [Fact]
    public async Task Equip_CommitsOnce_WithoutDispatcherCharacterSave()
    {
        var content = new Phase7PublishedContent();
        var innerChars = new InMemoryCharacterRepository();
        var countingChars = new CountingCharacterRepository(innerChars);
        var invRepo = new InMemoryInventoryRepository();
        var equipRepo = new InMemoryEquipmentRepository();
        var ground = new InMemoryGroundItemRepository();
        var transfers = new InMemoryInventoryTransferRepository(invRepo, equipRepo, ground, content);
        var inventory = new InventoryGameplayService(invRepo, transfers, ground, content, equipRepo);
        var charSvc = Phase7TestHelpers.CreateCharacterService(innerChars, content, invRepo);

        var created = await charSvc.CreateAsync(Guid.NewGuid(), "Gear", Phase7ContentSeed.DefaultClassId);
        await invRepo.TryAddAsync(created.Character!.Id, Phase7ContentSeed.DefaultWeaponId, 1, 1);
        var session = new Session { Id = Guid.NewGuid(), Username = "gear" };
        session.ApplyFromCharacter(created.Character);

        var savesBefore = countingChars.SaveCount;
        var result = await inventory.TryEquipAsync(session, 0);
        Assert.True(result.Success);
        Assert.Equal(savesBefore, countingChars.SaveCount);

        var saved = await equipRepo.GetAsync(created.Character.Id);
        Assert.Equal(Phase7ContentSeed.DefaultWeaponId, saved.WeaponItemId);
    }

    [Fact]
    public async Task Equip_DoesNotOverwriteConcurrentGoldMutation()
    {
        var content = new Phase7PublishedContent();
        var chars = new InMemoryCharacterRepository();
        var invRepo = new InMemoryInventoryRepository();
        var equipRepo = new InMemoryEquipmentRepository();
        var ground = new InMemoryGroundItemRepository();
        var transfers = new InMemoryInventoryTransferRepository(invRepo, equipRepo, ground, content);
        var inventory = new InventoryGameplayService(invRepo, transfers, ground, content, equipRepo);
        var charSvc = Phase7TestHelpers.CreateCharacterService(chars, content, invRepo);

        var created = await charSvc.CreateAsync(Guid.NewGuid(), "Rich", Phase7ContentSeed.DefaultClassId);
        await invRepo.TryAddAsync(created.Character!.Id, Phase7ContentSeed.DefaultWeaponId, 1, 1);
        var session = new Session { Id = Guid.NewGuid(), Username = "rich" };
        session.ApplyFromCharacter(created.Character);
        session.Gold = 999;

        var record = await chars.FindByIdAsync(created.Character.Id);
        await chars.SaveAsync(record! with { Gold = 42 });

        var result = await inventory.TryEquipAsync(session, 0);
        Assert.True(result.Success);

        var after = await chars.FindByIdAsync(created.Character.Id);
        Assert.Equal(42, after!.Gold);
        Assert.Equal(Phase7ContentSeed.DefaultWeaponId, session.EquippedWeaponItemId);
    }

    [Fact]
    public async Task Equip_ReconnectRestoresEquipment_FromTransferRepository()
    {
        var content = new Phase7PublishedContent();
        var chars = new InMemoryCharacterRepository();
        var invRepo = new InMemoryInventoryRepository();
        var equipRepo = new InMemoryEquipmentRepository();
        var ground = new InMemoryGroundItemRepository();
        var transfers = new InMemoryInventoryTransferRepository(invRepo, equipRepo, ground, content);
        var inventory = new InventoryGameplayService(invRepo, transfers, ground, content, equipRepo);
        var charSvc = Phase7TestHelpers.CreateCharacterService(chars, content, invRepo);

        var created = await charSvc.CreateAsync(Guid.NewGuid(), "Persist", Phase7ContentSeed.DefaultClassId);
        await invRepo.TryAddAsync(created.Character!.Id, Phase7ContentSeed.DefaultWeaponId, 1, 1);
        var session = new Session { Id = Guid.NewGuid(), Username = "persist" };
        session.ApplyFromCharacter(created.Character);
        Assert.True((await inventory.TryEquipAsync(session, 0)).Success);

        var reloaded = new Session { Id = Guid.NewGuid(), Username = "persist" };
        reloaded.ApplyFromCharacter((await chars.FindByIdAsync(created.Character.Id))!);
        var equipment = await equipRepo.GetAsync(created.Character.Id);
        reloaded.EquippedWeaponItemId = equipment.WeaponItemId;
        reloaded.EquippedArmorItemId = equipment.ArmorItemId;
        Assert.Equal(Phase7ContentSeed.DefaultWeaponId, reloaded.EquippedWeaponItemId);
    }
}
