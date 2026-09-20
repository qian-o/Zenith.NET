# Consignes de traduction en français

## Ressources et structure

L’anglais est l’unique langue source. Copier uniquement les clés et valeurs de `locales/en-US/strings.yml` dans la propre `strings.yml`. Avant la traduction, conserver seulement cette `translation.md` permanente, non publiée. Garder la première ligne `### YamlMime:Resources`.

La navigation, l’ordre des chapitres, les ancres, les liens, la mise en forme, le code et les images sont communs et restent hors de `locales/`. Ne pas ajouter ici d’articles, de TOC, de modèles, de scripts ou de configuration.

Les clés décrivent des fonctions et concepts stables. Elles ne changent pas lors d’une retouche du texte ; éviter les noms liés à une formulation, une position, une numérotation ou une langue. Traduire uniquement les valeurs anglaises dans un français technique clair et naturel.

Les valeurs sont du texte, sans HTML ni Markdown. Les paramètres nommés comme `{context}` ou `{link}` insèrent les éléments de code et les liens communs. Leur ordre peut changer, mais tous leurs noms doivent être conservés. Ne pas ajouter ni supprimer de paramètres. Toutes les clés anglaises sont obligatoires ; une clé manquante ou des paramètres différents font échouer la compilation.

Les commandes DocFX habituelles restent valables. L’ajout du dictionnaire complet rend la langue disponible automatiquement. Toutes les langues partagent les mêmes chemins ; `?lang=fr-FR` choisit la langue. Les listes suivent l’ordre des codes de dossier.

```sh
docfx documents/docfx.json --warningsAsErrors
docfx serve documents/_site --hostname 127.0.0.1 --port 8080
```

## Terminologie

| Terme anglais | Forme retenue |
| --- | --- |
| API / GPU / RHI | Conserver les sigles et les expliquer selon l’original |
| barrier | barrière |
| buffer | tampon ; conserver `Buffer` |
| command buffer | tampon de commandes |
| command queue | file de commandes |
| drawable | image courante à présenter ; conserver `Drawable` |
| graphics context | contexte graphique |
| pipeline | pipeline |
| readback | lecture de données depuis le GPU |
| render pass | passe de rendu |
| resource | ressource |
| shader | shader |
| swap chain | chaîne d’échange |
| texture | texture |
| timeline | ligne de temps |
| upload | transfert de données vers le GPU |
