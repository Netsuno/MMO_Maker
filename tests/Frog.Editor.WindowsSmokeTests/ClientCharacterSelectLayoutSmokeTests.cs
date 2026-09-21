using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Frog.Client;
using Frog.Client.Config;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>
/// Production clip : « Entrer dans le jeu » / « Créer perso » hors carte 520 px.
/// Échoue si un CTA sort de la zone cliente ou d'un ancêtre clip.
/// </summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class ClientCharacterSelectLayoutSmokeTests
{
    [Fact]
    public void CharacterSelect_ActionButtons_StayInsideClientArea_AtTypicalSizes()
    {
        StaTestRunner.Run(() =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "frog-char-select-layout-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "client-settings.json");
            var previous = Environment.GetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable);
            Environment.SetEnvironmentVariable(ClientSettingsStore.PathEnvironmentVariable, path);
            MainShellForm? form = null;
            try
            {
                form = ClientSmokeTestAccess.CreateAndShowMainShell();
                form.WindowState = FormWindowState.Normal;
                form.ShowCharacterSelectForTest();

                Assert.Equal("Entrer dans le jeu", form.EnterGameButtonForTest.Text);
                Assert.Equal("Créer perso", form.CharCreateButtonForTest.Text);
                Assert.Equal("Liste persos", form.CharRefreshButtonForTest.Text);
                Assert.Equal("Retour à la connexion (fermer la session)", form.BackDisconnectButtonForTest.Text);

                foreach (var client in new[]
                         {
                             new Size(1280, 720),
                             new Size(1280, 800),
                             new Size(1024, 720),
                         })
                {
                    form.ClientSize = client;
                    FlushLayout(form);
                    AssertActionButtonsFullyVisible(form, $"ClientSize {client.Width}×{client.Height}");
                }

                form.Size = new Size(1280, 720);
                FlushLayout(form);
                AssertActionButtonsFullyVisible(form, "Window Size 1280×720");
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

    private static void FlushLayout(MainShellForm form)
    {
        form.PerformLayout();
        form.CharacterPanelForTest.PerformLayout();
        foreach (Control child in form.CharacterPanelForTest.Controls)
        {
            child.PerformLayout();
        }

        System.Windows.Forms.Application.DoEvents();
    }

    private static void AssertActionButtonsFullyVisible(MainShellForm form, string sizeLabel)
    {
        Assert.True(form.CharacterPanelForTest.Visible, $"{sizeLabel}: character panel hidden");
        AssertFullyVisibleInAncestors(form.EnterGameButtonForTest, form, sizeLabel);
        AssertFullyVisibleInAncestors(form.CharCreateButtonForTest, form, sizeLabel);
        AssertFullyVisibleInAncestors(form.CharRefreshButtonForTest, form, sizeLabel);
        AssertFullyVisibleInAncestors(form.BackDisconnectButtonForTest, form, sizeLabel);
    }

    private static void AssertFullyVisibleInAncestors(Control control, Control root, string sizeLabel)
    {
        Assert.True(control.Visible, $"{sizeLabel}: « {control.Text} » Visible=false");
        Assert.True(
            control.Width > 8 && control.Height > 8,
            $"{sizeLabel}: « {control.Text} » has no size ({control.Width}×{control.Height})");

        var bounds = control.RectangleToScreen(new Rectangle(Point.Empty, control.Size));
        var rootClient = root.RectangleToScreen(root.ClientRectangle);
        Assert.True(
            rootClient.Contains(bounds),
            $"{sizeLabel}: « {control.Text} » {bounds} outside form client {rootClient}");

        for (var parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            var clip = parent.RectangleToScreen(parent.ClientRectangle);
            Assert.True(
                clip.Contains(bounds),
                $"{sizeLabel}: « {control.Text} » {bounds} clipped by {parent.GetType().Name} {clip}");
            if (ReferenceEquals(parent, root))
            {
                break;
            }
        }
    }
}
