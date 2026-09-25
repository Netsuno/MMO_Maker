using Frog.Core.Enums;

namespace Frog.Core.Social;

/// <summary>
/// Liste Amis collée au HUD pendant le jeu. Le roster reste celui des paquets 80–83 :
/// pas de nouvel opcode. Le clic prépare le chuchotement déjà livré sans prendre le focus du chat.
/// </summary>
public sealed class FriendsSticky
{
    public const string Title = "Amis";
    public const string PinText = "Épingler";
    public const string UnpinText = "Détacher";
    public const string CloseText = "Fermer";
    public const string EmptyText = "Aucun ami.";

    public bool Visible { get; private set; }

    public bool Pinned { get; private set; }

    public string PinLabel => Pinned ? UnpinText : PinText;

    public string TitleText => Pinned ? "Amis · épinglé" : Title;

    public void Show() => Visible = true;

    public void Pin()
    {
        Visible = true;
        Pinned = true;
    }

    public void Unpin() => Pinned = false;

    public void TogglePin()
    {
        if (Pinned)
        {
            Unpin();
            return;
        }

        Pin();
    }

    public void Close()
    {
        Visible = false;
        Pinned = false;
    }

    /// <summary>
    /// Clic carte ou Échap hors saisie. La liste épinglée reste visible pendant le déplacement.
    /// </summary>
    public bool DismissIfUnpinned()
    {
        if (!Visible || Pinned)
        {
            return false;
        }

        Visible = false;
        return true;
    }

    public readonly record struct Row(
        Guid CharacterId,
        string DisplayName,
        bool Online,
        bool CanWhisper,
        string Text);

    public readonly record struct View(
        IReadOnlyList<Row> Rows,
        string EmptyText,
        bool ShowEmpty,
        string Status);

    /// <summary>
    /// Remplit le champ nom du chuchotement. <see cref="WhisperArm.FocusChatInput"/> reste faux :
    /// Entrée / Échap du chat ne sont pas pris par la liste.
    /// </summary>
    public readonly record struct WhisperArm(bool FillName, string Name, bool FocusChatInput, string Notice);

    public static View Build(ClientSocialRoster roster)
    {
        ArgumentNullException.ThrowIfNull(roster);
        var rows = new List<Row>();
        var pending = 0;
        foreach (var item in roster.BuildRows(SocialKind.Friend))
        {
            if (item.IsPendingInvite || item.Role == ClientSocialRoster.FriendRoleIncoming)
            {
                pending++;
            }

            if (item.IsPendingInvite || item.Role != ClientSocialRoster.FriendRoleAccepted)
            {
                continue;
            }

            var name = item.DisplayName.Trim();
            var canWhisper = name.Length > 0;
            var presence = item.Online ? "en ligne" : "hors ligne";
            var label = canWhisper ? name : "Ami";
            rows.Add(new Row(item.CharacterId, name, item.Online, canWhisper, label + " — " + presence));
        }

        rows.Sort(static (a, b) =>
        {
            var byOnline = b.Online.CompareTo(a.Online);
            if (byOnline != 0)
            {
                return byOnline;
            }

            return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
        });

        var showEmpty = rows.Count == 0;
        return new View(rows, showEmpty ? EmptyText : string.Empty, showEmpty, StatusText(rows, pending));
    }

    public static WhisperArm Arm(Row row)
    {
        if (!row.CanWhisper || string.IsNullOrWhiteSpace(row.DisplayName))
        {
            return new WhisperArm(false, string.Empty, false, "Sélectionnez un ami pour chuchoter.");
        }

        var name = row.DisplayName.Trim();
        var notice = row.Online ? "Chuchoter à " + name + "." : name + " est hors ligne.";
        return new WhisperArm(true, name, false, notice);
    }

    private static string StatusText(List<Row> rows, int pending)
    {
        var presence = string.Empty;
        if (rows.Count > 0)
        {
            var online = 0;
            foreach (var row in rows)
            {
                if (row.Online)
                {
                    online++;
                }
            }

            presence = online == 0
                ? "Aucun ami en ligne."
                : online == 1 ? "1 en ligne." : online + " en ligne.";
        }

        var wait = pending switch
        {
            0 => string.Empty,
            1 => "1 demande en attente.",
            _ => pending + " demandes en attente.",
        };

        if (presence.Length == 0)
        {
            return wait;
        }

        if (wait.Length == 0)
        {
            return presence;
        }

        return presence + " " + wait;
    }
}
