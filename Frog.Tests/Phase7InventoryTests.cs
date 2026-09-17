using System;
using System.Linq;
using System.Threading.Tasks;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Server.Gameplay;
using Frog.Server.Models;
using Xunit;

namespace Frog.Tests;

public sealed class Phase7InventoryTests
{
    private static InventoryGameplayService CreateService(
        InMemoryCharacterRepository chars,
        InMemoryInventoryRepository invRepo,
        InMemoryEquipmentRepository equipRepo,
        InMemoryGroundItemRepository ground,
        Phase7PublishedContent content)
    {
        var transfers = new InMemoryInventoryTransferRepository(invRepo, equipRepo, ground, content);
        return new InventoryGameplayService(invRepo, transfers, ground, content, equipRepo);
    }

    [Fact]
    public async Task Equip_Weapon_PersistsOnCharacter()
    {
        var content = new Phase7PublishedContent();
        var chars = new InMemoryCharacterRepository();
        var invRepo = new InMemoryInventoryRepository();
        var equipRepo = new InMemoryEquipmentRepository();
        var ground = new InMemoryGroundItemRepository();
        var svc = CreateService(chars, invRepo, equipRepo, ground, content);
        var charSvc = Phase7TestHelpers.CreateCharacterService(chars, content, invRepo);

        var accountId = Guid.NewGuid();
        var created = await charSvc.CreateAsync(accountId, "Gear", Phase7ContentSeed.DefaultClassId);
        var character = created.Character!;
        await invRepo.TryAddAsync(character.Id, Phase7ContentSeed.DefaultWeaponId, 1, 1);

        var session = new Session { Id = Guid.NewGuid(), Username = "gear" };
        session.ApplyFromCharacter(character);
        var result = await svc.TryEquipAsync(session, 0);
        Assert.True(result.Success);
        Assert.Equal(Phase7ContentSeed.DefaultWeaponId, session.EquippedWeaponItemId);

        // Equipment is persisted by the transfer repository's own transaction (equipRepo),
        // not via a redundant post-commit character save.
        var saved = await equipRepo.GetAsync(character.Id);
        Assert.Equal(Phase7ContentSeed.DefaultWeaponId, saved.WeaponItemId);
    }

    [Fact]
    public async Task Equip_RejectsNonWeapon()
    {
        var content = new Phase7PublishedContent();
        var chars = new InMemoryCharacterRepository();
        var invRepo = new InMemoryInventoryRepository();
        var equipRepo = new InMemoryEquipmentRepository();
        var ground = new InMemoryGroundItemRepository();
        var svc = CreateService(chars, invRepo, equipRepo, ground, content);
        var created = await Phase7TestHelpers.CreateCharacterService(chars, content, invRepo)
            .CreateAsync(Guid.NewGuid(), "Pot", Phase7ContentSeed.DefaultClassId);
        await invRepo.TryAddAsync(created.Character!.Id, Phase7ContentSeed.DefaultItemId, 1, 20);
        var session = new Session { Id = Guid.NewGuid(), Username = "pot" };
        session.ApplyFromCharacter(created.Character);
        var result = await svc.TryEquipAsync(session, 0);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Pickup_Concurrent_OnlyOneWins()
    {
        var content = new Phase7PublishedContent();
        var chars = new InMemoryCharacterRepository();
        var invRepo = new InMemoryInventoryRepository();
        var ground = new InMemoryGroundItemRepository();
        var equipRepo = new InMemoryEquipmentRepository();
        var svc = CreateService(chars, invRepo, equipRepo, ground, content);
        var charSvc = Phase7TestHelpers.CreateCharacterService(chars, content, invRepo);
        var a = (await charSvc.CreateAsync(Guid.NewGuid(), "A", Phase7ContentSeed.DefaultClassId)).Character!;
        var b = (await charSvc.CreateAsync(Guid.NewGuid(), "B", Phase7ContentSeed.DefaultClassId)).Character!;
        var dropped = await ground.DropAsync(1, 100, 100, Phase7ContentSeed.DefaultItemId, 1, null);
        var sessionA = new Session { Id = Guid.NewGuid(), Username = "a", CurrentMapId = 1 };
        sessionA.ApplyFromCharacter(a);
        sessionA.PixelX = 100;
        sessionA.PixelY = 100;
        var sessionB = new Session { Id = Guid.NewGuid(), Username = "b", CurrentMapId = 1 };
        sessionB.ApplyFromCharacter(b);
        sessionB.PixelX = 100;
        sessionB.PixelY = 100;

        var t1 = svc.TryPickupAsync(sessionA, dropped.Item!.Id);
        var t2 = svc.TryPickupAsync(sessionB, dropped.Item!.Id);
        await Task.WhenAll(t1, t2);
        var wins = new[] { t1.Result.Success, t2.Result.Success }.Count(x => x);
        Assert.Equal(1, wins);
    }
}
