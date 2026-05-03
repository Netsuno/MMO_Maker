Carte monde serveur (.fmap)
===========================

1. Exportez une carte depuis Frog.Editor au format .fmap (blob identique a MapData / MapSerializer ; voir MapSerializer.MapFileFormatVersion pour l'octet version apres magic FMAP).

2. Copiez le fichier dans ce dossier (ou ailleurs sur le disque).

3. Dans Frog.Server/appsettings.json, section "Maps", renseignez "worldMapPath" :
   - chemin absolu, ou
   - chemin relatif au repertoire de l'executable Frog.Server (ex. "Maps\\monde.fmap").

4. Redemarrez le serveur. Si worldMapPath est vide ou le fichier est introuvable, une carte de secours integree est utilisee.

Les tuiles Block et Warp du fichier sont prises en compte pour collisions et teleports (meme carte).
