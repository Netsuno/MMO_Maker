using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Frog.Client;
using Frog.Client.Config;
using Frog.Client.UI;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>Barre de sorts : cases 5–0 liées aux compétences publiées, cases 1–4 inchangées.</summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class SkillHotbarSmokeTests
{
    private static readonly Guid EntailleId = Guid.Parse("bbbbbbbb-0002-4000-8000-0000000000e1");
    private static readonly Guid FrappeId = Guid.Parse("bbbbbbbb-0002-4000-8000-0000000000f2");

    [Fact]
    public void Hotbar_BindsPublishedSkills_AndKeepsMeleeSlot()
    {
        StaTestRunner.Run(() =>
        {
            Assert.Equal((ushort)11, FrogWireProtocol.Version);
            var dir = Path.Combine(Path.GetTempPath(), "frog-skillbar-ui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "client-settings.json");
            var previous = Environment.GetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable);
            Environment.SetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable, path);
            MainShellForm? form = null;
            try
            {
                form = ClientSmokeTestAccess.CreateAndShowMainShell();
                form.LayoutGameHudForTest();
                Assert.Equal("Barre de sorts", form.HotbarForTest.AccessibleName);
                Assert.Contains("Mêlée", form.HotbarForTest.SlotToolTipForTest(0), StringComparison.Ordinal);
                Assert.False(form.HotbarForTest.SlotEnabledForTest(4));
                Assert.False(form.HotbarForTest.SlotEnabledForTest(9));
                Assert.Equal("Slot 5 — non lié", form.HotbarForTest.SlotToolTipForTest(4));

                form.ApplyPublishedCatalogForTest(Catalog());

                Assert.Contains("Mêlée", form.HotbarForTest.SlotToolTipForTest(0), StringComparison.Ordinal);
                Assert.True(form.HotbarForTest.SlotEnabledForTest(0));
                Assert.False(form.HotbarForTest.SlotHasIconForTest(3));
                Assert.True(form.HotbarForTest.SlotEnabledForTest(4), "case 5 liée");
                Assert.Contains("Compétence (5)", form.HotbarForTest.SlotToolTipForTest(4), StringComparison.Ordinal);
                Assert.Contains("Entaille", form.HotbarForTest.SlotToolTipForTest(4), StringComparison.Ordinal);
                Assert.Contains("Utilisateur", form.HotbarForTest.SlotToolTipForTest(4), StringComparison.Ordinal);
                Assert.True(form.HotbarForTest.SlotHasIconForTest(4));
                Assert.Contains("Frappe", form.HotbarForTest.SlotToolTipForTest(5), StringComparison.Ordinal);
                Assert.Contains("Un ennemi", form.HotbarForTest.SlotToolTipForTest(5), StringComparison.Ordinal);
                Assert.Equal("Case 0 — non liée", form.HotbarForTest.SlotToolTipForTest(9));
                Assert.True(form.HotbarForTest.SlotEnabledForTest(9));
                Assert.Equal(UiTheme.TextPrimary, form.HotbarForTest.SlotForeColorForTest(4));
                Assert.Equal(UiTheme.AccentGold, form.HotbarForTest.SlotBorderColorForTest(4));

                var spells = form.SpellComboForTest.Items.Cast<object>().Select(item => item.ToString()).ToArray();
                Assert.Contains("Boule de feu", spells);
                Assert.DoesNotContain("Entaille", spells);
                Assert.DoesNotContain("Frappe", spells);

                var menu = form.SkillHotbarMenuLabelsForTest(4);
                Assert.Contains("Retirer la compétence", menu);
                Assert.Contains("Entaille", menu);
                Assert.Contains("Frappe", menu);

                form.ActivateHotbarSlotForTest(4);
                Assert.Contains("Compétence : hors ligne.", form.LogTextForTest, StringComparison.Ordinal);

                Assert.True(form.TryBindSkillHotbarForTest(4, FrappeId));
                Assert.Contains("Frappe", form.HotbarForTest.SlotToolTipForTest(4), StringComparison.Ordinal);
                Assert.Equal("Case 6 — non liée", form.HotbarForTest.SlotToolTipForTest(5));
                Assert.Contains("Compétence liée : Frappe (case 5).", form.LogTextForTest, StringComparison.Ordinal);

                form.MeleeTargetComboForTest.Text = string.Empty;
                form.ActivateHotbarSlotForTest(4);
                Assert.Contains("Compétence : choisissez une cible.", form.LogTextForTest, StringComparison.Ordinal);

                var saved = File.ReadAllText(path);
                Assert.Contains(FrappeId.ToString("D"), saved, StringComparison.Ordinal);
                Assert.Contains("\"skillHotbarBindings\"", saved, StringComparison.Ordinal);
            }
            finally
            {
                if (form is not null)
                {
                    ClientSmokeTestAccess.CloseMainShell(form);
                }

                Environment.SetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable, previous);
                try
                {
                    Directory.Delete(dir, recursive: true);
                }
                catch
                {
                    // ignore
                }
            }
        });
    }

    private static PublishedCatalogWire Catalog() => new()
    {
        Spells = new[]
        {
            new PublishedSpellWireEntry
            {
                Id = Guid.Parse("aaaaaaaa-0002-4000-8000-000000000001").ToString("D"),
                Name = "Boule de feu",
                MpCost = 8,
                Kind = nameof(SpellKind.Spell),
                CooldownMs = 1000,
                TargetType = nameof(TargetType.SingleEnemy),
            },
            new PublishedSpellWireEntry
            {
                Id = FrappeId.ToString("D"),
                Name = "Frappe",
                MpCost = 0,
                Kind = nameof(SpellKind.Skill),
                CooldownMs = 400,
                TargetType = nameof(TargetType.SingleEnemy),
            },
            new PublishedSpellWireEntry
            {
                Id = EntailleId.ToString("D"),
                Name = "Entaille",
                MpCost = 12,
                Kind = nameof(SpellKind.Skill),
                CooldownMs = 800,
                TargetType = nameof(TargetType.Self),
            },
        },
    };
}
