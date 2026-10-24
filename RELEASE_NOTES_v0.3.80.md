# Aetherion DPS Meter v0.3.80

## Ereignisse entdecken und verfolgen

- Neue Kategorie **Ereignisse** in den Einstellungen: Verlauf mit Zeit, Typ, Gebiet und Details, Typfilter, Suche und lokale Zähler.
- PCAP-Aufnahmen direkt starten/stoppen, Marker mit frei wählbarem Label setzen. Konfigurierbarer globaler Hotkey: standardmäßig **Ctrl+Shift+M**. Marker werden als JSONL-Datei neben der PCAP gespeichert; ein kurzer Hinweis bestätigt die Aktion ohne Fokuswechsel ins Meter.
- Neuer CLI-Befehl **discover**: unbekannte Nachrichten im Zeitfenster um Marker vergleichen, seltene gemeinsame Opcodes hervorheben und Hex-Daten sowie mögliche Integer-/Stringfelder anzeigen.
- Eigene Decodierungen über **events.json**, mit Opcode, Offset, Feldtyp, optionalem Feldvergleich und Ereignisnamen. Regeln ohne Neustart neu laden; ungültige Regeln ersetzen den letzten gültigen Stand nicht.
- Threadsicherer Zusatzverteiler und Tracker-Schnittstelle. Ereignisverlauf und Zähler werden lokal als JSON gespeichert und beim Start geladen.
- KillCounter zählt deduplizierte, beobachtete Monster-Tode nach NpcCode. Ein beobachteter Tod belegt weder einen eigenen Kill noch Beute/Seelen-Erwerb.
- Optionale Overlay-Hinweise und Windows-Systemtöne pro konfiguriertem Ereignis. Alte gespeicherte Ereignisse lösen beim Neustart keine Alarme aus.
- Optionales lokales Rohdatenprotokoll für unbekannte/nicht decodierte Nachrichten, plus CLI-Zusammenfassung der Opcodes.

## Eigene Daten und Grenzen

Die Vorlagen SystemMessage, HiddenCubeOpened, SoulGained und FieldBossSpawned sind **ausgeschaltet** und enthalten keine geratenen Opcodes. Echte IDs, Namen und Felddefinitionen werden vom Nutzer eingetragen. Dies ist die Grundlage für weitere Tracker, noch keine bestätigte automatische Cube-/Seelen-Erkennung. Nur passives Mitlesen über den bestehenden Parser; keine Eingriffe ins Spiel oder Schutzmechanismen. Keine zusätzlichen Fremddaten oder Assets.

## Zahlenformat und Tests

Der geprüfte Overlay-Stand rundet Werte unter 1.000 auf ganze Zahlen: 3 Heilung / 2 Sekunden ergibt **2/S**. Im Unterschied zu v0.3.79 werden hier unterhalb dieser Schwelle keine Nachkommastellen angezeigt. Die unpassende Testerwartung wurde korrigiert; deutsche/englische Kultur, Rundungs- und Abkürzungsgrenzen sowie DPS/Heilung sind abgesichert. Die DPS-Berechnung selbst bleibt unverändert.

Validierung: **138 Core-Tests und 44 App-/WPF-Tests bestanden**; CLI-Build und synthetische PCAP-End-to-End-Prüfung erfolgreich. Echte WPF-Seite gerendert und geprüft. Kein Live-Spieltest mit neuen echten Opcodes. Anleitung, Schema und vollständige Dateiliste: docs/tracker-erweiterung.md.
