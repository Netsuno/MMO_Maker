using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Content;
using Frog.Application.Maps;
using Frog.Core.Constants;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Paramètres Système (monnaie, groupe, carte, BGM, termes). Hello reste 11.
/// Le catalogue interrupteurs / variables n’est pas refait ici.
/// </summary>
public sealed class SystemSettingsContentTests
{
    [Fact]
    public void Protocol_Stays11_TileAssetStays48_AndSettingsLiveOnTheSystemShell()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal("Or", SystemDefinition.DefaultCurrencyUnit);
        Assert.Equal("HP", SystemDefinition.DefaultTermHp);
        Assert.Equal("MP", SystemDefinition.DefaultTermMp);
        Assert.Equal(4, SystemDefinition.MaxPartySize);

        var root = RepoRoot();
        var protocol = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "FrogWireProtocol.cs"));
        var tiles = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "TileAssetMetrics.cs"));
        var panel = File.ReadAllText(Path.Combine(root, "Frog.Editor", "Forms", "GameData", "SystemEditorPanel.cs"));
        var migration = File.ReadAllText(Path.Combine(
            root,
            "Frog.Persistence.PostgreSql",
            "Migrations",
            "20260926180000_SystemSettingsDraftPublish.cs"));
        var status = File.ReadAllText(Path.Combine(root, "docs", "progress", "gamedata-systeme", "STATUS.md"));
        Assert.Contains("Version = 11", protocol, StringComparison.Ordinal);
        Assert.DoesNotContain("Version = 12", protocol, StringComparison.Ordinal);
        Assert.Contains("TargetTileSizePixels = 48", tiles, StringComparison.Ordinal);
        Assert.Contains("Unité monétaire", panel, StringComparison.Ordinal);
        Assert.Contains("Terme HP", panel, StringComparison.Ordinal);
        Assert.Contains("Terme MP", panel, StringComparison.Ordinal);
        Assert.Contains("Groupe de départ", panel, StringComparison.Ordinal);
        Assert.Contains("Carte de départ", panel, StringComparison.Ordinal);
        Assert.Contains("Musique du titre", panel, StringComparison.Ordinal);
        Assert.Contains("Musique de départ", panel, StringComparison.Ordinal);
        Assert.Contains("Identifiant", panel, StringComparison.Ordinal);
        Assert.Contains("MapAudioTrack", panel, StringComparison.Ordinal);
        Assert.Contains(SystemDefinition.SingletonId.ToString(), migration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("party_actor1", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("party_actor_1", migration, StringComparison.Ordinal);
        Assert.Contains("**Propriétaire** | Netsun", status, StringComparison.Ordinal);
    }

    [Fact]
    public void Canonicalize_NormalizesMapBgmPaths_AndRejectsPartyAndTerms()
    {
        var definition = SystemDefinition.CreateDefault();
        Assert.True(definition.Validate(out _));
        Assert.True(definition.TitleBgm.IsNone);
        Assert.True(definition.StartBgm.IsNone);

        definition.TitleBgm = new MapAudioTrack
        {
            Asset = @"Assets\Audio\titre.ogg",
            Volume = 80,
            FadeMs = 250,
        };
        definition.StartBgm = new MapAudioTrack
        {
            Asset = "./Assets/Audio/depart.ogg",
            Volume = 60,
            FadeMs = 1000,
        };
        Assert.True(SystemDefinition.TryCanonicalize(definition, out var canonical, out _));
        Assert.Equal("Assets/Audio/titre.ogg", canonical.TitleBgm.Asset);
        Assert.Equal(80, canonical.TitleBgm.Volume);
        Assert.Equal(250, canonical.TitleBgm.FadeMs);
        Assert.Equal("Assets/Audio/depart.ogg", canonical.StartBgm.Asset);
        Assert.Equal(60, canonical.StartBgm.Volume);
        Assert.Equal(1000, canonical.StartBgm.FadeMs);

        definition = SystemDefinition.CreateDefault();
        definition.TitleBgm = new MapAudioTrack { Asset = "  ", Volume = 12, FadeMs = 30 };
        Assert.True(SystemDefinition.TryCanonicalize(definition, out canonical, out _));
        Assert.True(canonical.TitleBgm.IsNone);
        Assert.Equal(MapAudioTrack.DefaultVolume, canonical.TitleBgm.Volume);
        Assert.Equal(0, canonical.TitleBgm.FadeMs);

        definition = SystemDefinition.CreateDefault();
        definition.TitleBgm = new MapAudioTrack { Asset = "/tmp/titre.ogg", Volume = 80, FadeMs = 0 };
        Assert.False(SystemDefinition.TryCanonicalize(definition, out _, out var error));
        Assert.Contains("absolu", error, StringComparison.Ordinal);

        definition = SystemDefinition.CreateDefault();
        definition.StartBgm = new MapAudioTrack { Asset = "../secret.ogg", Volume = 80, FadeMs = 0 };
        Assert.False(SystemDefinition.TryCanonicalize(definition, out _, out error));
        Assert.Contains("traversée", error, StringComparison.Ordinal);

        definition = SystemDefinition.CreateDefault();
        definition.TitleBgm = new MapAudioTrack { Asset = "Assets/Audio/titre.ogg", Volume = 101, FadeMs = 0 };
        Assert.False(SystemDefinition.TryCanonicalize(definition, out _, out error));
        Assert.Contains("volume", error, StringComparison.Ordinal);

        definition = SystemDefinition.CreateDefault();
        definition.CurrencyUnit = " ";
        Assert.False(definition.Validate(out error));
        Assert.Contains("Unité monétaire", error, StringComparison.Ordinal);

        definition = SystemDefinition.CreateDefault();
        definition.TermHp = new string('H', SystemDefinition.MaxTermLength + 1);
        Assert.False(definition.Validate(out error));
        Assert.Contains("Terme HP", error, StringComparison.Ordinal);

        var hero = Guid.NewGuid();
        definition = SystemDefinition.CreateDefault();
        definition.PartyActor1 = hero;
        definition.PartyActor3 = hero;
        Assert.False(definition.Validate(out error));
        Assert.Contains("groupe de départ", error, StringComparison.Ordinal);

        definition = SystemDefinition.CreateDefault();
        definition.PartyActor2 = Guid.Empty;
        Assert.False(definition.Validate(out error));
        Assert.Contains("invalide", error, StringComparison.Ordinal);

        definition = SystemDefinition.CreateDefault();
        definition.StartMapId = Guid.Empty;
        Assert.False(definition.Validate(out error));
        Assert.Contains("carte de départ", error, StringComparison.Ordinal);

        definition = SystemDefinition.CreateDefault();
        definition.Id = Guid.NewGuid();
        Assert.False(definition.Validate(out error));
        Assert.Contains("unique", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DraftPublish_KeepsPartyMapBgmAndTerms_PublishedSnapshotStaysPut()
    {
        var heroA = Guid.NewGuid();
        var heroB = Guid.NewGuid();
        var heroC = Guid.NewGuid();
        var heroD = Guid.NewGuid();
        var actors = new PublishedActors(heroA, heroB, heroC, heroD);
        var maps = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var mapId = await ResourceContentTestData.SaveMapAsync(maps, "Prairie");
        var repository = new InMemorySystemSettingsRepository(actors, maps);
        var session = new SystemSettingsWorkspaceSession(repository);
        await session.EnsureLoadedAsync();
        Assert.Equal("Or", session.Current.CurrencyUnit);
        Assert.False(session.IsDirty);

        session.Current.CurrencyUnit = "Écus";
        session.Current.TermHp = "PV";
        session.Current.TermMp = "PM";
        session.Current.PartyActor1 = heroA;
        session.Current.PartyActor2 = heroB;
        session.Current.PartyActor4 = heroD;
        session.Current.StartMapId = mapId;
        session.Current.TitleBgm = new MapAudioTrack
        {
            Asset = @"Assets\Audio\titre.ogg",
            Volume = 80,
            FadeMs = 250,
        };
        session.Current.StartBgm = new MapAudioTrack
        {
            Asset = "Assets/Audio/depart.ogg",
            Volume = 60,
            FadeMs = 1000,
        };
        session.MarkDirty();

        var draft = Assert.IsType<SaveSystemSettingsResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal(SystemDefinition.SingletonId, draft.SettingsId);
        Assert.Equal(ContentPublishStatus.Draft, session.CurrentStatus);
        Assert.Null(await repository.LoadPublishedAsync());

        session.Current.PartyActor3 = heroC;
        session.MarkDirty();
        var published = Assert.IsType<SaveSystemSettingsResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Equal(draft.SettingsId, published.SettingsId);
        Assert.Equal(ContentPublishStatus.Published, session.CurrentStatus);

        var stored = await repository.LoadPublishedAsync();
        Assert.NotNull(stored);
        Assert.Equal("Écus", stored!.Definition.CurrencyUnit);
        Assert.Equal("PV", stored.Definition.TermHp);
        Assert.Equal("PM", stored.Definition.TermMp);
        Assert.Equal(heroA, stored.Definition.PartyActor1);
        Assert.Equal(heroB, stored.Definition.PartyActor2);
        Assert.Equal(heroC, stored.Definition.PartyActor3);
        Assert.Equal(heroD, stored.Definition.PartyActor4);
        Assert.Equal(mapId, stored.Definition.StartMapId);
        Assert.Equal("Assets/Audio/titre.ogg", stored.Definition.TitleBgm.Asset);
        Assert.Equal(80, stored.Definition.TitleBgm.Volume);
        Assert.Equal(250, stored.Definition.TitleBgm.FadeMs);
        Assert.Equal("Assets/Audio/depart.ogg", stored.Definition.StartBgm.Asset);

        session.Current.CurrencyUnit = "Louis";
        session.MarkDirty();
        Assert.IsType<SaveSystemSettingsResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal("Louis", (await repository.LoadAsync())!.Definition.CurrencyUnit);
        Assert.Equal("Écus", (await repository.LoadPublishedAsync())!.Definition.CurrencyUnit);
        Assert.Equal(heroC, (await repository.LoadPublishedAsync())!.Definition.PartyActor3);
    }

    [Fact]
    public async Task Save_RejectsUnpublishedActor_MissingMap_Conflict_AndDemo()
    {
        var publishedHero = Guid.NewGuid();
        var actors = new PublishedActors(publishedHero);
        var maps = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var mapId = await ResourceContentTestData.SaveMapAsync(maps);
        var repository = new InMemorySystemSettingsRepository(actors, maps);
        var session = new SystemSettingsWorkspaceSession(repository);
        await session.EnsureLoadedAsync();
        session.Current.PartyActor1 = Guid.NewGuid();
        var unpublished = Assert.IsType<SaveSystemSettingsResult.ValidationFailed>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Contains("publié", unpublished.Error, StringComparison.Ordinal);

        session.Current.PartyActor1 = publishedHero;
        session.Current.StartMapId = Guid.NewGuid();
        var missingMap = Assert.IsType<SaveSystemSettingsResult.ValidationFailed>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Contains("introuvable", missingMap.Error, StringComparison.Ordinal);

        session.Current.StartMapId = mapId;
        Assert.IsType<SaveSystemSettingsResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));

        var other = new SystemSettingsWorkspaceSession(repository);
        await other.EnsureLoadedAsync();
        other.Current.CurrencyUnit = "Denier";
        other.MarkDirty();
        session.Current.CurrencyUnit = "Florin";
        session.MarkDirty();
        Assert.IsType<SaveSystemSettingsResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        var conflict = Assert.IsType<SaveSystemSettingsResult.Conflict>(
            await other.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.True(conflict.CurrentRevision >= 1);

        var demo = new InMemorySystemSettingsRepository(
            actors,
            maps,
            ContentRepositoryCapabilities.InMemoryDemo);
        var demoSession = new SystemSettingsWorkspaceSession(demo);
        await demoSession.EnsureLoadedAsync();
        var blocked = Assert.IsType<SaveSystemSettingsResult.NotDurable>(
            await demoSession.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Contains("Persistance", blocked.Message, StringComparison.Ordinal);
    }

    private sealed class PublishedActors : IPublishedActorCatalog
    {
        private readonly Guid[] _ids;

        public PublishedActors(params Guid[] ids) => _ids = ids;

        public Task<IReadOnlyList<ActorDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ActorDefinition> list = _ids
                .Select(id => new ActorDefinition
                {
                    Id = id,
                    Name = "Héros",
                    BaseHp = 10,
                    BaseMp = 10,
                    Str = 1,
                    Agi = 1,
                    Vit = 1,
                    Int = 1,
                    Dex = 1,
                    Luck = 1,
                })
                .ToList();
            return Task.FromResult(list);
        }
    }

    private static string RepoRoot()
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

        throw new InvalidOperationException("Frog.Creator.sln introuvable.");
    }
}
