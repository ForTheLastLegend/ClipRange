# ClipRange

*[English version](README.md)*

Transforme un passage d'une vidéo YouTube en GIF depuis Chrome ou Brave, sans télécharger la vidéo entière.

Deux boutons apparaissent dans la barre du lecteur : `Début` et `Fin` capturent le temps courant,
`GIF` lance le traitement. Le fichier arrive dans le dossier Téléchargements et une notification Windows
propose de l'ouvrir. Trois presets de qualité, ou tes propres réglages, dans les options de l'extension.

Windows uniquement. Interface en anglais, ou en français si le navigateur l'est.

> Cet outil va à l'encontre des conditions d'utilisation de YouTube et ne peut pas être publié sur le
> Chrome Web Store. Il est partagé ici pour un usage personnel, à charger en mode développeur.
>
> Tu l'utilises à tes risques et sous ta propre responsabilité. Rien ici n'est conçu pour nuire, mais
> le logiciel est fourni sans aucune garantie, et l'auteur ne peut être tenu responsable de quoi que
> ce soit qui résulterait de son installation ou de son utilisation.

## Installation

1. Télécharger `ClipRange-Setup-x.y.z.exe` dans la [dernière release](../../releases/latest) et le lancer.
   Aucun droit administrateur nécessaire.
2. Dans Chrome ou Brave, ouvrir `chrome://extensions` (ou `brave://extensions`), activer le **Mode développeur**,
   cliquer **Charger l'extension non empaquetée** et choisir le dossier `extension` dans
   `%LOCALAPPDATA%\Programs\ClipRange`.
3. La page d'options de l'extension s'ouvre : cliquer **Télécharger** pour yt-dlp et pour ffmpeg.
   Si tu les as déjà, indique leur chemin à la place.

Ouvre ensuite n'importe quelle vidéo YouTube. Le navigateur affichera à chaque démarrage un avertissement
sur les extensions en mode développeur, c'est le prix d'une extension hors store.

yt-dlp doit être mis à jour régulièrement pour suivre YouTube : bouton **Mettre à jour** dans les options
(un clic sur l'icône ClipRange dans la barre d'outils les ouvre).

## Comment ça marche

```
extension/   Extension Chrome MV3 (content script, service worker, page d'options)
host/        Hôte Native Messaging en C# (.NET 9, NativeAOT) : yt-dlp puis ffmpeg
installer/   Script Inno Setup qui installe l'hôte, l'extension et la clé de registre
```

L'extension parle à l'hôte par `chrome.runtime.connectNative`. Le navigateur lance l'hôte à chaque
demande et il se termine quand le port se ferme. Pendant un traitement il envoie un message de progression
au moins toutes les 10 secondes pour que le navigateur ne décharge pas le service worker.

Pipeline : `yt-dlp --download-sections` ne récupère que l'intervalle demandé (coupe précise grâce à
`--force-keyframes-at-cuts`, résolution plafonnée selon la largeur du GIF), puis ffmpeg encode en une
passe avec `palettegen` et `paletteuse`. Les presets vivent dans `host/Messages.cs`.

Brave lit la même clé de registre que Chrome (`HKCU\Software\Google\Chrome\NativeMessagingHosts`).
Edge utilise `HKCU\Software\Microsoft\Edge\NativeMessagingHosts`, non gérée par l'installeur.

Réglages : `%LOCALAPPDATA%\ClipRange\settings.json`. Journal : `%LOCALAPPDATA%\ClipRange\host.log`
(commandes lancées et sortie d'erreur de yt-dlp et ffmpeg).

## Développement

Prérequis : .NET 9 SDK et les outils C++ de Visual Studio (NativeAOT). Compiler l'hôte depuis PowerShell
ou cmd, pas Git Bash (la chaîne AOT cherche `vswhere.exe` via `%ProgramFiles(x86)%`) :

```
dotnet publish host -c Release
```

Pour tester sans installeur : copier `host/com.cliprange.host.json` à côté de l'exécutable publié, puis
enregistrer ce manifeste dans le registre, avec ton propre chemin :

```
reg add "HKCU\Software\Google\Chrome\NativeMessagingHosts\com.cliprange.host" /ve /d "<chemin>\publish\com.cliprange.host.json" /f
```

Charger `extension/` en mode développeur. Le champ `key` du manifeste fixe l'identifiant de l'extension à
`jndjblelcmehihkifdeblpobkadfkfbn`, le seul autorisé par le manifeste natif. Après une modification de
l'extension : « Recharger » sur la page des extensions puis rafraîchir la page YouTube.

Installeur : [Inno Setup 6](https://jrsoftware.org/isinfo.php), puis `iscc installer\cliprange.iss`.

Release : pousser un tag `vX.Y.Z` après avoir aligné la version dans `extension/manifest.json`,
`host/ClipRange.Host.csproj` et `installer/cliprange.iss`. Le workflow GitHub compile l'hôte et
l'installeur, puis publie la release avec l'installeur et un zip de l'extension.

Protocole hôte : messages JSON typés par un champ `type`. Requêtes `status`, `install-tool`, `set-settings`,
`make-gif`, `open` ; réponses `status`, `progress`, `done`, `error`. Voir `host/Messages.cs`.
