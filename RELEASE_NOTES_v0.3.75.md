# Aetherion DPS Meter v0.3.75

## Lesbare, feste Größen im DPS-Overlay

- Beim Ziehen am Seitenrand bleiben Schrift, Icons, Zeilenhöhe und Abstände unverändert. Nur der Namensbereich und die Balken passen sich der verfügbaren Breite an.
- Kompakt / Standard / Groß verwenden Basisschriften von 13 / 14 / 15 sowie Zeilenhöhen von 38 / 44 / 52 Pixeln und Zeilenabstände von 2 / 4 / 6 Pixeln bei normaler Textskalierung.
- Die Größenprofile setzen die angezeigte Zeilenanzahl wieder auf 5 / 8 / 12 und behalten die aktuellen Fenstermaße bei.
- Titel und Timer sind gleich groß und leicht fett. Die Kopf-Icons sind heller, größer und haben mindestens 24 × 24 Pixel Klickfläche.
- Der Leerzustand-Hinweis steht gut lesbar unter der Kopfzeile. Ein leichter Schatten verbessert den Kontrast auf hellem Spielhintergrund.
- Mindestbreite 300 Pixel; kleinere gespeicherte Breiten werden beim Start korrigiert. Lange Spielernamen werden mit „…“ gekürzt und bleiben innerhalb ihrer Spalte.

## Vollständige Zahlen und Hintergrund-Transparenz

- DPS und Gesamtschaden erscheinen ausgeschrieben mit Tausendertrennzeichen, beispielsweise 22,049/S und 1,345,034. Die kleine Zahl unter dem Namen bleibt abgekürzt.
- Der grüne Prozentwert ist gleich groß wie die DPS-Zahl. Feste Zahlenspalten halten auch siebenstellige Werte bei 300 Pixeln Breite sichtbar.
- Hintergrund-Deckkraft direkt über den Regler: Standard 60 %, Minimum 20 %. Text und Icons bleiben davon unabhängig. Hoher Kontrast setzt den Hintergrund auf 100 %, ohne den gewählten Reglerwert zu überschreiben.
- Reglerbewegungen wirken sofort auf die Darstellung; gespeichert wird gebündelt 400 ms nach der letzten Änderung oder beim Schließen.
- Die Heilungsrate verwendet weiterhin Heilung geteilt durch mindestens eine Sekunde Kampfdauer. Die ausgeschriebene Anzeige wird auf ganze Einheiten gerundet; null bleibt 0 und fehlende Daten bleiben „—“.

Kampfberechnung, Parsing und Lizenzprüfung bleiben unverändert.

Validierung: Core- und App-Tests einschließlich echter Textmaße bei 300 Pixeln, Größenprofilen, HighContrast, Heilungsformatierung und isolierter Persistenzprüfung in einem temporären Verzeichnis. Kein Live-Raidtest.
