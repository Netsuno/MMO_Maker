using System;
using System.IO;
using Frog.Core.Constants;
using Xunit;

namespace Frog.Tests;

/// <summary>Fiche perso — paperdoll slots, French labels, protocol 11 (Netsun). Linux source gates.</summary>
public sealed class CharacterSheetTests
{
    [Fact]
    public void Sheet_UsesExistingLayers_AndFrenchSlotLabels()
    {
        var sheet = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Controls", "CharacterSheetPanel.cs"));
        var assets = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "PlayerWorldAssets.cs"));
        var order = new[] { "Corps", "Tunique", "Armure", "Tête", "Casque", "Arme" };
        var cursor = sheet.IndexOf("Layers =", StringComparison.Ordinal);
        Assert.True(cursor >= 0, "layer table");
        foreach (var label in order)
        {
            var at = sheet.IndexOf("\"" + label + "\"", cursor, StringComparison.Ordinal);
            Assert.True(at > cursor, label);
            cursor = at;
        }

        Assert.Contains("WindowTitle = \"Fiche perso\"", sheet, StringComparison.Ordinal);
        Assert.Contains("TabTitle = \"Fiche\"", sheet, StringComparison.Ordinal);
        Assert.Contains("\"de base\"", sheet, StringComparison.Ordinal);
        Assert.Contains("\"portée\"", sheet, StringComparison.Ordinal);
        Assert.Contains("\"porté\"", sheet, StringComparison.Ordinal);
        Assert.Contains("PlayerWorldAssets.FrameFor", sheet, StringComparison.Ordinal);
        Assert.Contains("PlayerSpritePose.IdleDown", sheet, StringComparison.Ordinal);
        Assert.Contains("PlayerWorldAssets.LayerIcon", sheet, StringComparison.Ordinal);
        Assert.Contains("InterpolationMode.NearestNeighbor", sheet, StringComparison.Ordinal);
        Assert.Contains("EquipmentService.ToOverlaySet", sheet, StringComparison.Ordinal);
        Assert.Contains("body → tunic → armor → head → hat → weapon", sheet, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never Graal", sheet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PacketId", sheet, StringComparison.Ordinal);
        Assert.DoesNotContain("ProtocolVersion", sheet, StringComparison.Ordinal);
        Assert.Contains("internal static Bitmap LayerIcon", assets, StringComparison.Ordinal);
        Assert.Contains("CropIdleSouth", assets, StringComparison.Ordinal);
    }

    [Fact]
    public void Shell_TogglesSheetFromKeyAndPerso_WithoutProtocolBump()
    {
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));
        var sheet = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Controls", "CharacterSheetPanel.cs"));
        var help = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "Forms", "HelpForm.cs"));
        var protocol = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Core", "Constants", "FrogWireProtocol.cs"));

        Assert.Contains("new(\"Fiche\")", shell, StringComparison.Ordinal);
        Assert.Contains("\"Fiche perso\"", shell, StringComparison.Ordinal);
        Assert.Contains("case HudMenuCommand.Character:", shell, StringComparison.Ordinal);
        Assert.Contains("ToggleCharacterSheet();", shell, StringComparison.Ordinal);
        Assert.Contains("e.KeyCode == Keys.C", shell, StringComparison.Ordinal);
        Assert.Contains("IsTextInputFocus(ActiveControl)", shell, StringComparison.Ordinal);
        Assert.Contains("_characterSheet.ApplyLoadout", shell, StringComparison.Ordinal);
        Assert.Contains("_hudStatus.ApplyPortrait(EquipmentService.ToOverlaySet(_paperdoll))", shell, StringComparison.Ordinal);
        Assert.Contains("_characterSheet.ToggleTunicRequested", shell, StringComparison.Ordinal);
        Assert.Contains("_characterSheet.ToggleHeadwearRequested", shell, StringComparison.Ordinal);
        Assert.Contains("_characterSheet.EquipRequested += slot => _ = EquipSlotAsync(slot);", shell, StringComparison.Ordinal);
        Assert.Contains("_characterSheet.UnequipRequested += slot => _ = UnequipSlotAsync(slot);", shell, StringComparison.Ordinal);
        Assert.Contains("_characterSheet.ApplyBag(snapshot, ResolveItemName, ResolveItemType);", shell, StringComparison.Ordinal);
        Assert.Contains("SendEquipAsync", shell, StringComparison.Ordinal);
        Assert.Contains("SendUnequipAsync", shell, StringComparison.Ordinal);
        Assert.Contains("CharacterSheetGear.FromSlotClick", sheet, StringComparison.Ordinal);
        Assert.Contains("CharacterSheetGear.FromBagEquip", sheet, StringComparison.Ordinal);
        Assert.Contains("event Action<byte>? EquipRequested", sheet, StringComparison.Ordinal);
        Assert.Contains("RequestToggleTunic", shell, StringComparison.Ordinal);
        Assert.Contains("tabRight.TabPages.Add(_tabGameplay);", shell, StringComparison.Ordinal);
        var gameplayTab = shell.IndexOf("tabRight.TabPages.Add(_tabGameplay);", StringComparison.Ordinal);
        var ficheTab = shell.IndexOf("tabRight.TabPages.Add(_tabCharacter);", gameplayTab, StringComparison.Ordinal);
        Assert.True(ficheTab > gameplayTab, "Fiche sits next to Inventaire");

        var key = shell.IndexOf("e.KeyCode == Keys.C", StringComparison.Ordinal);
        var connected = shell.IndexOf("_client is null || !_client.IsConnected", key, StringComparison.Ordinal);
        Assert.True(key >= 0 && connected > key, "C toggles the local sheet before any send");

        Assert.Contains("fiche perso", help, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Corps, Tunique, Armure, Tête, Casque, Arme", help, StringComparison.Ordinal);
        Assert.Contains("Version = 11", protocol, StringComparison.Ordinal);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.DoesNotContain("EquippedTunic", protocol, StringComparison.Ordinal);
        Assert.DoesNotContain("EquippedHat", protocol, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusDoc_RecordsFichePerso_OwnerNetsun()
    {
        var path = Path.Combine(RepoRoot(), "docs", "progress", "client-ui", "STATUS-fiche-perso.md");
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path);
        Assert.Contains("**Propriétaire** | Netsun", text, StringComparison.Ordinal);
        Assert.Contains("Fiche perso", text, StringComparison.Ordinal);
        Assert.Contains("Corps", text, StringComparison.Ordinal);
        Assert.Contains("Tunique", text, StringComparison.Ordinal);
        Assert.Contains("Armure", text, StringComparison.Ordinal);
        Assert.Contains("Tête", text, StringComparison.Ordinal);
        Assert.Contains("Casque", text, StringComparison.Ordinal);
        Assert.Contains("Arme", text, StringComparison.Ordinal);
        Assert.Contains("body → tunic → armor → head → hat → weapon", text, StringComparison.Ordinal);
        Assert.Contains("v11", text, StringComparison.Ordinal);
        Assert.Contains("EquipRequest", text, StringComparison.Ordinal);
        Assert.Contains("UnequipRequest", text, StringComparison.Ordinal);
        Assert.Contains("Eldiran", text, StringComparison.Ordinal);
        Assert.Contains("Keys.C", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Marc", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Graal sheet", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("public beta", text, StringComparison.OrdinalIgnoreCase);
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
