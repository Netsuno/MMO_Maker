using System;
using System.Collections.Generic;
using System.IO;
using Frog.Core.Chat;
using Frog.Core.Constants;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Bulles d'expression client. Le fil reste le chat carte. Hello reste 11.
/// </summary>
public sealed class ExpressionEmoteTests
{
    [Fact]
    public void Protocol_Stays11_BubblesReuseChatAndNameplateChrome()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        var root = RepoRoot();
        var protocol = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "FrogWireProtocol.cs"));
        Assert.Contains("Version = 11", protocol, StringComparison.Ordinal);
        Assert.DoesNotContain("Version = 12", protocol, StringComparison.Ordinal);

        var packets = File.ReadAllText(Path.Combine(root, "Frog.Core", "Enums", "PacketId.cs"));
        Assert.DoesNotContain("Emote", packets, StringComparison.Ordinal);
        Assert.DoesNotContain("Expression", packets, StringComparison.Ordinal);

        var catalog = File.ReadAllText(Path.Combine(root, "Frog.Core", "Chat", "ExpressionEmote.cs"));
        Assert.DoesNotContain("PacketId", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("Version = 12", catalog, StringComparison.Ordinal);

        var painter = File.ReadAllText(Path.Combine(root, "Frog.Client", "UI", "ExpressionBubblePainter.cs"));
        Assert.Contains("MessageBoxFont", painter, StringComparison.Ordinal);
        Assert.Contains("GraphicsUnit.Pixel", painter, StringComparison.Ordinal);
        Assert.Contains("UiTheme.BgPanel", painter, StringComparison.Ordinal);
        Assert.Contains("UiTheme.TextPrimary", painter, StringComparison.Ordinal);
        Assert.Contains("NameplatePainter.FontPixels", painter, StringComparison.Ordinal);
        Assert.DoesNotContain("AccentGold", painter, StringComparison.Ordinal);
        Assert.DoesNotContain("TextGold", painter, StringComparison.Ordinal);
        Assert.DoesNotContain("PacketId", painter, StringComparison.Ordinal);

        var renderer = File.ReadAllText(Path.Combine(root, "Frog.Client", "UI", "MapViewRenderer.cs"));
        Assert.Contains("ExpressionBubblePainter.DrawAbove", renderer, StringComparison.Ordinal);
        Assert.DoesNotContain("FrogWireProtocol.Version = 12", renderer, StringComparison.Ordinal);
        Assert.DoesNotContain("PacketId.Emote", renderer, StringComparison.Ordinal);

        var shell = File.ReadAllText(Path.Combine(root, "Frog.Client", "MainShellForm.cs"));
        Assert.Contains("ExpressionEmote.ParseSlash", shell, StringComparison.Ordinal);
        Assert.Contains("ChatChannel.Map", shell, StringComparison.Ordinal);
        Assert.Contains("emote.Wire", shell, StringComparison.Ordinal);
        Assert.Contains("ExpressionEmote.TryParseWire", shell, StringComparison.Ordinal);
        Assert.Contains("_expressionBubbles.Show", shell, StringComparison.Ordinal);
        Assert.Contains("_expressionBubbles.Advance", shell, StringComparison.Ordinal);
        Assert.Contains("_expressionBubbles.Clear", shell, StringComparison.Ordinal);
        Assert.Contains("_expressionBubbles.Remove", shell, StringComparison.Ordinal);
        Assert.Contains("expressionBubbles:", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("PacketId.Emote", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void Slash_ResolvesKnownAliases_AndRejectsPlainChat()
    {
        Assert.Equal(ExpressionEmote.Slash.None, ExpressionEmote.ParseSlash("bonjour", out _));
        Assert.Equal(ExpressionEmote.Slash.None, ExpressionEmote.ParseSlash("/equip epee", out _));
        Assert.Equal(ExpressionEmote.Slash.Incomplete, ExpressionEmote.ParseSlash("/e", out _));
        Assert.Equal(ExpressionEmote.Slash.Incomplete, ExpressionEmote.ParseSlash("/emote", out _));
        Assert.Equal(ExpressionEmote.Slash.Unknown, ExpressionEmote.ParseSlash("/e danse", out _));

        Assert.Equal(ExpressionEmote.Slash.Ready, ExpressionEmote.ParseSlash("/e sourire", out var smile));
        Assert.Equal("smile", smile.Id);
        Assert.Equal(":-)", smile.Glyph);
        Assert.Equal("*sourit*", smile.Wire);

        Assert.Equal(ExpressionEmote.Slash.Ready, ExpressionEmote.ParseSlash("/E Salut", out var wave));
        Assert.Equal("wave", wave.Id);

        Assert.Equal(ExpressionEmote.Slash.Ready, ExpressionEmote.ParseSlash("/expression cœur", out var heart));
        Assert.Equal("heart", heart.Id);
        Assert.Equal("\u2665", heart.Glyph);

        Assert.Equal(ExpressionEmote.Slash.Ready, ExpressionEmote.ParseSlash("/emo rire encore", out var laugh));
        Assert.Equal("laugh", laugh.Id);

        var wires = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var emote in ExpressionEmote.All)
        {
            Assert.True(wires.Add(emote.Wire));
            Assert.True(ids.Add(emote.Id));
            Assert.False(string.IsNullOrEmpty(emote.Glyph));
            Assert.Equal(ExpressionEmote.Slash.Ready, ExpressionEmote.ParseSlash("/e " + emote.Id, out var parsed));
            Assert.Equal(emote.Id, parsed.Id);
            Assert.True(ExpressionEmote.TryParseWire(emote.Wire, out var fromWire));
            Assert.Equal(emote.Id, fromWire.Id);
            Assert.True(ExpressionEmote.TryParseWire("  " + emote.Wire.ToUpperInvariant() + "  ", out var folded));
            Assert.Equal(emote.Id, folded.Id);
        }

        Assert.False(ExpressionEmote.TryParseWire("bonjour", out _));
        Assert.False(ExpressionEmote.TryParseWire("*sourit* !", out _));
        Assert.Equal(10, ExpressionEmote.All.Count);
    }

    [Fact]
    public void Opacity_HoldsThenFadesOut()
    {
        Assert.Equal(1f, ExpressionEmote.Opacity(0));
        Assert.Equal(1f, ExpressionEmote.Opacity(ExpressionEmote.HoldMs));
        Assert.Equal(0f, ExpressionEmote.Opacity(ExpressionEmote.HoldMs + ExpressionEmote.FadeMs));
        var mid = ExpressionEmote.Opacity(ExpressionEmote.HoldMs + (ExpressionEmote.FadeMs / 2));
        Assert.InRange(mid, 0.45f, 0.55f);
    }

    [Fact]
    public void Board_ShowsLocalAndRemote_ThenFadesAndReplaces()
    {
        var board = new ExpressionBubbleBoard();
        var t0 = new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(ExpressionEmote.Slash.Ready, ExpressionEmote.ParseSlash("/e sourire", out var smile));
        Assert.Equal(ExpressionEmote.Slash.Ready, ExpressionEmote.ParseSlash("/e salut", out var wave));
        Assert.Equal(ExpressionEmote.Slash.Ready, ExpressionEmote.ParseSlash("/e rire", out var laugh));

        board.Show("Ada", smile, t0);
        board.Show("Bob", wave, t0);
        Assert.False(board.Advance(t0));
        Assert.True(board.Advance(t0.AddMilliseconds(ExpressionEmote.HoldMs + (ExpressionEmote.FadeMs / 2))));

        var visible = new List<ExpressionBubbleBoard.Visible>();
        board.CopyVisible(t0, visible);
        Assert.Equal(2, visible.Count);
        Assert.Contains(visible, item => item.Username == "Ada" && item.Glyph == ":-)" && item.Opacity == 1f);
        Assert.Contains(visible, item => item.Username == "Bob" && item.Glyph == "Salut" && item.Opacity == 1f);

        board.Show("ada", laugh, t0.AddMilliseconds(ExpressionEmote.HoldMs));
        board.CopyVisible(t0.AddMilliseconds(ExpressionEmote.HoldMs), visible);
        var ada = Assert.Single(visible, item => item.Username.Equals("ada", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(":D", ada.Glyph);
        Assert.Equal(1f, ada.Opacity);

        var end = t0.AddMilliseconds(ExpressionEmote.HoldMs + ExpressionEmote.HoldMs + ExpressionEmote.FadeMs);
        Assert.True(board.Advance(end));
        board.CopyVisible(end, visible);
        Assert.DoesNotContain(visible, item => item.Username.Equals("ada", StringComparison.OrdinalIgnoreCase));
        Assert.False(board.Advance(end.AddMilliseconds(16)));

        board.Show("Bob", wave, end);
        board.Remove("bob");
        board.CopyVisible(end, visible);
        Assert.Empty(visible);

        board.Show("Clea", smile, end);
        board.Clear();
        Assert.Equal(0, board.Count);
        Assert.False(board.Advance(end));
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
