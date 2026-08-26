using System;
using System.Linq;
using System.Threading.Tasks;
using Frog.Application.Gameplay;
using Frog.Core.Gameplay;
using Frog.Server.Gameplay;
using Xunit;

namespace Frog.Tests;

public sealed class Phase7CharacterTests
{
    [Fact]
    public async Task CreateAsync_UsesPublishedClass()
    {
        var content = new Phase7PublishedContent();
        var chars = new InMemoryCharacterRepository();
        var inv = new InMemoryInventoryRepository();
        var svc = new CharacterGameplayService(chars, content, inv);

        var accountId = Guid.NewGuid();
        var result = await svc.CreateAsync(accountId, "Aventurier", Phase7ContentSeed.DefaultClassId);
        Assert.Equal(CharacterCreateStatus.Created, result.Status);
        Assert.NotNull(result.Character);
        Assert.Equal(Phase7ContentSeed.DefaultClassId, result.Character!.ClassId);
        Assert.Equal(Phase7ContentSeed.DefaultSpellId, result.Character.StartingSpellId);
    }

    [Fact]
    public async Task CreateAsync_RejectsUnknownClass()
    {
        var content = new Phase7PublishedContent();
        var svc = new CharacterGameplayService(
            new InMemoryCharacterRepository(),
            content,
            new InMemoryInventoryRepository());
        var result = await svc.CreateAsync(Guid.NewGuid(), "Test", Guid.NewGuid());
        Assert.Equal(CharacterCreateStatus.InvalidClass, result.Status);
    }

    [Fact]
    public void Session_ApplyFromCharacter_CopiesStats()
    {
        var stats = new CharacterStats(11, 12, 13, 14, 15, 16);
        var record = new CharacterRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Hero",
            Phase7ContentSeed.DefaultClassId,
            1,
            32,
            48,
            2,
            50,
            90,
            100,
            40,
            50,
            200,
            0,
            false,
            stats,
            Phase7ContentSeed.DefaultSpellId,
            null,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        var session = new Frog.Server.Models.Session { Id = Guid.NewGuid(), Username = "u" };
        session.ApplyFromCharacter(record);
        Assert.Equal(record.Id, session.CharacterGuid);
        Assert.Equal(2, session.Level);
        Assert.Equal(90, session.Hp);
        Assert.Contains(Phase7ContentSeed.DefaultSpellId, session.KnownSpellIds);
    }
}
