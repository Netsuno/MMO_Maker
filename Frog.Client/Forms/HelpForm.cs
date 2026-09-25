using System.Windows.Forms;
using Frog.Client.UI;

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
        • C ou le bouton Perso ouvre la fiche perso (aperçu paperdoll et emplacements Corps, Tunique, Armure, Tête, Casque, Arme).
        • Depuis la fiche, Équiper envoie l'objet du sac au serveur (arme ou armure). Un clic sur l'arme ou l'armure portée la range. Tunique et casque restent un aperçu local.
        • Vie, or, inventaire, équipement, banque, quêtes et fabrication sont des panneaux nommés.
        • Les identifiants techniques (Guid) ne font pas partie de l'interface normale.
        • Mêlée et sorts : choisissez une cible par son nom.
        • En cas de mort, le bouton Respawn apparaît.

        Échange entre joueurs
        • À portée (3 tuiles) : /trade invite <identifiant personnage>, puis accepter.
        • La fenêtre d'échange affiche les noms, objets, quantités, or et la révision.
        • Confirmer uniquement la révision affichée ; modifier l'offre annule les confirmations.

        Fabrication
        • Onglet Quêtes : choisissez une recette par son nom, puis « Fabriquer ».
        • Un métier peut être requis selon la recette.

        Chat
        • Canaux Map, Global, Whisper, Party et Guild.
        • Whisper demande le nom du destinataire.

        Amis, Groupe, Guilde
        • Boutons Amis / Groupe / Guilde dans le dock chat ouvrent les panneaux (même chrome que Inventaire).
        • Les listes viennent des paquets sociaux existants (80–83). Slash /friend /party /guild restent valides.

        Aide, options, diagnostics
        • F1 ou le bouton Aide ouvre cette fenêtre.
        • F8 (en jeu) : cycle météo debug Auto → Clair → Pluie → Brouillard (teinte overlay, pas de bump protocole).
        • Options : volume, muet, musique (boucle de test), disposition clavier, rebind, taille de fenêtre, plein écran.
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
        UiTheme.Apply(this);

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
