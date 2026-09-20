# Übersetzungsrichtlinien für Deutsch

## Ressourcen und Aufbau

Englisch ist die einzige Ausgangssprache. Übersetzungen übernehmen ausschließlich die Schlüssel und Werte aus `locales/en-US/strings.yml` in die eigene `strings.yml`. Bis zur Übersetzung bleibt hier nur diese dauerhafte `translation.md`; sie wird nicht veröffentlicht. Die erste Zeile `### YamlMime:Resources` beibehalten.

Navigation, Kapitelreihenfolge, Anker, Links, Formatierung, Code und Bilder liegen außerhalb von `locales/` und werden gemeinsam genutzt. Keine Artikel, TOCs, Vorlagen, Skripte oder Konfigurationen in diesem Verzeichnis anlegen.

Schlüsselnamen beschreiben beständige Aufgaben und Begriffe. Sie bleiben bei redaktionellen Änderungen unverändert; weder Formulierung noch Position, Nummerierung oder Sprache in neue Namen aufnehmen. Nur die englischen Werte in natürliches, sachliches Deutsch übersetzen.

Werte sind Text, kein HTML oder Markdown. Benannte Platzhalter wie `{context}` oder `{link}` binden gemeinsame Code- und Linkelemente ein. Ihre Reihenfolge darf sich ändern; Namen müssen vollständig erhalten bleiben. Keine Platzhalter hinzufügen oder entfernen. Alle englischen Schlüssel müssen vorhanden sein; fehlende Schlüssel oder abweichende Platzhalter lassen den Build fehlschlagen.

Die bisherigen DocFX-Befehle genügen. Nach dem Hinzufügen der vollständigen Wörterbuchdatei wird die Sprache automatisch verfügbar. Alle Sprachen verwenden dieselben Seitenpfade; `?lang=de-DE` wählt die Sprache. Sprachlisten bleiben nach Verzeichniscode sortiert.

```sh
docfx documents/docfx.json --warningsAsErrors
docfx serve documents/_site --hostname 127.0.0.1 --port 8080
```

## Terminologie

| Englischer Begriff | Festgelegte Form |
| --- | --- |
| API / GPU / RHI | Abkürzungen beibehalten; nach dem Original erläutern |
| barrier | Barriere |
| buffer | Puffer; `Buffer` bleibt unverändert |
| command buffer | Befehlspuffer |
| command queue | Befehlswarteschlange |
| drawable | Aktuelles darstellbares Bild; `Drawable` bleibt unverändert |
| graphics context | Grafikkontext |
| pipeline | Pipeline |
| readback | Rücklesen von GPU-Daten |
| render pass | Renderpass |
| resource | Ressource |
| shader | Shader |
| swap chain | Swapchain |
| texture | Textur |
| timeline | Zeitleiste |
| upload | Hochladen von Daten |
