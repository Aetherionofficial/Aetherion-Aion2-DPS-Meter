# Aetherion DPS Meter v0.3.52

Die sechs angekündigten Funktionen sind jetzt im neuen **Kampfbuch & Training** zusammengeführt. Öffnen über **Verlauf → Kampfbuch & Training** oder **Komfort & Analyse → Kampfanalyse**. Alle neuen Bereiche sind zunächst deutsch.

## Boss-Tagebuch und Verlauf
- Pro Boss, Charakter, Klasse, Server und Zone werden gespeicherte Versuche, Kills, persönliche DPS-Bestwerte, schnellste bestätigte Kills und die zeitliche DPS-Entwicklung gezeigt. Die Entwicklung vergleicht bestätigte Kills mit dem ersten vergleichbaren Kill.
- Liste nach Boss/Notiz, Datum (einschließlich Enddatum), Charakter, Ergebnis und Favoriten filtern. Bossnamen der aktuellen App-Sprache werden bei der Suche berücksichtigt.
- Eigene Notizen mit bis zu 2.000 Zeichen und Favoriten lokal speichern. Vor Auswahlwechsel oder Schließen fragt die App bei ungespeicherten Änderungen nach. Der Streamer-Modus verbirgt Notizen und Spielernamen, ohne Notiz-Entwürfe zu verwerfen.
- Favoriten bleiben normale gespeicherte Kämpfe; ein manuelles Löschen erfordert eine Bestätigung mit zusätzlichem Favoritenhinweis. Notizen, Favoriten und Ziele werden atomar in einer separaten Metadatendatei gespeichert; die ursprünglichen Kampfdateien werden dafür nicht umgeschrieben.
- Zwei Versuche mit demselben Boss und Startzeitpunkt überschreiben sich nicht mehr. Beschädigte Kampfdateien werden übersprungen; beschädigte oder unbekannte Metadatenformate werden vor Überschreiben geschützt.

## Persönliche Trainingsziele
- DPS-Ziel für denselben Boss und Charakter/Klasse/Server/Zone festlegen, z.B. **50000**. Ganze Zahl von 1 bis 1.000.000.000 ohne Trennzeichen; ein leeres Feld entfernt das Ziel.
- Fortschritt am gespeicherten Versuch mit Prozentbalken ablesen. Nach Kampfende erscheint der Fortschritt zusätzlich etwa 20 Sekunden im laufenden Overlay.
- Eingeschränkte Versuche und Wipes bleiben vorläufig. Eine Zielerreichung wird erst in einem vergleichbaren vollständigen Kill bestätigt. Für persönliche Zuordnung müssen Boss, Zone, Charakter, Klasse und Server bekannt sein.

## Heiler-Auswertung
- Empfangene Heilung nach Heiler, Fähigkeit und Empfänger auswerten, mit Mengen, Ereigniszahl, Anteil und HPS über den erfassten Kampf-/Heilungsabschnitt.
- Reine Heiler werden separat erfasst, auch wenn sie keinen eigenen Schaden verursachen. Sie erzeugen keine künstlichen DPS-Ranglistenplätze. Es zählen zuordenbare Heilereignisse im aktiven Kampf mit einem bereits erfassten Teilnehmer als Quelle oder Ziel.
- **Effektive Heilung und Überheilung werden nicht unterschieden:** Die aktuellen Ereignisse erlauben das nicht zuverlässig. Nicht empfangene Heilungen bleiben unbekannt. Begrenzte Detailtabellen werden ausdrücklich als eingeschränkt gekennzeichnet.
- Empfänger und reine Heiler sind erst in neu gespeicherten Kämpfen verfügbar. Ältere Dateien zeigen ihre vorhandenen Heilungsdaten mit einem Hinweis auf fehlende Details.

## Ergebniskarte für Discord
- Boss, Kampfdauer, Ergebnis, Rangliste, Schaden, DPS und Anteile als gestaltete PNG-Karte exportieren. Vorschau vor dem Speichern; große Gruppen werden mit je 15 Spielern pro Seite vollständig auf mehrere Karten verteilt.
- Anonymisierung ist standardmäßig an. Im Streamer-Modus ist sie verpflichtend; auch eine bereits geöffnete Kartenvorschau aktualisiert sich beim Umschalten.
- Eigene Notizen, Ziele und Serverdaten sind nicht auf der Karte enthalten. Der PNG-Export schreibt keine Text-Metadaten. Es wird nichts automatisch an Discord oder den AION2-Chat gesendet; die bestehenden Discord-Tabellen bleiben zusätzlich verfügbar.

## Datenqualität
- Direkt am ausgewählten Kampf und auf der Ergebniskarte werden bekannte Einschränkungen und ihre Folgen erklärt: spätes Einsteigen, fehlende Gruppen-Schadenswerte, unbekannte maximale Boss-HP, unzugeordnete Beschwörungen und begrenzte Heilungsdetails.
- Neue Kampfdateien speichern, ob eine HP-Startreferenz vorlag. Fehlt diese, gilt die vollständige Erfassung als nicht bestätigt. Ältere Dateien bleiben lesbar, werden ohne diese Information aber nicht als neue bestätigte Bestwerte/Zielerreichungen gewertet.
- „Keine erkannten Einschränkungen“ bedeutet ausschließlich, dass die App keine der erkennbaren Einschränkungen festgestellt hat; unbemerkte Datenlücken sind nicht ausgeschlossen.

Start-Updates und Wartungsmodus aus v0.3.51 bleiben enthalten. Eine laufende Wartung wird durch Installation dieses Updates nicht beendet. Kampfspeicherung muss für neue Tagebucheinträge aktiviert sein.

Vor dem Release wurden 137 automatisierte Core- und App-Tests erfolgreich ausgeführt, darunter Speicherfehler, Heiler-Erfassung, Filter, Ziele, WPF-Bedienung und echte PNG-Dateien. Der Release-Workflow führt sie vor dem Paketbau erneut aus.
