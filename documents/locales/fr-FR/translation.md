# Consignes de traduction en français

## Ressources et structure

Le [dictionnaire de ressources anglais](../en-US/strings.yml) est l’unique source du texte. Les règles du projet figurent dans le [guide de maintenance de la documentation](../../maintenance.md). Pour commencer une traduction, copier l’intégralité du fichier anglais dans `strings.yml`, dans ce répertoire. Conserver la première ligne `### YamlMime:Resources` et l’encodage UTF-8. Pour les mises à jour, reporter les changements de l’original sans écraser les traductions existantes. Ce fichier `translation.md` est conservé durablement et n’est pas publié sur le site.

La navigation, l’ordre des chapitres, les ancres, les liens, la mise en forme, le code et les images sont partagés et restent hors de `locales/`. Ne pas ajouter ici d’articles, de TOC, de modèles, de scripts ou de configuration.

Modifier uniquement les valeurs du fichier `strings.yml` de cette langue. L’ensemble des clés doit être strictement identique à celui de l’anglais. Ne pas ajouter, supprimer ou renommer de clés de sa propre initiative, même si les textes semblent identiques ou ne sont pas encore apparus dans l’interface. Signaler toute erreur de l’original aux responsables du projet en indiquant la clé concernée. Corriger d’abord la source anglaise et les pages communes, puis les traductions.

## Style et contexte

Adopter le ton d’un manuel technique : précis, sobre et accessible. Expliquer les relations entre opérations avant leurs contraintes. Dans les tutoriels, conserver des étapes explicites et des résultats vérifiables. La rigueur ne demande ni tournures passives ni phrases inutilement longues.

Réviser chaque article dans son ordre de lecture, avec les paragraphes voisins, tableaux et extraits de code. Les clés délimitent le stockage, pas les unités de traduction. Vérifier les accords et les prépositions des liens et fragments mis en valeur dans la phrase complète.

Ne pas terminer par un point les titres, libellés, textes courts visibles de l’accueil et légende du pied de page. Conserver la ponctuation normale dans les paragraphes, diagnostics et instructions d’accessibilité. Employer un terme établi par notion, sans ajouter ponctuellement son équivalent anglais entre parenthèses. Préserver les identifiants d’API, fichiers et réglages.

Conserver une phrase si elle explique l’opération en cours, un choix de conception, un résultat vérifiable ou une condition nécessaire. Supprimer les listes de fonctions inutilisées, les réserves générales et les prérequis répétés. Traduire les titres et les phrases découpées par la mise en forme comme un tout, dans l’ordre naturel du français. Les emplacements `{accent}` et `{lineBreak}` du titre d’accueil sont déplaçables : la présentation ne doit pas imposer la syntaxe anglaise. La suppression d’un passage et de ses clés est coordonnée par la maintenance dans le texte source, le balisage commun et tous les dictionnaires.

## Texte et espaces réservés

Traduire des phrases ou paragraphes complets dans un français technique clair et naturel. Préserver les conditions, négations, unités, étapes et contraintes de durée de vie. Ne pas ajouter de garanties techniques, de prérequis de plateforme, de versions de paquets ou de notes de version absents de l’original. Conserver les noms de produits, de paquets et de fichiers, ainsi que les identifiants C# et Slang, en respectant leur casse.

Chaque valeur doit être une chaîne de caractères non vide, en texte brut, sans HTML ni Markdown. Respecter les guillemets et les échappements YAML ; utiliser des guillemets si nécessaire pour éviter une mauvaise interprétation des deux-points, des dièses ou des valeurs ressemblant à des nombres. Conserver l’ordre des clés de l’anglais pour faciliter la relecture.

Les espaces réservés nommés, comme `{context}` et `{link}`, peuvent insérer du code, des liens ou du texte mis en évidence provenant des pages communes. Ceux de l’interface, comme `{name}`, `{query}` et `{count}`, peuvent aussi recevoir du texte à l’exécution. Vérifier leur utilisation dans la page ou l’interface concernée sans déduire leur contenu du seul nom. Traduire également les libellés de liens et les textes mis en évidence qui disposent de leur propre clé.

L’ordre des espaces réservés peut suivre la syntaxe française. Pour chaque clé, conserver tous les noms, leur casse et les accolades ASCII. Ne pas ajouter, supprimer ou renommer d’espaces réservés, ni les remplacer par des noms d’API, des libellés de liens ou des nombres d’exemple. Ne pas scinder les clés pour recomposer une phrase.

## Liens d’API et sens technique

Les liens sont maintenus dans les pages communes. Les identifiants désignant explicitement une API locale dans le texte ou les tableaux renvoient au type ou au membre correspondant. Les variables, noms de paramètres, types propres aux exemples, noms de paquets et de fichiers, ainsi que les symboles externes sans page de référence locale restent du texte de code. Une même orthographe peut désigner des symboles différents. Les blocs de code ordinaires du tutoriel ne reçoivent pas de liens automatiques. Déplacer un espace réservé entier est permis ; modifier les liens, le code commun ou sa mise en forme ne l’est pas.

Si une API ne possède pas d’entrée de référence générée, son identifiant reste du texte de code dans la page commune, accompagné d’un lien explicite vers le code source. Ne pas le remplacer par un lien vers un autre type ou membre.

Préserver les distinctions suivantes :

- L’enregistrement des commandes, leur soumission, leur exécution sur le GPU et leur achèvement sont des étapes distinctes.
- L’usage d’une ressource, la résidence mémoire ou l’accès CPU prévu, le layout d’une texture et son format de pixel sont des notions distinctes.
- La propriété désigne la responsabilité de libérer une ressource. Un handle ne maintient pas à lui seul la ressource en vie ; la soumission de commandes ne transfère pas sa propriété.
- L’achèvement sur le GPU ne signifie pas toujours que les données de lecture intermédiaires ont été copiées vers la destination CPU de l’application. Conserver les indications sur l’attente de la file responsable.
- Un drawable est la texture de sortie de l’image en cours de rendu, pas nécessairement une image de la chaîne d’échange. Distinguer les vues de ressources des contrôles d’interface.

## Compilation et vérification avant livraison

Exécuter les commandes à la racine du dépôt, avec le SDK .NET requis par le projet et DocFX installés. Dans une nouvelle copie du dépôt, les métadonnées générées dans `documents/api/` sont absentes. Commencer par la compilation complète, qui inclut l’extraction de l’API :

```sh
docfx documents/docfx.json --warningsAsErrors
```

Si les métadonnées d’API existent et correspondent au code source actuel, une modification du dictionnaire seul peut utiliser la compilation normale et la prévisualisation :

```sh
docfx build documents/docfx.json --warningsAsErrors
docfx serve documents/_site --hostname 127.0.0.1 --port 8080
```

Cette langue est déjà enregistrée. Le dictionnaire complet devient disponible après une compilation réussie, sans modifier la configuration. Une clé manquante ou supplémentaire, une valeur vide ou d’un autre type, ou un nom d’espace réservé différent fait échouer la compilation. Celle-ci ne détecte ni les traductions inexactes ni les passages restés en anglais ; une relecture humaine est nécessaire.

Les langues partagent les mêmes chemins. `?lang=fr-FR` sélectionne le français ; changer de langue conserve le chemin et l’ancre. Sans choix explicite, la sélection manuelle enregistrée est prioritaire, puis les préférences du navigateur, puis l’anglais. Si cette traduction n’est pas publiée, ou si le chargement ou la validation des ressources de la page échoue, la page reste en anglais sans mélange de langues phrase par phrase. La liste des langues est triée par code.

Avant livraison, vérifier les clés et les espaces réservés. Contrôler l’accueil, l’entrée Learn, le tutoriel du triangle, les concepts, les exemples et l’interface de référence API sur ordinateur et à 320 pixels de largeur. Relire le tutoriel du point de vue d’un débutant ; vérifier la terminologie, les boutons, la recherche, la copie du code, les liens d’API et la position de la section après un changement de langue. Les identifiants, déclarations et extraits de code restent communs ; seuls les textes du dictionnaire sont traduits. Garder les dictionnaires de test et les fichiers temporaires hors du dépôt et ne pas livrer de traductions de remplissage.

## Terminologie

| Terme anglais    | Forme retenue                                                                                                                  |
| ---------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| API / GPU / RHI  | Conserver les sigles et les expliquer selon l’original                                                                         |
| barrier          | barrière                                                                                                                       |
| buffer           | tampon ; conserver `Buffer`                                                                                                    |
| command buffer   | tampon de commandes                                                                                                            |
| command queue    | file de commandes                                                                                                              |
| drawable         | texture de sortie de l’image en cours de rendu ; conserver `Drawable`, sans supposer qu’elle appartient à une chaîne d’échange |
| graphics context | contexte graphique                                                                                                             |
| graphics API     | API graphique ; DirectX 12, Metal 4 ou Vulkan 1.4                                                                              |
| implementation   | implémentation de l’API graphique ; pour les différences techniques entre implémentations                                      |
| pipeline         | pipeline                                                                                                                       |
| readback         | lecture de données depuis le GPU                                                                                               |
| render pass      | passe de rendu                                                                                                                 |
| resource         | ressource                                                                                                                      |
| shader           | shader                                                                                                                         |
| swap chain       | chaîne d’échange                                                                                                               |
| texture          | texture                                                                                                                        |
| timeline         | ligne de temps                                                                                                                 |
| upload           | transfert de données vers le GPU                                                                                               |
