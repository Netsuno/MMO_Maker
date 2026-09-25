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
        • Choisissez un serveur dans la liste. F9 saisit l'hôte, le port et un nom, puis « Ajouter ».
        • « Connecter » établit le lien, puis « Connexion » ou « Inscription ».
        • « Réessayer » relance la même adresse, ou renvoie le compte si le lien est déjà ouvert.
        • Délai dépassé, serveur injoignable, version incompatible, mauvais identifiants ou certificat invalide : le message est en français. Le bandeau indique la cause, l'adresse et le protocole.
        • Reconnecter reprend une session déjà ouverte (le jeton n'est jamais affiché).

        Déplacement
        • Clavier AZERTY : ZQSD. Clavier QWERTY : WASD. Les flèches directionnelles fonctionnent toujours.
        • E : interagir avec un PNJ, un objet ou un événement proche.
        • Espace : attaque de mêlée.
        • Les touches se règlent dans Options (déplacement, interagir, mêlée) et sont conservées d'une session à l'autre.
        • La saisie dans un champ texte (chat, mot de passe) n'envoie pas de déplacement.

        Combat, inventaire, boutique
        • C ou le bouton Perso ouvre la fiche perso (aperçu paperdoll et emplacements Corps, Tunique, Armure, Tête, Casque, Arme).
        • Création : Corps, Cheveux et Tunique se choisissent à côté de l'aperçu (clic ou flèches). Le choix reste sur cet ordinateur.
        • En jeu, un clic sur Corps ou Tête dans la fiche change le style. La tunique locale se porte ou se retire comme avant.
        • Depuis la fiche, Équiper envoie l'objet du sac au serveur (arme ou armure). Un clic sur l'arme ou l'armure portée la range. Tunique et casque restent un aperçu local.
        • Vie, or, inventaire, équipement, banque, quêtes et fabrication sont des panneaux nommés.
        • Les identifiants techniques (Guid) ne font pas partie de l'interface normale.
        • Mêlée (Espace ou 1) : coup au contact. Une touche affiche le nombre et des étincelles.
        • Distance (touche 4) : même coup, jusqu'à 3 tuiles. Le trait jaune précède les étincelles.
        • Sorts : choisissez une cible par son nom.
        • En cas de mort, le bouton Respawn apparaît.

        Boutique et banque
        • « Ouvrir la boutique » liste le nom, le prix et le stock. « Fermer la boutique » quitte la fenêtre.
        • Acheter, vendre, déposer et retirer demandent un second clic (Confirmer).
        • E sur un événement « shop:identifiant », ou à côté d'un PNJ de test lié, ouvre ou ferme cette boutique.
        • Or insuffisant, stock épuisé, inventaire ou banque pleine s'affiche en français.
        • Si la connexion tombe pendant l'envoi, l'action est interrompue. Vérifiez l'or après reconnexion.

        Échange entre joueurs
        • À portée (3 tuiles) : /trade NomDuJoueur, ou /trade avec le nom déjà saisi au-dessus du chat.
        • /trade invite <identifiant> reste possible. La fenêtre propose Accepter ou Refuser.
        • Huit emplacements et l'or. « Proposer » envoie l'offre. Confirmer et annuler un échange non vide demandent un second clic.
        • Trop loin, déconnexion ou inventaire plein s'affiche en français. Rien n'est transféré tant que les deux n'ont pas confirmé la même révision.

        Fabrication
        • Onglet Quêtes : choisissez une recette par son nom, puis « Fabriquer ».
        • Un métier peut être requis selon la recette.

        Chat
        • Entrée ouvre la saisie. Tant que vous écrivez, les touches de déplacement ne bougent pas le personnage.
        • Échap ou un clic sur le monde rend la main au déplacement.
        • Canaux Général, Local, Chuchoter, Groupe et Guilde.
        • Chuchoter : nom dans le champ, ami sélectionné, ou cible. /w Nom message fonctionne aussi.
        • La ligne part dans le même chat. Hors ligne, inconnu ou bloqué s'affiche en français.

        Amis, Groupe, Guilde
        • Boutons Amis / Groupe / Guilde dans le dock chat ouvrent les panneaux (même chrome que Inventaire).
        • Les listes viennent des paquets sociaux existants (80–83). Slash /friend /party /guild restent valides.
        • Ouvrir Amis épingle la liste à gauche. Elle reste pendant le déplacement et les clics sur la carte. « Détacher » la laisse se fermer au clic ou à Échap. « Épingler » la recolle. « Fermer » la range.
        • Un clic sur un ami remplit le nom du chuchotement. Entrée envoie seulement si la saisie est déjà ouverte. Échap rend la main au monde.

        Aide, options, diagnostics
        • F1 ou le bouton Aide ouvre cette fenêtre.
        • F8 (en jeu) : cycle météo debug Auto → Clair → Pluie → Brouillard (teinte overlay, pas de bump protocole).
        • Options : volume, muet, musique (boucle de test), disposition clavier, rebind, taille de fenêtre, plein écran, échelle d'interface (75–200 %).
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
