using System.Drawing;
using System.Windows.Forms;
using Frog.Editor;
using Frog.Editor.Forms;
using Frog.Editor.Forms.GameData;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>Le MenuStrip classique ouvre le GameDataForm existant (même hook smoke que la commande WPF).</summary>
[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class MainFormGameDataMenuSmokeTests
{
    [Fact]
    public void MainForm_RessourcesMenu_DonneesDeJeu_OpensGameDataForm()
    {
        StaTestRunner.Run(() =>
        {
            GameDataSmokeTestHelper.ConfigureInMemory();
            MainForm? main = null;
            GameDataForm? gameData = null;
            try
            {
                main = new MainForm(embedAsWpfChild: false)
                {
                    WindowState = FormWindowState.Normal,
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    Bounds = new Rectangle(40, 40, 1280, 800),
                };
                _ = main.Handle;

                gameData = GameDataSmokeUiDriver.OpenViaMainFormMenu(
                    main,
                    EditorSmokeTestAccess.DefaultTimeout);
                GameDataSmokeUiDriver.AssertInitialTilesetCategory(gameData);
                GameDataSmokeUiDriver.CloseForm(gameData, EditorSmokeTestAccess.DefaultTimeout);
                gameData = null;
            }
            finally
            {
                if (gameData is { IsDisposed: false })
                {
                    gameData.Dispose();
                }

                if (main is { IsDisposed: false })
                {
                    main.Close();
                    StaTestRunner.PumpUntil(
                        () => main.IsDisposed,
                        EditorSmokeTestAccess.DefaultTimeout);
                }

                EditorSmokeTestAccess.ResetHooks();
            }
        });
    }
}
