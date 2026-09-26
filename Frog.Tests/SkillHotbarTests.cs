using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Frog.Client.Config;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Server.Gameplay;
using Frog.Server.Models;
using Frog.Server.Network;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Barre de sorts : compétences publiées (#95) liées aux cases 5–0,
/// lancées par SpellCastRequest. Hello reste 11.
/// </summary>
public sealed class SkillHotbarTests
{
    private static readonly Guid EntailleId = Guid.Parse("bbbbbbbb-0002-4000-8000-0000000000e1");
    private static readonly Guid FrappeId = Guid.Parse("bbbbbbbb-0002-4000-8000-0000000000f2");

    [Fact]
    public void Protocol_Stays11_AndSkillBarReusesSpellCast()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal((byte)PacketId.SpellCastRequest, (byte)48);

        var root = RepoRoot();
        var protocol = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "FrogWireProtocol.cs"));
        Assert.Contains("Version = 11", protocol, StringComparison.Ordinal);
        Assert.DoesNotContain("Version = 12", protocol, StringComparison.Ordinal);

        var shell = File.ReadAllText(Path.Combine(root, "Frog.Client", "MainShellForm.cs"));
        var hotbar = File.ReadAllText(Path.Combine(root, "Frog.Client", "UI", "HudHotbar.cs"));
        Assert.Contains("Barre de sorts", hotbar, StringComparison.Ordinal);
        Assert.Contains("Mêlée (1) — poison", hotbar, StringComparison.Ordinal);
        Assert.Contains("ActivateBoundSkillAsync", shell, StringComparison.Ordinal);
        Assert.Contains("Compétence liée", shell, StringComparison.Ordinal);
        Assert.Contains("SendSpellCastAsync", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("PacketId.SkillHotbar", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void SpellCastFrame_OmitsTarget_AndKeepsAimedLayout()
    {
        var bare = SpellCastRequestWire.EncodeFrame(EntailleId, null);
        Assert.Equal((byte)PacketId.SpellCastRequest, bare[0]);
        Assert.Equal(17, bare.Length);
        Assert.True(PacketDispatcher.TryParseSpellCastRequest(bare.AsSpan(1), out var id, out var target));
        Assert.Equal(EntailleId, id);
        Assert.Null(target);

        var aimed = SpellCastRequestWire.EncodeFrame(FrappeId, "Slime");
        Assert.Equal(1 + 16 + 1 + "Slime".Length, aimed.Length);
        Assert.True(PacketDispatcher.TryParseSpellCastRequest(aimed.AsSpan(1), out var aimedId, out var name));
        Assert.Equal(FrappeId, aimedId);
        Assert.Equal("Slime", name);
    }

    [Fact]
    public async Task PublishedCatalog_CarriesSkillKind_AndBoardBindsFrenchLabels()
    {
        var content = new Phase7PublishedContent();
        content.Publish(Skill("Entaille", EntailleId, TargetType.Self, 12, 800));
        content.Publish(Skill("Frappe", FrappeId, TargetType.SingleEnemy, 0, 400));
        var service = new PublishedCatalogService(
            content,
            content,
            content,
            content,
            content,
            new Phase8InMemoryPublishedContent());
        var wire = await service.BuildAsync();

        var entaille = Assert.Single(wire.Spells, entry => entry.Name == "Entaille");
        Assert.Equal(nameof(SpellKind.Skill), entaille.Kind);
        Assert.Equal(12, entaille.MpCost);
        Assert.Equal(800, entaille.CooldownMs);
        Assert.Equal(nameof(TargetType.Self), entaille.TargetType);
        Assert.Equal(nameof(SpellKind.Spell), Assert.Single(wire.Spells, entry => entry.Name == "Éclair").Kind);

        var skills = PublishedSkillCatalog.FromWire(wire.Spells);
        Assert.Equal(new[] { "Entaille", "Frappe" }, skills.Select(skill => skill.Name).ToArray());
        Assert.DoesNotContain(skills, skill => skill.Name == "Éclair");

        var board = new SkillHotbarBoard();
        board.Reconcile(skills);
        Assert.True(board.IsPinned);
        Assert.Equal(EntailleId, board.IdAt(0));
        Assert.Equal(FrappeId, board.IdAt(1));
        Assert.Null(board.IdAt(2));
        var tip = board.TooltipForHud(SkillHotbarBoard.FirstHudIndex);
        Assert.Contains("Compétence (5)", tip, StringComparison.Ordinal);
        Assert.Contains("Entaille", tip, StringComparison.Ordinal);
        Assert.Contains("Utilisateur", tip, StringComparison.Ordinal);
        Assert.Contains("12 PM", tip, StringComparison.Ordinal);
        Assert.Contains("sans PM", board.TooltipForHud(5), StringComparison.Ordinal);
        Assert.Contains("Un ennemi", board.TooltipForHud(5), StringComparison.Ordinal);
        Assert.Equal("Case 0 — non liée", board.TooltipForHud(9));

        Assert.Contains(SkillHotbarBoard.RemoveLabel, board.MenuFor(0).Select(item => item.Label));
        Assert.True(board.TryBind(0, FrappeId));
        Assert.Equal(FrappeId, board.IdAt(0));
        Assert.Null(board.IdAt(1));
        board.Clear(0);
        board.Reconcile(skills);
        Assert.Null(board.IdAt(0));
        var cleared = board.MenuFor(0).Select(item => item.Label).ToArray();
        Assert.DoesNotContain(SkillHotbarBoard.RemoveLabel, cleared);
        Assert.Contains("Entaille", cleared);
        Assert.Contains("Frappe", cleared);
    }

    [Fact]
    public void SavedBindings_RoundTrip_AndStayEmptyUntilChosen()
    {
        var fresh = new UserSettings();
        fresh.Normalize();
        Assert.Empty(fresh.SkillHotbarBindings);

        var path = Path.Combine(Path.GetTempPath(), "frog-skillbar-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var settings = new UserSettings
            {
                SkillHotbarBindings = new List<string> { EntailleId.ToString("D"), "pas-un-guid" },
            };
            settings.Normalize();
            Assert.Equal(SkillHotbarBoard.SlotCount, settings.SkillHotbarBindings.Count);
            Assert.Equal(EntailleId.ToString("D"), settings.SkillHotbarBindings[0]);
            Assert.Equal(string.Empty, settings.SkillHotbarBindings[1]);

            var store = new ClientSettingsStore(path);
            store.Save(settings);
            var loaded = store.Load();
            Assert.Equal(settings.SkillHotbarBindings, loaded.SkillHotbarBindings);
            Assert.Equal(settings.SkillHotbarBindings, settings.Clone().SkillHotbarBindings);

            var board = new SkillHotbarBoard();
            board.Load(loaded.SkillHotbarBindings);
            Assert.True(board.IsPinned);
            Assert.Equal(EntailleId, board.IdAt(0));
            board.Reconcile(new[]
            {
                new PublishedSkillRef(EntailleId, "Entaille", 12, 800, TargetType.Self),
                new PublishedSkillRef(FrappeId, "Frappe", 0, 400, TargetType.SingleEnemy),
            });
            Assert.Equal(EntailleId, board.IdAt(0));
            Assert.Null(board.IdAt(1));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task PublishedSkill_CastsThroughSpellPipeline_WithoutBeingKnown()
    {
        var content = new Phase7PublishedContent();
        content.Publish(Skill("Entaille", EntailleId, TargetType.Self, 5, 800));
        var foreignId = Guid.Parse("bbbbbbbb-0002-4000-8000-0000000000aa");
        content.Publish(new SpellDefinition
        {
            Id = foreignId,
            Name = "Boule",
            Kind = SpellKind.Spell,
            ManaCost = 4,
            CooldownMs = 0,
            TargetType = TargetType.SingleEnemy,
            IconLogicalPath = "icons/spells/boule.png",
            Description = "Sort non appris",
        });

        var chars = new InMemoryCharacterRepository();
        var charSvc = Phase7TestHelpers.CreateCharacterService(chars, content);
        var combat = Phase7TestHelpers.CreateCombatService(chars, content);
        var created = await charSvc.CreateAsync(Guid.NewGuid(), "Lame", Phase7ContentSeed.DefaultClassId);
        var session = new Session { Id = Guid.NewGuid(), Username = "lame", CurrentMapId = 1 };
        session.ApplyFromCharacter(created.Character!);
        Assert.DoesNotContain(EntailleId, session.KnownSpellIds);

        session.Mp = 0;
        var dry = await combat.TryCastSpellAsync(session, EntailleId, null);
        Assert.False(dry.Success);
        Assert.Equal("Mana insuffisant.", dry.Message);

        session.Mp = 20;
        var cast = await combat.TryCastSpellAsync(session, EntailleId, null);
        Assert.True(cast.Success);
        Assert.Equal("Compétence lancée.", cast.Message);
        Assert.Equal("Entaille", cast.SpellName);
        Assert.Equal(15, session.Mp);

        var cooling = await combat.TryCastSpellAsync(session, EntailleId, null);
        Assert.False(cooling.Success);
        Assert.Equal("Compétence en recharge.", cooling.Message);
        Assert.Equal(15, session.Mp);

        var unknownSpell = await combat.TryCastSpellAsync(session, foreignId, null);
        Assert.False(unknownSpell.Success);
        Assert.Equal("Sort inconnu.", unknownSpell.Message);

        var missing = await combat.TryCastSpellAsync(session, Guid.NewGuid(), null);
        Assert.Equal("Sort inconnu.", missing.Message);
    }

    private static SpellDefinition Skill(string name, Guid id, TargetType target, int mana, int cooldown) => new()
    {
        Id = id,
        Name = name,
        Kind = SpellKind.Skill,
        ManaCost = mana,
        CooldownMs = cooldown,
        TargetType = target,
        IconLogicalPath = "icons/skills/" + id.ToString("N") + ".png",
        Description = name,
    };

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
