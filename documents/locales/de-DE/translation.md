# Übersetzungsrichtlinien für Deutsch

## Ressourcen und Aufbau

Das [englische Ressourcenwörterbuch](../en-US/strings.yml) ist die einzige Textquelle. Die Projektregeln stehen im [Leitfaden zur Dokumentationspflege](../../maintenance.md). Für eine neue Übersetzung die vollständige englische Datei als `strings.yml` in dieses Verzeichnis kopieren. Die erste Zeile `### YamlMime:Resources` und die UTF-8-Kodierung beibehalten. Spätere Änderungen mit dem englischen Stand abgleichen, ohne vorhandene Übersetzungen zu überschreiben. Diese `translation.md` bleibt dauerhaft erhalten und wird nicht auf der Website veröffentlicht.

Navigation, Kapitelreihenfolge, Anker, Links, Formatierung, Code und Bilder werden außerhalb von `locales/` gemeinsam gepflegt. Hier keine Artikel, TOCs, Vorlagen, Skripte oder Konfigurationen anlegen.

Bei der Übersetzung nur die Werte der eigenen `strings.yml` ändern. Die Schlüsselmenge muss exakt der englischen entsprechen. Keine Schlüssel eigenständig hinzufügen, löschen oder umbenennen, auch nicht bei ähnlich lautenden oder noch nicht gesehenen Oberflächentexten. Fehler im Original mit dem betroffenen Schlüssel an die Projektverantwortlichen melden. Zuerst die englische Quelle und die gemeinsamen Seiten berichtigen, dann die Übersetzungen nachziehen.

## Stil und Kontext

Der Text soll wie eine technische Einführung lesbar sein: präzise, sachlich und zusammenhängend. Zuerst die Beziehung zwischen Vorgängen erklären, dann die Bedingungen und Grenzen. In Tutorials klare Arbeitsschritte und überprüfbare Ergebnisse nennen. Ein wissenschaftlicher Stil verlangt weder Passivkonstruktionen noch unnötig lange Sätze.

Jeden Artikel in seiner Lesereihenfolge bearbeiten, mit angrenzenden Absätzen, Tabellen und Code. Ressourcenschlüssel sind Speichergrenzen, keine isolierten Übersetzungseinheiten. Linktexte und hervorgehobene Fragmente müssen im vollständigen Satz grammatisch passen.

Überschriften, Bedienelemente sowie die sichtbaren Kurztexte der Startseite und die Fußzeilenbeschriftung erhalten keinen Schlusspunkt. Fließtext, Diagnosen und Bedienhinweise für assistive Technik behalten reguläre Satzzeichen. Pro Begriff eine etablierte Bezeichnung verwenden; englische Entsprechungen nicht vereinzelt in Klammern ergänzen. API-Namen, Dateien und Bezeichner tatsächlicher Einstellungen bleiben unverändert.

Ein Satz sollte die aktuelle Handlung, eine Entwurfsentscheidung, ein überprüfbares Ergebnis oder eine notwendige Bedingung erklären. Aufzählungen ungenutzter Funktionen, pauschale Plattformhinweise und wiederholte Voraussetzungen entfallen. Überschriften und stilistisch aufgeteilte Sätze werden als Ganzes in natürlicher deutscher Wortstellung übersetzt. Die Slots `{accent}` und `{lineBreak}` des Startseitentitels sind verschiebbar; Gestaltung legt keine englische Wortstellung fest. Entfernte Passagen und ihre Schlüssel werden von der Projektpflege in Ausgangstext, gemeinsamem Markup und allen Wörterbüchern zusammen bereinigt.

## Texte und Platzhalter

Ganze Sätze oder Absätze in natürliches, sachliches Deutsch übersetzen. Voraussetzungen, Verneinungen, Einheiten, Handlungsschritte und Lebensdauerbedingungen erhalten. Keine zusätzlichen technischen Zusagen, Plattformanforderungen, Paketversionen oder Versionshinweise einführen. Produkt- und Paketnamen, Dateinamen sowie C#- und Slang-Bezeichner einschließlich Groß- und Kleinschreibung unverändert lassen.

Jeder Wert muss eine nicht leere Zeichenfolge mit reinem Text sein, ohne HTML oder Markdown. YAML-Anführungszeichen und Escape-Sequenzen beachten; bei Doppelpunkten, Rauten oder zahlenähnlichen Werten nötigenfalls Anführungszeichen setzen. Die englische Schlüsselreihenfolge für die Prüfung beibehalten.

Benannte Platzhalter wie `{context}` und `{link}` können gemeinsamen Code, Links oder hervorgehobenen Text einfügen. Platzhalter der Oberfläche wie `{name}`, `{query}` und `{count}` können auch zur Laufzeit mit Text gefüllt werden. Die Bedeutung anhand der jeweiligen Verwendung auf der gemeinsamen Seite oder in der Oberfläche prüfen, nicht aus dem Namen erraten. Linkbeschriftungen und hervorgehobene Texte mit eigenen Ressourcenschlüsseln ebenfalls übersetzen.

Platzhalter dürfen der deutschen Satzstellung folgen. Für jeden Schlüssel müssen jedoch alle Namen, ihre Schreibweise und die geschweiften ASCII-Klammern unverändert erhalten bleiben. Keine Platzhalter hinzufügen, entfernen, umbenennen oder durch angezeigte API-Namen, Linktexte oder Beispielzahlen ersetzen. Keine Schlüssel aufteilen, um Sätze neu zusammenzusetzen.

## API-Links und technische Bedeutung

Links gehören zu den gemeinsamen Seiten. Eindeutige Verweise auf lokale API-Bezeichner in Fließtext und Tabellen führen zum jeweiligen Typ oder Member. Variablen, Parameternamen, Beispieltypen, Paket- und Dateinamen sowie externe Symbole ohne lokale Referenzseite bleiben einfacher Codetext. Gleiche Schreibweisen können verschiedene Symbole bezeichnen. Gewöhnliche Codeblöcke des Tutorials erhalten keine automatischen Links. Ganze Platzhalter dürfen verschoben werden; Linkziele, gemeinsamer Code und dessen Formatierung bleiben unverändert.

Fehlt für eine API ein generierter Referenzeintrag, bleibt ihr Bezeichner auf der gemeinsamen Seite einfacher Codetext mit einem klar bezeichneten Quellcodelink in der Nähe. Keinen anderen Typ oder Member als Linkziel einsetzen.

Bei der Übersetzung diese Unterschiede erhalten:

- Befehle aufzeichnen, einreichen, auf der GPU ausführen und abschließen sind unterschiedliche Schritte.
- Ressourcennutzung, Speicherresidenz beziehungsweise vorgesehener CPU-Zugriff, Texturlayout und Pixelformat sind unterschiedliche Begriffe.
- Besitz bezeichnet die Verantwortung für die Freigabe. Ein Handle hält die Ressource nicht automatisch am Leben; das Einreichen von Befehlen überträgt keinen Besitz.
- GPU-Abschluss bedeutet nicht immer, dass zwischengespeicherte Rücklesedaten bereits am CPU-Ziel der Anwendung liegen. Die Hinweise zum Warten auf die zuständige Queue erhalten.
- Ein Drawable ist die Ausgabetextur des aktuellen Frames und nicht zwangsläufig ein Swapchain-Bild. Ressourcenansichten von UI-Steuerelementen unterscheiden.

## Build und Prüfung vor der Abgabe

Die Befehle im Stammverzeichnis des Repositorys mit dem vom Projekt benötigten .NET SDK und DocFX ausführen. In einem frischen Checkout fehlen die generierten Metadaten in `documents/api/`. Zuerst den vollständigen Build einschließlich API-Extraktion ausführen:

```sh
docfx documents/docfx.json --warningsAsErrors
```

Sind die API-Metadaten bereits vorhanden und entsprechen dem aktuellen Quellcode, genügen für reine Wörterbuchänderungen der normale Build und die Vorschau:

```sh
docfx build documents/docfx.json --warningsAsErrors
docfx serve documents/_site --hostname 127.0.0.1 --port 8080
```

Deutsch ist bereits registriert. Das vollständige Wörterbuch wird nach einem erfolgreichen Build ohne Konfigurationsänderung verfügbar. Fehlende oder zusätzliche Schlüssel, leere Werte, Werte eines anderen Typs und abweichende Platzhalternamen lassen den Build fehlschlagen. Der Build erkennt weder ungenaue Übersetzungen noch verbliebenen englischen Text; eine redaktionelle Prüfung ist erforderlich.

Alle Sprachen teilen dieselben Seitenpfade. `?lang=de-DE` wählt Deutsch; beim Sprachwechsel bleiben Pfad und Anker erhalten. Ohne ausdrückliche Auswahl gelten nacheinander die gespeicherte manuelle Auswahl, die Browsersprachen und Englisch. Ist diese Übersetzung noch nicht veröffentlicht oder scheitert das Laden beziehungsweise die Prüfung der Seitenressourcen, bleibt die Seite vollständig auf Englisch, statt einzelne Sätze zu mischen. Die Sprachliste ist nach Sprachcode sortiert.

Vor der Abgabe Schlüssel und Platzhalter abgleichen. Startseite, Learn-Einstieg, Dreieck-Tutorial, Konzepte, Beispiele und API-Oberfläche auf dem Desktop und bei 320 Pixel Breite prüfen. Das Tutorial aus Sicht eines Einsteigers lesen; Terminologie, Schaltflächen, Suche, Codekopie, API-Links und den Abschnitt nach einem Sprachwechsel kontrollieren. Bezeichner, Deklarationen und Code bleiben gemeinsam; übersetzt werden nur die Texte im Wörterbuch. Testwörterbücher und Prüfdateien außerhalb des Repositorys halten und keine Platzhalterübersetzungen einreichen.

## Terminologie

| Englischer Begriff | Festgelegte Form                                                                                                     |
| ------------------ | -------------------------------------------------------------------------------------------------------------------- |
| API / GPU / RHI    | Abkürzungen beibehalten; nach dem Original erläutern                                                                 |
| barrier            | Barriere                                                                                                             |
| buffer             | Puffer; `Buffer` bleibt unverändert                                                                                  |
| command buffer     | Befehlspuffer                                                                                                        |
| command queue      | Befehlswarteschlange                                                                                                 |
| drawable           | Ausgabetextur des aktuellen Frames; `Drawable` bleibt unverändert, nicht grundsätzlich als Swapchain-Bild bezeichnen |
| graphics context   | Grafikkontext                                                                                                        |
| graphics API       | Grafik-API; DirectX 12, Metal oder Vulkan                                                                            |
| API implementation | Implementierung der Grafik-API; für technische Unterschiede zwischen den Implementierungen                           |
| pipeline           | Pipeline                                                                                                             |
| readback           | Rücklesen von GPU-Daten                                                                                              |
| render pass        | Renderpass                                                                                                           |
| resource           | Ressource                                                                                                            |
| shader             | Shader                                                                                                               |
| swap chain         | Swapchain                                                                                                            |
| texture            | Textur                                                                                                               |
| timeline           | Timeline                                                                                                             |
| upload             | Hochladen von Daten                                                                                                  |
