# Aetherion DPS Meter v0.3.43

## Deutsch

### Neu
- Rangliste im Kampfzentrum: Namen suchen, nach AION2-Klasse filtern und nach DPS/HPS, Gesamtwert, Burst oder Name/Klasse sortieren.
- DPS- und HPS-Modus sind klar markiert; die Werte-Spaltenüberschrift wechselt passend mit.
- Menü „Ansicht“ zum Ein- und Ausblenden der Kennzahlen, des Kampfverlaufs und des Team-Status. Die rechte Seite nutzt den verfügbaren Platz neu, wenn ein Panel ausgeblendet wird.
- Verständliche Zustände für „Warte auf AION2“, „Warte auf Kampf“, laufenden Kampf und abgeschlossene Kampfzusammenfassung.
- Nach dem Kampf erscheinen Spieleranzahl, Gesamtschaden und Kampfzeit als kompakte Zusammenfassung.
- Spielerbereich erklärt jetzt, ob noch Kampfdaten fehlen oder ob Suche/Klassenfilter keine Treffer liefern.
- Overlay-Bedienelemente erscheinen beim Darüberfahren in einer kontrastreichen Leiste am unteren Rand. Das bleibt auch bei komplett transparentem Overlay-Hintergrund lesbar.
- Die Akzentfarben von Overlay und Kampfzentrum sind vereinheitlicht: dunkles Anthrazit, Glut-Orange und Silber; Klassenfarben bleiben nur als kleine Spielerkennung erhalten.
- Neuer Verbindungscheck in den Erfassungseinstellungen: prüft Spielprozess, Npcap, Erfassung, Paketfluss, erkannte Spieldaten und Overlay-Status.
- Ein Supportbericht lässt sich per Klick kopieren. Er enthält technische Statuswerte, aber keine Charakternamen oder Paketmitschnitte.
- Das neue Aetherion-Zeichen wird in App- und Tray-Icon, Kampfzentrum, Timer und Einstellungen verwendet.

### Weiter enthalten
- Schwarz-/Glut-Orange-/Silber-Branding mit dem Aetherion-Emblem und saisonalem Look.
- Verschiebbarer Timer in deiner Nähe; dessen Deckkraftregler verändert nur die Fläche, nicht Text oder Bedienelemente.
- Unbekannte entfernte Spieler-IDs werden nicht als scheinbare Charakternamen ausgegeben.
- Aufgeräumter Boss-Lebensbalken mit HP-Zahl und Prozent oberhalb einer separaten, schmalen Füllleiste; lesbarere Spielerzeilen.

### Technische Hinweise
- Klassen- und Namensfilter arbeiten auf den Daten, die AION2 an den Meter übermittelt. Eine Gruppenzuordnung oder Zielzuordnung pro Spieler wird nicht angezeigt, wenn das Spiel sie nicht liefert.

## English

### New
- Search the combat roster, filter by AION2 class, and sort by DPS/HPS, total value, burst, name, or class.
- DPS and HPS modes are clearly highlighted, and the value column label follows the selected mode.
- A View menu toggles metrics, encounter timeline, and team status; the right column reflows when panels are hidden.
- Clear waiting, active-combat, and completed-combat states, with a post-fight summary of player count, total damage, and duration.
- The roster explains whether combat data has not arrived yet or the active filters returned no matches.
- Hovering the overlay reveals a high-contrast control tray along the bottom edge, readable even with a fully transparent overlay background.
- Overlay and dashboard now share the same charcoal, ember-orange, and silver palette; class colors remain only as small player identifiers.
- A connection check in capture settings reports game-process, Npcap, capture, packet, decoded-data, and overlay status.
- A one-click support summary copies technical status only, without character names or packet recordings.
- The new Aetherion mark is used by the app and tray icons, dashboard, timer, and settings.

### Still included
- Black, ember-orange, and silver Aetherion branding with seasonal styling.
- Movable nearby timer; its opacity control affects only the surface, not text or controls.
- Unknown remote player IDs stay hidden instead of being shown as if they were character names.
- Cleaner boss health bar with the HP value and percentage above a separate slim fill track, plus more readable player rows.

### Technical note
- Filters use the class/name data AION2 actually provides. Per-player party/target attribution is not fabricated when the game does not send it.
