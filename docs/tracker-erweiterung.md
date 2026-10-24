# Ereignis-Erkennung und Tracker-Erweiterung

## Architektur

Der vorhandene Weg bleibt bestehen:

`Npcap → PacketPipeline → PacketParser → EventDecoded → CombatTracker.Process`

`MeterService` registriert danach einen zweiten Empfänger für Live-Ereignisse:

`EventDecoded → OnLiveTrackerEvent → TrackerDispatcher → ITracker.OnEvent`

Der CombatTracker, seine Locks, seine Berechnungen und der DispatcherTimer der GUI wurden nicht geändert. Der Verteiler reicht dieselben GameEvent-Instanzen weiter. Ein einzelner Produzent behält seine Reihenfolge; gleichzeitige Produzenten werden nach Eintritt in die Verteilersperre serialisiert, nicht nach Zeitstempeln sortiert. Rekursive Veröffentlichungen warten bis zum Ende der aktuellen Verteilung. Registrierungen können per IDisposable aufgehoben werden. Fehler eines Zusatzmoduls werden abgefangen, damit andere Tracker weiterarbeiten.

`ITracker` enthält `OnEvent(GameEvent)`, `GetSnapshot()` und `Reset()`. Neue Zustände implementieren `ITrackerSnapshot`. Tracker müssen ihren Zustand selbst synchronisieren. `OnEvent` soll kurz bleiben und keine GUI oder langsame Dateioperationen aufrufen. Die GUI liest auf ihrem UI-Thread ausschließlich Snapshots, beispielsweise `meter.KillCounter.GetSnapshot()`.

## Marker und PCAP-Aufnahmen

Unter **Einstellungen → Ereignisse** eine Aufnahme starten. Die vorhandene passive Netzwerkerfassung wird dafür neu gestartet; der CombatTracker wird nicht zurückgesetzt. Die Aufnahme beginnt, sobald der vorhandene Adapter-/Verbindungsdetektor Spielverkehr erfasst. Die Statuszeile zeigt den tatsächlich geöffneten PCAP-Pfad.

- Standard-Hotkey: **Ctrl+Shift+M**, auf derselben Seite konfigurierbar. Hotkey mit **Speichern** übernehmen.
- Das frei wählbare Label, etwa `cube_geöffnet`, steht im Feld **Marker-Label**. Während das Einstellungsfenster geöffnet ist, gilt dessen aktueller Text; sonst der gespeicherte Text. Auch der Button **Marker setzen** ist verfügbar.
- Pro Tastendruck wird die aktuelle UTC-Zeit in Unix-Millisekunden erfasst, mit Label, MapId und Gebiet. Dateioperationen laufen anschließend im Hintergrund. Das Label wird nicht aus Spieleingaben gelesen.
- Sidecar: `<aufnahme>.pcap.markers.jsonl`, direkt neben der PCAP. PCAP und Markerdatei gemeinsam aufbewahren. Aufnahmedateien haben eindeutige Namen, damit schnelles Neustarten keine ältere Aufnahme überschreibt.
- Ein kurzer, nicht aktivierender Hinweis bestätigt den Marker. Ohne tatsächlich geöffnete Aufnahme erscheint ein entsprechender Hinweis, und es wird kein Marker angelegt.
- Hotkeys verwenden ausschließlich das vorhandene Windows-RegisterHotKey-Verfahren. Belegte Kombinationen werden gemeldet. Es gibt keinen Tastatur-Hook und keinen Zugriff auf Eingaben im Spiel.

## discover: Kandidaten mit Markern finden

```powershell
dotnet run --project src/AionMeter.Cli -- discover "C:\Aufnahmen\versuch.pcap"
dotnet run --project src/AionMeter.Cli -- discover "C:\Aufnahmen\versuch.pcap" --markers "C:\Aufnahmen\marker.jsonl" --window 3
dotnet run --project src/AionMeter.Cli -- discover "C:\Aufnahmen\versuch.pcap" --events "C:\eigene-regeln\events.json"
```

Standardmäßig wird die passende `.pcap.markers.jsonl` geladen. `--window` setzt das beidseitige Zeitfenster in Sekunden (Standard ±3, maximal ±30). Derselbe PCAP-Replayer und derselbe Parser wie im Meter werden verwendet. `--events` lädt zusätzlich bestätigte eigene Regeln; erfolgreich konfigurierte Nachrichten gelten dann als decodiert und erscheinen nicht unter den unbekannten Kandidaten.

Die Ausgabe nennt **jede noch nicht decodierte Nachricht im Markerfenster** mit Opcode, vollständigem Hex-Körper, Länge, Paketzeitabstand und Gebiet. Darauf folgt eine Zusammenfassung je Label/Opcode mit Markerabdeckung, Treffern innerhalb und außerhalb der Fenster. `*** KANDIDAT` bedeutet: mindestens zwei Marker desselben Labels, Treffer bei allen, höchstens zwei zusätzliche Nachrichten außerhalb dieser Label-Fenster. Häufiger Hintergrundverkehr wird niedriger eingeordnet. Überlappende Fenster zählen dieselbe Nachricht in der Statistik nur einmal; sie können trotzdem mehrere Marker abdecken. Eine Warnung erklärt die dadurch schwächere Aussagekraft.

Die Feldvorschau vergleicht bis zu acht Treffer und deren erste 512 Bytes: mögliche u8/u16LE/u32LE/u32BE/u64LE-Werte an den ersten 24 Offsets, gleichbleibende/veränderliche Werte und druckbare ASCII-Stringfolgen. IDs und Feldgrenzen werden **nicht automatisch als bestätigt übernommen**. ASCII ist nur eine begrenzte Vorschau; andere Textcodierungen können zusätzliche manuelle Analyse benötigen. Keine Treffer sind ebenfalls ein Ergebnis, kein Nachweis für fehlende Spielereignisse.

Für aussagekräftige Versuche mehrere gleiche Aktionen mit mindestens sechs Sekunden Abstand aufnehmen und dazwischen eine ruhige Kontrollphase lassen. Zeitliche Korrelation beweist keinen Seelen-/Beute-Erwerb und keinen Zusammenhang mit einer bestimmten Funktion.

## Eigene Decoderregeln

`data/events.json` enthält vier ausgeschaltete, bewusst leere Vorlagen: `SystemMessage`, `HiddenCubeOpened`, `SoulGained`, `FieldBossSpawned`. Es wurden **keine echten Opcodes oder IDs geraten**. Die Arbeitskopie liegt unter `%AppData%\AionMeter\data\events.json` und bleibt bei Updates erhalten. Auf der Seite **Ereignisse** lässt sie sich öffnen und ohne Neustart neu laden. Bei ungültigem JSON oder ungültigen Regeln bleibt der letzte gültige Decoder aktiv; die Oberfläche meldet den Fehler.

Schema-Beispiel ausschließlich für synthetische Testdaten; `ABCD` ist kein bestätigter Spiel-Opcode:

```json
{
  "events": [
    {
      "name": "MeinEreignis",
      "enabled": false,
      "opcode": "ABCD",
      "fields": [
        { "name": "art", "offset": 0, "type": "u8", "match": 7 },
        { "name": "id", "offset": 1, "type": "u32le" },
        { "name": "text", "offset": 5, "type": "utf8", "length": 12 }
      ],
      "notify": false,
      "sound": false
    }
  ]
}
```

Offsets zählen ab dem ersten Byte des **Nachrichtenkörpers**, ohne Opcode und Frame-Präfix. Unterstützte Typen: `u8`, `bool` (nur 0/1), `u16le`, `u16be`, `u32le`, `u32be`, `i32le`, `u64le`, `i64le`, `f32le`, `varuint` (vorhandenes 32-Bit-Varint), `utf8`, `bytes` (Hex-String). `utf8`/`bytes` benötigen eine feste `length` von 1–4096. `match` ist ein optionaler exakter Feldvergleich, um Varianten desselben Opcodes einzugrenzen. Ein unvollständiger Körper, ungültiges UTF-8, ein unvollständiger Varint oder ein falscher Match erzeugt kein konfiguriertes Ereignis. Die gesamte Regel muss passen. Mehrere passende Regeln können mehrere Ereignisse erzeugen.

Grenzen: Konfigurationsdatei 1 MiB, 256 Typen, 64 Felder pro Typ. Regeln sind validierte Kopien; Änderungen der JSON-Datei wirken erst nach erneutem Laden. Ein neuer primitiver Feldtyp oder eine dynamische/verschachtelte Binärstruktur benötigt weiterhin eine gezielte Code-Erweiterung. Die JSON-Konfiguration ist kein ausführbarer Code.

**Wichtig für unveränderte DPS-Berechnung:** `ConfiguredGameEvent` wird über `PacketParser.ConfiguredEventDecoded` ausschließlich an den Zusatzverteiler geschickt. Es gelangt niemals in `CombatTracker.Process`, da dort selbst ein unbekannter Ereignistyp die letzte Kampfzeit verschieben könnte. Die normalen GameEvents nehmen unverändert ihren bisherigen Weg.

## Ereignisverlauf, Zähler und Benachrichtigungen

`EventLogTracker` sammelt konfigurierte Ereignisse, deduplizierte beobachtete Monster-Tode und bestehende Gebietswechsel. `CounterTracker` zählt pro konfiguriertem Ereignisnamen sowie `MonsterTodBeobachtet`. Nur der KillCounter leitet beobachtete Tode aus den ursprünglichen Identitäts-/Todesereignissen ab; der generische Zähler zählt niemals rohe DeathEvents zusätzlich.

- UI: **Einstellungen → Ereignisse**, Tabelle Zeit / Typ / Gebiet / Details, Typfilter und Freitextsuche. Details enthalten die selbst definierten Feldnamen und Werte.
- Speicher: `%AppData%\AionMeter\trackers\event-log.json` und `event-counters.json`, Hintergrundspeicherung etwa alle zwei Sekunden und Flush beim regulären Beenden. Laden erfolgt beim Start.
- Der Verlauf hält die letzten **2000** Einträge; Details sind auf etwa **8192 Zeichen** je Eintrag begrenzt. Die Zähler bleiben davon unabhängig erhalten. Das ist ein Verlauf, kein unbegrenztes Rohdatenarchiv; dafür PCAP/Diagnose verwenden.
- Snapshots sind schreibgeschützte Kopien. Eine eigene UI-Abfrage aktualisiert die Tabelle; der vorhandene Overlay-DispatcherTimer und CombatTracker bleiben unverändert. Ausgeblendete Seiten pausieren ihre Abfrage.
- `notify: true` zeigt einen kurzen Overlay-Hinweis, `sound: true` spielt den vorhandenen Windows-Systemton. Standardmäßig ist beides aus. Ein eigener UI-Snapshot-Verbraucher bündelt neue Meldungen pro 500 ms, damit Ereignisschübe nicht jeweils ein Fenster öffnen. Beim Laden gespeicherter Ereignisse ertönen keine alten Alarme.
- Nicht aktivierende, undurchsichtige Hinweise verwenden Software-Rendering; sie übernehmen keinen Spielfokus. Keine neuen Assets oder Downloads.
- Beschädigte Zustandsdateien werden erhalten und nicht durch leere Daten ersetzt. Lade- und Schreibfehler erscheinen auf der Ereignisseite. PCAP-Wiedergabe/Demo vergrößern die persistenten Live-Tracker nicht.

## KillCounter

- Verknüpft beobachtete ActorIds mit NpcCodes. Zählt DeathEvents einmal pro beobachtetem Lebenszyklus.
- Bereits tot geladene Monster, unbekannte ActorIds sowie bekannte Spieler und Beschwörungen werden nicht gezählt.
- Ein erneutes NpcSeenEvent nach einem Tod wird als neues Erscheinen gewertet. Das Protokoll liefert hier keine eindeutige Lebenszyklus-ID. Eine sehr spät eintreffende Wiederholung einer Spawn-Meldung ist deshalb nicht sicher von einem neuen Spawn zu unterscheiden.
- Ein echter Gebietswechsel verwirft Actor-Zuordnungen, behält aber die Zähler. Teleports innerhalb desselben Gebiets behalten die Duplikaterkennung.
- **Ein beobachteter Monster-Tod beweist weder einen eigenen Kill noch Kill-Credit, Beute oder Seelen-Erwerb.** Training-Dummys können ohne eigene Klassifikationsdaten nicht zuverlässig von anderen NPCs unterschieden werden.
- NPCs, die schon vor Beginn der Erfassung erschienen sind und deren Spawn nicht mitgelesen wurde, werden konservativ ausgelassen. Eine nach dem Tod gelieferte Identität führt nicht nachträglich zu einem Zähleintrag.
- PCAP-Wiedergabe und Demo verändern den persistenten Live-Zähler nicht. Der Verteiler selbst ist unabhängig von der Quelle und kann in eigenen Offline-Werkzeugen mit einem separaten KillCounter verwendet werden.

Speicherort: `%AppData%\AionMeter\trackers\kill-counter.json`. Der Hintergrunddienst speichert geänderte Zähler etwa alle zwei Sekunden und beim regulären Beenden. Geschrieben wird zunächst eine temporäre Datei; anschließend wird die Zieldatei ersetzt. Nach einem abrupten Prozessende können die letzten noch nicht gespeicherten Änderungen fehlen. Nicht persistiert werden die kurzlebigen ActorIds.

`Reset()` löscht Zähler und Zuordnungen des Zusatztrackers. Der bisherige DPS-Reset setzt den KillCounter nicht zurück. Snapshots enthalten schreibgeschützte Kopien. Beschädigte oder ungültige JSON-Dateien werden nicht überschrieben; `PersistenceError` beschreibt den Fehler. Nach Sicherung und Reparatur bzw. Umbenennung der beschädigten Datei kann die App den Zähler beim nächsten Start wieder speichern.

## Eigene ID-Zuordnung

`data/ids.json` startet mit `{}`. Das vorgesehene Format ist eine Zuordnung von NpcCode zu eigenem Namen, beispielsweise `"123456": "Mein Monstername"`. Die Datei enthält bewusst keine vorbefüllten Namen und ist noch keine zusätzliche Datenquelle für die DPS-Berechnung.

Die App legt eine leere Arbeitskopie unter `%AppData%\AionMeter\data\ids.json` an. Diese eigene Zuordnung wird bei Datenupdates erhalten. Die bestehenden Tabellen des DPS-Meters bleiben unverändert. Es werden keine zusätzlichen Daten oder Assets heruntergeladen.

## Unbekannte Nachrichten untersuchen

In den Einstellungen unter **Allgemein → Netzwerkerfassung** den Schalter **„Unbekannte und nicht dekodierte Nachrichten protokollieren“** aktivieren und speichern. Er ist standardmäßig aus und wirkt ohne Neustart der Erfassung.

Dateien: `%AppData%\AionMeter\logs\unknown-packets-<Zeit>-<Kennung>.jsonl`.

Jede JSON-Zeile enthält:

- `TimeMs`: Paketzeit als Unix-Millisekunden;
- `Opcode`: vierstellige Hex-Darstellung in der Reihenfolge der Opcode-Bytes;
- `Length` und `Hex`: vollständiger Nachrichtenkörper einschließlich unbekannter Daten, ohne Ethernet-/IP-/TCP-Header;
- `MapId` und `Zone`: zuletzt vom Parser erfasstes Gebiet, vorher null;
- `Reason`: „Unbekannter Opcode“, „Kein Ereignis dekodiert“ oder „Teilweise dekodiert“.

„Kein Ereignis dekodiert“ kann auch eine absichtlich ignorierte oder nicht unterstützte Variante eines bekannten Opcodes bezeichnen. „Teilweise dekodiert“ erfasst die bereits erkennbaren unvollständigen Schaden-/Bosslisten-Datensätze; die Instrumentierung kann nicht beweisen, dass jedes Feld jeder bekannten Nachricht verstanden wurde. Heartbeats werden ausgelassen. Unbekannte äußere Nachrichten bleiben protokollierbar, auch wenn bestehende Scanner darin bereits einzelne Identitäten erkennen.

Die Diagnose verändert keine Ereignisse und keine Parser-Entscheidungen. Sie sieht ausschließlich Nachrichten, die der vorhandene Stream-/Frame-Decoder erreicht. TLS-Verkehr wird weiterhin ignoriert; es wird nichts entschlüsselt, eingespeist oder umgangen. Hex-Daten können Namen und andere mitgelesene Inhalte enthalten; die Dateien bleiben lokal.

Live schreibt ein Hintergrunddienst über eine begrenzte Warteschlange. Wenn sie voll ist, läuft die Erfassung weiter; ausgelassene Diagnoseeinträge und Schreibfehler werden beim Beenden im App-Log gemeldet. Für vollständige Offline-Diagnosen die vorhandene PCAP-Aufzeichnung verwenden: Der CLI-Modus wartet auf den Log-Schreiber und verwirft bei normalem Betrieb keine Diagnoseeinträge.

## CLI

Im Repository-Verzeichnis:

```powershell
dotnet run --project src/AionMeter.Cli -- unknown "C:\Aufnahmen\kampf.pcap"
dotnet run --project src/AionMeter.Cli -- unknown "C:\Aufnahmen\kampf.pcap" --log "C:\Aufnahmen\unbekannt.jsonl"
```

`unknown-opcodes` ist ein Alias. Die CLI verwendet PcapReplaySource und damit dieselbe PacketPipeline sowie denselben PacketParser. Die Ausgabe zählt Diagnose-Nachrichten je Opcode und Kategorie. PCAP-Paketanzahl und Anzahl der Protokollnachrichten sind nicht identisch. Das JSONL-Protokoll wird angehängt; für getrennte Versuche unterschiedliche Dateinamen verwenden. Ein Logpfad, der direkt auf die PCAP-Eingabedatei zeigt, wird abgelehnt.

Zeitpunkte eines Seelen-/Item-Erwerbs können so mit den unbekannten Nachrichten verglichen werden. Ein Hex-Wert ist dadurch noch keine bestätigte Item-ID. Neue Decodierungen brauchen überprüfbare Beispiele; es werden keine Item-/Seelen-Ereignisse geraten.

## Tests

```powershell
dotnet test tests/AionMeter.Tests/AionMeter.Tests.csproj -c Release
dotnet test tests/AionMeter.App.Tests/AionMeter.App.Tests.csproj -c Release -p:UseAppHost=false
dotnet build src/AionMeter.Cli/AionMeter.Cli.csproj -c Release
```

Zusätzliche Tests prüfen Reihenfolge und identische Event-Instanzen, parallele und rekursive Veröffentlichungen, Abmeldung und Fehlerisolation, Kill-Duplikate, bereits tote Monster, Spieler/Beschwörungen, Gebietswechsel, parallele Snapshots, JSON-Speicherung und Wiederherstellung sowie Schutz beschädigter Dateien. Diagnose-Tests prüfen vollständiges Hex, Zeit/Gebiet, unbeeinflusste GameEvents und das vollständige Leeren des Offline-Logs. Ein App-Integrationstest vergleicht Schaden, DPS, Kampfdauer und sämtliche CombatantSnapshots mit einem direkt gespeisten CombatTracker. Weitere Tests prüfen den Erhalt eigener ID-/Regeldateien bei Datenupdates, ungültige und reparierte Regeln, die Ereignisseite mit Suche/Typfilter, Marker-Roundtrips, Discovery-Korrelation, Integer-Präzision, Feldgrenzen und generische Persistenz.

Zusätzlich wurde eine selbst erzeugte PCAP mit drei Heartbeats, Gebietswechsel, zwei unbekannten ABCD-Nachrichten und einer nicht dekodierbaren Schaden-Nachricht durch die gebaute CLI geschickt. Erwartet: ABCD zweimal, 0438 einmal; vollständige Hex-Werte und Gebiet 1110 im JSONL-Log. Zusätzlich prüft eine zweite synthetische PCAP zwei getrennte Cube-Marker: ABCD bei beiden Markern ohne Hintergrundtreffer, EEEE bei beiden und fünfmal außerhalb. Mit `--events` verschwindet der konfigurierte ABCD-Kandidat. Dafür wurde kein Spielverkehr benötigt.

Abschlussprüfung dieser Erweiterung: **138 Core-Tests bestanden**, **44 App-/WPF-Tests bestanden**, **kein verbliebener Testfehler**. CLI und WPF-App kompiliert. Die echte Ereignisseite wurde zusätzlich als PNG gerendert und auf Lesbarkeit geprüft. Der synthetische CLI-Durchlauf bestätigt Markerabdeckung, Hintergrundhäufigkeit, vollständiges Hex/Gebiet, Ausschluss konfigurierter Ereignisse und Ablehnung ungültiger Zeitfenster.

Die frühere Ausgangsabweichung im Test `Healing_numbers_keep_the_original_duration_floor_and_handle_zero_and_missing_data` ist behoben: Der Test erwartete fälschlich „1,5/S“, obwohl der geprüfte Formatter Werte ohne Abkürzung ganzzahlig darstellt („2/S“). Nur der Test wurde korrigiert und um Grenzwerte für DPS/Heilung unter deutscher und englischer Kultur erweitert. v0.3.80 verwendet diesen geprüften Ganzzahl-Stand; gegenüber der zwischenzeitlichen v0.3.79 werden Werte unter 1000 wieder ganzzahlig dargestellt. DPS-Berechnung und CombatTracker bleiben unverändert.

## Geänderte und neue Dateien

Core:

- `src/AionMeter.Core/Tracking/ITracker.cs`
- `src/AionMeter.Core/Tracking/TrackerDispatcher.cs`
- `src/AionMeter.Core/Tracking/KillCounter.cs`
- `src/AionMeter.Core/Tracking/ConfiguredTrackers.cs`
- `src/AionMeter.Core/Protocol/PacketParser.cs`
- `src/AionMeter.Core/Protocol/UndecodedPacket.cs`
- `src/AionMeter.Core/Protocol/ConfiguredEventDecoder.cs`
- `src/AionMeter.Core/Protocol/EventDiscovery.cs`
- `src/AionMeter.Core/Capture/LiveCapture.cs`
- `src/AionMeter.Core/Capture/RecordingMarkers.cs`

App und CLI:

- `src/AionMeter.App/App.xaml.cs`
- `src/AionMeter.App/Services/MeterService.cs`
- `src/AionMeter.App/Services/AppSettings.cs`
- `src/AionMeter.App/Services/EmbeddedData.cs`
- `src/AionMeter.App/Services/HotkeyManager.cs`
- `src/AionMeter.App/Services/EventNotificationPump.cs`
- `src/AionMeter.App/Controls/EventsPage.cs`
- `src/AionMeter.App/Controls/EventsPageTheme.xaml`
- `src/AionMeter.App/Windows/EventToastWindow.cs`
- `src/AionMeter.App/Windows/SettingsWindow.xaml`
- `src/AionMeter.App/Windows/SettingsWindow.xaml.cs`
- `src/AionMeter.Cli/Program.cs`

Daten, Tests und Dokumentation:

- `data/ids.json`
- `data/events.json`
- `tests/AionMeter.Tests/TrackerTests.cs`
- `tests/AionMeter.Tests/PacketDiagnosticsTests.cs`
- `tests/AionMeter.Tests/ConfiguredEventsTests.cs`
- `tests/AionMeter.Tests/DiscoveryTests.cs`
- `tests/AionMeter.App.Tests/TrackerIntegrationTests.cs`
- `tests/AionMeter.App.Tests/EventsUiChecks.cs`
- `tests/AionMeter.App.Tests/WindowSmokeTests.cs` (nur zusätzlicher Aufruf der Ereignis-UI-Prüfung)
- `docs/tracker-erweiterung.md`

Keine zusätzlichen NuGet-Pakete. Bestehende andere Arbeitskopie-Änderungen gehören nicht zu dieser Erweiterung.

## Manuell prüfen und Annahmen

1. App starten, **Ereignisse** öffnen, Label und freien Hotkey einstellen, speichern.
2. Eine passive Aufnahme starten. Bei aktivem Spielverkehr tatsächlichen PCAP-Pfad prüfen. Zwei getrennte identische Aktionen markieren; Bestätigung und JSONL-Sidecar prüfen. Ohne Aufnahme darf kein Marker entstehen.
3. Aufnahme stoppen und `discover` wie oben ausführen. Verschiedene Labels und Kontrollphasen vergleichen; erst nach eigener Prüfung echte Regeln eintragen.
4. Regeldatei öffnen, konfigurieren und neu laden. Mit ungültiger Regel muss eine Fehlermeldung erscheinen. Unbekannte echte Opcodes werden standardmäßig nicht als neue Spielereignisse gedeutet.
5. Bei eigener bestätigter Regel Verlauf, Suche, Zähler, optionalen Ton/Hinweis prüfen. App regulär neu starten: Daten sollen erhalten bleiben, alte Alarme dürfen nicht erneut erscheinen.
6. Einen normalen Kampf mit/ohne aktive Zusatzregeln vergleichen; DPS, Schadenssummen, Kampfzeit, Reset und Overlay bleiben gleich. Die automatisierten Tests vergleichen zusätzlich exakt mit dem bisherigen CombatTracker.

Die Erfassung kann nur Nachrichten entdecken, die der bestehende passive Stream-/Frame-Parser bereits erreicht. Verschlüsselte oder nie mitgesendete Daten können nicht rekonstruiert werden. Es gibt keine Eingriffe in Spielprozesse oder Schutzmechanismen. Die Uhrzeit der Marker und der lokalen PCAP muss dieselbe Systemuhr verwenden; importierte Aufnahmen anderer Rechner benötigen passend erzeugte Marker. Ein Live-Spieltest mit tatsächlichen neuen Opcodes ist noch erforderlich und wurde durch die synthetischen Tests nicht ersetzt.
