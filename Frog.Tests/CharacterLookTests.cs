using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Frog.Client.Config;
using Frog.Core.Constants;
using Frog.Core.Gameplay;
using Xunit;

namespace Frog.Tests;

/// <summary>Sélecteur d'apparence : palettes, persistance locale, Hello 11 (Netsun).</summary>
public sealed class CharacterLookTests
{
    [Fact]
    public void Slots_CycleFrenchLabels_AndWrap()
    {
        var draft = CharacterLook.CreateDraft;
        Assert.Equal("Chevalier", draft.Label(CharacterLookSlot.Body));
        Assert.Equal("Naturel", draft.Label(CharacterLookSlot.Hair));
        Assert.Equal("Ocre", draft.Label(CharacterLookSlot.Tunic));
        Assert.True(draft.WearsTunic);
        Assert.False(CharacterLook.Default.WearsTunic);

        var linen = draft.Cycle(CharacterLookSlot.Tunic, +1);
        Assert.Equal("Lin", linen.Label(CharacterLookSlot.Tunic));
        var none = draft.Cycle(CharacterLookSlot.Tunic, -1);
        Assert.Equal("Aucune", none.Label(CharacterLookSlot.Tunic));
        var crimson = none.Cycle(CharacterLookSlot.Tunic, -1);
        Assert.Equal("Cramoisi", crimson.Label(CharacterLookSlot.Tunic));

        var blond = draft.Cycle(CharacterLookSlot.Hair, +2);
        Assert.Equal("Blond", blond.Label(CharacterLookSlot.Hair));
        var forest = draft.Cycle(CharacterLookSlot.Body, +1);
        Assert.Equal("Forêt", forest.Label(CharacterLookSlot.Body));
        Assert.Equal(CharacterLook.BodyLabels.Length, (int)CharacterLook.BodyCount);
        Assert.Equal(CharacterLook.HairLabels.Length, (int)CharacterLook.HairCount);
        Assert.Equal(CharacterLook.TunicLabels.Length, (int)CharacterLook.TunicCount);
    }

    [Fact]
    public void Tint_KeepsIdentityOutlineAndSkin_AndShiftsCloth()
    {
        var blue = CharacterLookTint.Apply(CharacterLookSlot.Body, 0, 127, 146, 255, 255);
        Assert.Equal(new CharacterLookTint.Rgba(127, 146, 255, 255), blue);

        var forest = CharacterLookTint.Apply(CharacterLookSlot.Body, 1, 127, 146, 255, 255);
        Assert.NotEqual(blue, forest);
        Assert.True(forest.G > forest.R && forest.G > forest.B, "forêt reads green");

        var outline = CharacterLookTint.Apply(CharacterLookSlot.Body, 1, 8, 8, 8, 255);
        Assert.Equal(new CharacterLookTint.Rgba(8, 8, 8, 255), outline);

        var clear = CharacterLookTint.Apply(CharacterLookSlot.Body, 1, 10, 20, 30, 0);
        Assert.Equal(0, clear.A);

        var skin = CharacterLookTint.Apply(CharacterLookSlot.Hair, 2, 255, 229, 229, 255);
        Assert.Equal(new CharacterLookTint.Rgba(255, 229, 229, 255), skin);

        var hair = CharacterLookTint.Apply(CharacterLookSlot.Hair, 2, 127, 146, 255, 255);
        Assert.NotEqual(new CharacterLookTint.Rgba(127, 146, 255, 255), hair);

        var ochre = CharacterLookTint.Apply(CharacterLookSlot.Tunic, 1, 186, 122, 64, 255);
        Assert.Equal(new CharacterLookTint.Rgba(186, 122, 64, 255), ochre);
        var linen = CharacterLookTint.Apply(CharacterLookSlot.Tunic, 2, 186, 122, 64, 255);
        Assert.NotEqual(ochre, linen);
        var crimson = CharacterLookTint.Apply(CharacterLookSlot.Tunic, 3, 186, 122, 64, 255);
        Assert.True(crimson.R > crimson.G && crimson.R > crimson.B, "cramoisi reads red");
    }

    [Fact]
    public void Book_BindsCreateId_AndKeepsTunicStyleWhenRemoved()
    {
        var rows = new List<CharacterLookRecord>();
        var look = new CharacterLook(1, 2, 3);
        CharacterLookBook.Remember(rows, null, "Ael", look, tunicWorn: true);
        var id = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";
        Assert.True(CharacterLookBook.BindCreatedId(rows, "Ael", id));
        Assert.True(CharacterLookBook.TryGet(rows, id, null, out var bound));
        Assert.Equal(1, bound.Body);
        Assert.Equal(2, bound.Hair);
        Assert.Equal(3, bound.Tunic);
        Assert.True(bound.TunicWorn);
        Assert.Equal("Ael", bound.DisplayName);

        CharacterLookBook.Remember(rows, id, "Ael", bound.ToLook(), tunicWorn: false);
        Assert.True(CharacterLookBook.TryGet(rows, id, "other", out var off));
        Assert.False(off.TunicWorn);
        Assert.Equal(3, off.Tunic);
        Assert.Equal("Cramoisi", off.ToLook().Label(CharacterLookSlot.Tunic));
    }

    [Fact]
    public void Settings_RoundTripLooks_WithoutDroppingOtherFields()
    {
        var settings = new UserSettings
        {
            LastUsername = "netsun",
            RememberAccount = true,
            AppearanceDraft = CharacterLookRecord.FromLook(CharacterLook.CreateDraft, tunicWorn: true),
        };
        settings.CharacterLooks.Add(CharacterLookRecord.FromLook(
            new CharacterLook(2, 4, 2),
            tunicWorn: true,
            characterId: "id-1",
            displayName: "Mira"));
        var clone = settings.Clone();
        Assert.NotSame(settings.CharacterLooks[0], clone.CharacterLooks[0]);
        Assert.Equal("Mira", clone.CharacterLooks[0].DisplayName);
        Assert.Equal(2, clone.CharacterLooks[0].Body);
        Assert.Equal(4, clone.CharacterLooks[0].Hair);
        Assert.NotNull(clone.AppearanceDraft);
        Assert.Equal(1, clone.AppearanceDraft!.Tunic);
        Assert.Equal("netsun", clone.LastUsername);

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };
        var json = JsonSerializer.Serialize(clone, options);
        var back = JsonSerializer.Deserialize<UserSettings>(json, options);
        Assert.NotNull(back);
        back!.Normalize();
        Assert.Equal("Mira", back.CharacterLooks.Single().DisplayName);
        Assert.Equal((byte)2, back.CharacterLooks[0].Tunic);
        Assert.True(back.CharacterLooks[0].TunicWorn);
        Assert.Equal("netsun", back.LastUsername);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Client_WiresPickerOnCreatePath_WithoutProtocolBump()
    {
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));
        var picker = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Controls", "AppearancePickerPanel.cs"));
        var look = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Core", "Gameplay", "CharacterLook.cs"));
        var assets = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "PlayerWorldAssets.cs"));
        var network = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Network", "FrogGameClient.cs"));
        var protocol = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Core", "Constants", "FrogWireProtocol.cs"));
        var metrics = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Core", "Constants", "WorldMetrics.cs"));
        var help = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Forms", "HelpForm.cs"));

        Assert.Contains("_appearancePicker", shell, StringComparison.Ordinal);
        Assert.Contains("RememberNamedLook(feedback.Normalized, _appearancePicker.Look)", shell, StringComparison.Ordinal);
        Assert.Contains("CharacterLookBook.BindCreatedId", shell, StringComparison.Ordinal);
        Assert.Contains("ApplySavedLook(row.Id, row.DisplayName)", shell, StringComparison.Ordinal);
        Assert.Contains("localLook: _activeLook", shell, StringComparison.Ordinal);
        Assert.Contains("SendCharacterCreateAsync(feedback.Normalized, row.Id)", shell, StringComparison.Ordinal);
        var create = shell.IndexOf("RememberNamedLook(feedback.Normalized, _appearancePicker.Look)", StringComparison.Ordinal);
        var send = shell.IndexOf("SendCharacterCreateAsync(feedback.Normalized, row.Id)", create, StringComparison.Ordinal);
        Assert.True(create >= 0 && send > create, "the look is stored before the existing create packet");

        Assert.Contains("CharacterLook.Title(slot)", picker, StringComparison.Ordinal);
        Assert.Contains("\"Corps\"", look, StringComparison.Ordinal);
        Assert.Contains("\"Cheveux\"", look, StringComparison.Ordinal);
        Assert.Contains("\"Tunique\"", look, StringComparison.Ordinal);
        Assert.Contains("Chevalier", look, StringComparison.Ordinal);
        Assert.Contains("Cramoisi", look, StringComparison.Ordinal);
        Assert.Contains("PlayerWorldAssets.FrameFor", picker, StringComparison.Ordinal);
        Assert.Contains("Clic ou flèches", picker, StringComparison.Ordinal);
        Assert.Contains("Keys.Left", picker, StringComparison.Ordinal);
        Assert.Contains("Keys.Right", picker, StringComparison.Ordinal);
        Assert.DoesNotContain("PacketId", picker, StringComparison.Ordinal);

        Assert.Contains("CharacterLookTint.Apply", assets, StringComparison.Ordinal);
        Assert.Contains("NeedsPalette", assets, StringComparison.Ordinal);
        Assert.DoesNotContain("CharacterLook", network, StringComparison.Ordinal);
        Assert.Contains("Version = 11", protocol, StringComparison.Ordinal);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Contains("public const int DefaultTileSizePixels = 32;", metrics, StringComparison.Ordinal);
        Assert.Contains("Cheveux", help, StringComparison.Ordinal);
        Assert.Contains("Corps, Tunique, Armure, Tête, Casque, Arme", help, StringComparison.Ordinal);
        Assert.DoesNotContain("Marc", picker, StringComparison.Ordinal);
        Assert.DoesNotContain("Marc", shell, StringComparison.Ordinal);
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

        throw new InvalidOperationException("Frog.Creator.sln not found from " + AppContext.BaseDirectory);
    }
}
