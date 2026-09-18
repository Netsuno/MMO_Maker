using System.Windows.Forms;

namespace Frog.Client.Forms;

/// <summary>Aide intégrée (FR, défilable) — F1 ou bouton Aide.</summary>
public sealed class HelpForm : Form
{
    internal const string HelpBody =
        """
        FRoG — Aide

        Connexion
        • Saisissez l'hôte et le port du serveur, puis votre compte et votre mot de passe.
        • « Connecter » établit le lien, puis « Login » ou « Inscription ».
        • Serveur hors ligne, version incompatible, mauvais identifiants, sanction ou certificat invalide : un message l'explique en français.
        • Reconnecter reprend une session déjà ouverte (le jeton n'est jamais affiché).

        Déplacement
        • Clavier AZERTY : ZQSD. Clavier QWERTY : WASD. Les flèches directionnelles fonctionnent toujours.
        • E : interagir avec un PNJ, un objet ou un événement proche.
        • Les touches se règlent dans Options et sont conservées d'une session à l'autre.
        • La saisie dans un champ texte (chat, mot de passe) n'envoie pas de déplacement.

        Combat, inventaire, boutique
        • Vie, or, inventaire, équipement, banque, quêtes et fabrication sont des panneaux nommés.
        • Les identifiants techniques (Guid) ne font pas partie de l'interface normale.
        • Mêlée et sorts : choisissez une cible par son nom.
        • En cas de mort, le bouton Respawn apparaît.

        Fabrication
        • Onglet Quêtes : choisissez une recette par son nom, puis « Fabriquer ».
        • Un métier peut être requis selon la recette.

        Chat
        • Canaux Map, Global, Whisper, Party et Guild.
        • Whisper demande le nom du destinataire.

        Aide, options, diagnostics
        • F1 ou le bouton Aide ouvre cette fenêtre.
        • Options : volume, disposition clavier, rebind, taille de fenêtre, plein écran.
        • Le numéro de version est visible en permanence.
        • « Copier diagnostics » prépare un rapport sans mot de passe ni jeton, pour signaler un problème.
        """;

    public HelpForm()
    {
        Text = "Aide — FRoG";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(520, 420);
        Size = new Size(640, 520);
        ShowInTaskbar = false;
        MinimizeBox = false;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Font;

        var text = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Text = HelpBody.Replace("\n", Environment.NewLine, StringComparison.Ordinal),
            Font = new Font(SystemFonts.MessageBoxFont?.FontFamily ?? FontFamily.GenericSansSerif, 10f),
            BorderStyle = BorderStyle.FixedSingle,
            TabStop = true,
        };

        var close = new Button
        {
            Text = "Fermer",
            AutoSize = true,
            DialogResult = DialogResult.OK,
            Anchor = AnchorStyles.Right,
        };
        AcceptButton = close;
        CancelButton = close;

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(8),
        };
        bottom.Controls.Add(close);

        Controls.Add(text);
        Controls.Add(bottom);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Escape or Keys.F1)
            {
                e.Handled = true;
                Close();
            }
        };

        Load += (_, _) =>
        {
            text.SelectionStart = 0;
            text.SelectionLength = 0;
            text.ScrollToCaret();
        };
    }

    internal string HelpTextForTest => HelpBody;
}
