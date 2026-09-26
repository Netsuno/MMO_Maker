using System;
using System.IO;
using Frog.Client.UI;
using Frog.Core.Character;
using Frog.Core.Constants;
using Xunit;

namespace Frog.Tests;

public sealed class CharacterCreateUxTests
{
    [Theory]
    [InlineData("  Mage_2  ", true, true, "Mage_2", CharacterCreateUx.HintReady)]
    [InlineData("Ael", false, false, "Ael", CharacterCreateUx.HintNeedClass)]
    [InlineData("", false, false, "", CharacterCreateUx.HintRules)]
    [InlineData("   ", false, false, "", CharacterCreateUx.HintRules)]
    public void Describe_matches_shared_name_rules(string raw, bool classSelected, bool ok, string normalized, string message)
    {
        var feedback = CharacterCreateUx.Describe(raw, classSelected);
        Assert.Equal(ok, feedback.Ok);
        Assert.Equal(normalized, feedback.Normalized);
        Assert.Equal(message, feedback.Message);
        Assert.False(feedback.IsError);
        var nameValid = CharacterDisplayNameRules.TryNormalize(raw, out var serverName, out _);
        if (nameValid)
        {
            Assert.Equal(serverName, feedback.Normalized);
            Assert.Equal(classSelected, feedback.Ok);
        }
        else
        {
            Assert.False(feedback.Ok);
        }
    }

    [Fact]
    public void Describe_rejects_invalid_character_with_server_message()
    {
        Assert.False(CharacterDisplayNameRules.TryNormalize("bad@", out _, out var serverError));
        var feedback = CharacterCreateUx.Describe("bad@", classSelected: true);
        Assert.False(feedback.Ok);
        Assert.True(feedback.IsError);
        Assert.Equal(serverError, feedback.Message);
        Assert.Equal(serverError, CharacterCreateUx.BlockedCreateMessage("bad@", classSelected: true));
    }

    [Fact]
    public void Describe_rejects_too_long_name()
    {
        var raw = new string('a', CharacterDisplayNameRules.MaxLength + 1);
        var feedback = CharacterCreateUx.Describe(raw, classSelected: true);
        Assert.False(feedback.Ok);
        Assert.True(feedback.IsError);
        Assert.Contains("32", feedback.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CanCreate_requires_session_class_and_valid_name()
    {
        Assert.False(CharacterCreateUx.CanCreate("", classSelected: true, sessionOpen: true));
        Assert.False(CharacterCreateUx.CanCreate("Ael", classSelected: false, sessionOpen: true));
        Assert.False(CharacterCreateUx.CanCreate("Ael", classSelected: true, sessionOpen: false));
        Assert.False(CharacterCreateUx.CanCreate("bad@", classSelected: true, sessionOpen: true));
        Assert.True(CharacterCreateUx.CanCreate("  Ael  ", classSelected: true, sessionOpen: true));
    }

    [Fact]
    public void CanEnter_requires_session_and_selection()
    {
        Assert.False(CharacterCreateUx.CanEnter(sessionOpen: false, hasSelection: true));
        Assert.False(CharacterCreateUx.CanEnter(sessionOpen: true, hasSelection: false));
        Assert.True(CharacterCreateUx.CanEnter(sessionOpen: true, hasSelection: true));
    }

    [Fact]
    public void DecideEnter_creates_from_name_or_class_and_enters_from_roster()
    {
        Assert.Equal(
            CharacterCreateUx.EnterAction.Create,
            CharacterCreateUx.DecideEnter(true, false, false, false, false, false));
        Assert.Equal(
            CharacterCreateUx.EnterAction.Create,
            CharacterCreateUx.DecideEnter(false, true, false, false, false, false));
        Assert.Equal(
            CharacterCreateUx.EnterAction.EnterGame,
            CharacterCreateUx.DecideEnter(false, false, false, true, false, false));
        Assert.Equal(
            CharacterCreateUx.EnterAction.None,
            CharacterCreateUx.DecideEnter(false, true, true, false, false, false));
        Assert.Equal(
            CharacterCreateUx.EnterAction.None,
            CharacterCreateUx.DecideEnter(false, false, false, true, true, false));
        Assert.Equal(
            CharacterCreateUx.EnterAction.None,
            CharacterCreateUx.DecideEnter(true, false, false, false, false, true));
    }

    [Fact]
    public void HintFor_mentions_waiting_classes_until_the_catalog_arrives()
    {
        var waiting = CharacterCreateUx.HintFor("  ", classSelected: false, classesAvailable: false, sessionOpen: true);
        Assert.Contains(CharacterCreateUx.HintRules, waiting, StringComparison.Ordinal);
        Assert.Contains(CharacterCreateUx.HintNeedClass, waiting, StringComparison.Ordinal);
        Assert.Equal(
            CharacterCreateUx.HintRules,
            CharacterCreateUx.HintFor("", classSelected: false, classesAvailable: false, sessionOpen: false));
        Assert.Equal(
            CharacterCreateUx.HintReady,
            CharacterCreateUx.HintFor("Ael", classSelected: true, classesAvailable: true, sessionOpen: true));
    }

    [Fact]
    public void BlockedCreateMessage_prompts_for_a_name()
    {
        Assert.Equal(CharacterCreateUx.NeedNameStatus, CharacterCreateUx.BlockedCreateMessage("  ", classSelected: true));
        Assert.Equal(CharacterCreateUx.HintNeedClass, CharacterCreateUx.BlockedCreateMessage("Ael", classSelected: false));
    }

    [Fact]
    public void Shell_stacks_create_fields_and_keeps_ctas_on_their_rows()
    {
        var shell = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "MainShellForm.cs"));
        var login = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "LoginShell.cs"));
        Assert.Contains("new LoginShell.StackRow()", shell, StringComparison.Ordinal);
        Assert.Contains("FitCharacterCard", shell, StringComparison.Ordinal);
        Assert.Contains("TryHandleCharacterPageEnter", shell, StringComparison.Ordinal);
        Assert.Contains("SetCharacterSessionOpen", shell, StringComparison.Ordinal);
        Assert.Contains("RefreshCharacterActions", shell, StringComparison.Ordinal);
        Assert.Contains("rowEnter.Controls.Add(_btnEnterGame)", shell, StringComparison.Ordinal);
        Assert.Contains("rowCreateAction.Controls.Add(_btnCharCreate)", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("rowCreate.Controls.Add(_btnCharCreate)", shell, StringComparison.Ordinal);
        Assert.Contains("class StackRow", login, StringComparison.Ordinal);
        Assert.Contains("row.WrapContents = !stack", login, StringComparison.Ordinal);
        Assert.Contains("fitInnerWidth?.Invoke(scaledInner)", login, StringComparison.Ordinal);
    }

    [Fact]
    public void French_copy_and_protocol_11_stay_put()
    {
        var ux = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Client", "UI", "CharacterCreateUx.cs"));
        Assert.Contains("Choisir votre personnage", ux, StringComparison.Ordinal);
        Assert.Contains("Créer le personnage", ux, StringComparison.Ordinal);
        Assert.Contains("Entrer dans le jeu", ux, StringComparison.Ordinal);
        Assert.Contains("Liste persos", ux, StringComparison.Ordinal);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        var protocol = File.ReadAllText(Path.Combine(RepoRoot(), "Frog.Core", "Constants", "FrogWireProtocol.cs"));
        Assert.Contains("public const ushort Version = 11;", protocol, StringComparison.Ordinal);
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
