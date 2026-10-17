# Aetherion DPS Meter v0.3.48

Das Kampf-Overlay folgt jetzt der breiten, flachen Referenzansicht. Bestehende Installationen erhalten die neuen Standardmaße von 678 × 330 DIP, kleinere Schrift, transparente Außenflächen und dunkle Spielerbalken. Eigene Einstellungen lassen sich danach weiterhin anpassen.

Zusätzlich zu v0.3.47 wird ein erster Rekordhinweis bei unbekannten maximalen Boss-HP unterdrückt. Ein Regressionstest prüft diesen Fall.

## Anzeige und Komfort

- Automatische Fensterhöhe nach Spielerzahl, begrenzt durch die gewählte Maximalhöhe. Manuelles Ziehen am Größenregler schaltet die Automatik aus.
- Solo-, Gruppen- und Raid-Profile speichern Größe, Position, Zeilengröße, Schriftgröße, Transparenz und Kontrast. Layouts lassen sich ohne Konto- oder Lizenzdaten importieren und exportieren.
- Streamer-Modus verbirgt Spielernamen und Server in Overlay, Raid-Ansicht, Detailauswertungen und kopierten Kampfergebnissen. Bereits geöffnete Auswertungen aktualisieren sich beim Umschalten. Die lokal gespeicherten Original-Kämpfe bleiben unverändert.
- Sparmodus reduziert die Overlay-Aktualisierung von 5 auf 2 Hz und deaktiviert den pulsierenden Statuspunkt. Die Erfassung wird weiterhin vollständig verarbeitet.
- Einstellbare Overlay-Schriftgröße von 80 bis 150 %, hoher Kontrast und farbsehfreundliche Balken. Klassensymbole und Rangzahlen bleiben erhalten.

## Einrichtung und Hilfe

- Neuer Bereich **Komfort & Analyse** über die Overlay-Werkzeugleiste oder Einstellungen → Overlay.
- Einrichtungsassistent mit Sprachauswahl, ausdrücklich gekennzeichneten Beispieldaten und einer Vorschau aus dem echten App-Renderer. Kein laufendes Spiel erforderlich.
- Verbindungsprüfung mit passenden nächsten Schritten für fehlendes Spiel, Adapter-/Npcap-Probleme, fehlende Spielpakete oder ausbleibende Decoder-Ereignisse.
- Lokales Support-ZIP mit vollständiger Inhaltsvorschau. Enthalten sind ein freigegebener technischer Diagnosebericht und die reine Layout-Konfiguration. Keine Namen, IP-Adressen, Lizenzschlüssel, Kontodaten, Paketmitschnitte, Kampfdaten oder rohen Logs. Es wird nichts automatisch versendet.
- Overlay-Darstellung einzeln zurücksetzen; gespeicherte Profile bleiben erhalten.

## Kampfauswertung

- Persönliche Boss-Rekorde nach vollständigen erfassten Kills. Vergleichbar sind nur derselbe Charakter, dieselbe Klasse, derselbe Server, dieselbe Zone und derselbe Boss. Kämpfe mit bekanntem verspätetem Einstieg, unbekanntem maximalen Boss-HP oder deutlich eingeschränkten Gruppendaten zählen nicht.
- Erweiterter Kampfvergleich: eigene DPS, kritische Treffer, Todesmeldungen, Sekunden mit empfangenem eigenen Schaden, längste schadensfreie Folge und Anteile der wichtigsten Skills.
- Todesanalyse mit empfangenen Treffern und Heilungen der letzten zehn Sekunden. Die Ereignisfenster sind begrenzt, damit große Kämpfe den Speicherverbrauch nicht unbegrenzt erhöhen. Ältere Kampfdateien bleiben lesbar und zeigen ausdrücklich an, wenn ihnen die Zeitleiste fehlt.
- Aktivitätswerte erklären die empfangenen Schadensereignisse. Sie messen keine Tastatureingaben und bewerten keine Bossmechaniken oder tatsächlichen Unterbrechungen.
- **Buff-/Debuff-Uptime ist noch nicht verfügbar:** Der aktuelle Decoder liefert keine verlässlichen Beginn-/Ende-Ereignisse. Die Oberfläche zeigt den Datenstatus und erfindet keine Laufzeiten.

## Updates und Wiederherstellung

- Vor Updates, die von dieser Version oder neuer gestartet werden, legt Aetherion lokal einen Checkpoint der aktuellen ausführbaren App und ihrer Einstellungen an. Eine SHA-256-Prüfung erkennt veränderte Backup-Dateien.
- Unter Hilfe & Wiederherstellung kann der Spieler zur gesicherten Version zurückkehren. Die App wird dabei geschlossen und anschließend neu gestartet.
- Beim fehlgeschlagenen Austausch eines portablen Updates versucht der Helfer, die gesicherte App-Datei wiederherzustellen.
- **Der erste Checkpoint entsteht beim nächsten Update nach Installation von v0.3.47.** Ältere Updater können diese Funktion für das Upgrade auf v0.3.47 noch nicht ausführen.

Der Release-Workflow prüft die Core- und App-Tests vor dem Erstellen von Installer und ZIP. Die neue Werkzeugoberfläche ist zunächst deutsch; die bestehende Sprachwahl für Spielnamen und die übrige Oberfläche bleibt verfügbar.
